using Microsoft.Extensions.Logging;
using OboxSteam.Application.Commons;
using OboxSteam.Application.DTOs.QuizDTO;
using OboxSteam.Application.Interfaces;
using OboxSteam.Application.Notifications;
using OboxSteam.Application.Utils;
using OboxSteam.Application.Validation;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;
using OboxSteam.Domain.Interfaces;

namespace OboxSteam.Application.Services;

public sealed class QuizAttemptService : IQuizAttemptService
{
    private readonly IClaimsService _claimsService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICertificateService _certificateService;
    private readonly INotificationPublisher _notificationPublisher;
    private readonly ILogger<QuizAttemptService> _logger;
    private readonly ProgramPurchaseLifecycle _programPurchaseLifecycle;

    public QuizAttemptService(
        IClaimsService claimsService,
        IUnitOfWork unitOfWork,
        ICertificateService certificateService,
        INotificationPublisher notificationPublisher,
        ILogger<QuizAttemptService> logger,
        ProgramPurchaseLifecycle programPurchaseLifecycle)
    {
        _claimsService = claimsService;
        _unitOfWork = unitOfWork;
        _certificateService = certificateService;
        _notificationPublisher = notificationPublisher;
        _logger = logger;
        _programPurchaseLifecycle = programPurchaseLifecycle;
    }

    public async Task<QuizAttemptResponseDto> StartQuiz(Guid assignmentId)
    {
        var student = await EnrollmentAccessValidator.GetCurrentStudentForEnrollAsync(
            _unitOfWork,
            _claimsService,
            QuizAttemptValidator.QuizForbiddenMessage);

        QuizAttemptValidator.ValidateAssignmentIdRequired(assignmentId);

        var assignment = await _unitOfWork.Assignments.GetByIdAsync(assignmentId);
        QuizAttemptValidator.ValidateAssignmentForQuizStart(assignment);

        var enrollment = await QuizAttemptValidator.ValidateActiveModuleEnrollmentAsync(
            _unitOfWork,
            student.Id,
            assignment!);

        var (personalDue, personalUntil) = await AssessmentAttemptPolicy.GetPersonalWindowAsync(
            _unitOfWork,
            student.Id,
            assignment!.Id,
            enrollment.Id);

        QuizAttemptValidator.ValidateAssignmentAvailability(
            assignment,
            DateTime.UtcNow,
            personalDue,
            personalUntil);

        var pendingSubmission = await _unitOfWork.Submissions.FirstOrDefaultAsync(
            s => s.AssignmentId == assignmentId
                 && s.StudentId == student.Id
                 && s.Status == SubmissionStatus.Pending
                 && !s.IsDeleted);

        if (pendingSubmission != null)
        {
            _logger.LogInformation(
                "StartQuiz resuming Pending submission. SubmissionId={SubmissionId}, StudentId={StudentId}",
                pendingSubmission.Id, student.Id);

            var pendingQuestions = await LoadSnapshotQuestionsAsync(pendingSubmission.Id);
            var pendingAnswers = await _unitOfWork.QuizAnswers.GetAllAsync(
                a => a.SubmissionId == pendingSubmission.Id && !a.IsDeleted);

            return new QuizAttemptResponseDto
            {
                SubmissionId = pendingSubmission.Id,
                AssignmentId = assignment!.Id,
                StudentId = student.Id,
                StudentName = student.FullName,
                AttemptNumber = pendingSubmission.AttemptNumber,
                TimeLimitMinutes = assignment.TimeLimitMinutes,
                StartedAt = pendingSubmission.StartedAt,
                ExpiresAt = pendingSubmission.ExpiresAt,
                Questions = pendingQuestions
                    .OrderBy(q => q.OrderIndex)
                    .Select(question => new QuizQuestionForStudentDto
                    {
                        Id = question.Id,
                        QuestionText = question.QuestionText,
                        QuestionType = question.QuestionType,
                        Points = question.Points,
                        OrderIndex = question.OrderIndex,
                        Options = question.Options
                            .Where(o => !o.IsDeleted)
                            .Select(option => new QuizOptionForStudentDto
                            {
                                Id = option.Id,
                                OptionText = option.OptionText
                            })
                            .ToList()
                    })
                    .ToList(),
                SavedAnswers = pendingQuestions
                    .Select(question => new QuizAnswerItemDto
                    {
                        QuestionId = question.Id,
                        SelectedOptionIds = pendingAnswers
                            .Where(a => a.QuizQuestionId == question.Id)
                            .Select(a => a.QuizOptionId)
                            .ToList()
                    })
                    .Where(item => item.SelectedOptionIds.Count > 0)
                    .ToList()
            };
        }

        await QuizAttemptValidator.ValidateMaxAttemptsForNewStartAsync(
            _unitOfWork,
            assignment!,
            student.Id,
            enrollment.Id);

        var classId = await ResolveStudentClassIdForAssignmentAsync(student.Id, assignment!);
        ClassQuizQuestionSet? classSet = null;
        if (classId.HasValue)
        {
            classSet = await _unitOfWork.ClassQuizQuestionSets.FirstOrDefaultAsync(
                s => s.ClassId == classId.Value
                     && s.AssignmentId == assignmentId
                     && !s.IsDeleted);
        }

        List<BankQuestion>? drawnQuestions = null;
        List<ClassQuizQuestion>? classQuestions = null;

        if (classSet != null)
        {
            classQuestions = await LoadClassSetQuestionsAsync(classSet.Id);
            if (assignment!.AllowShuffle)
                classQuestions = classQuestions.OrderBy(_ => Random.Shared.Next()).ToList();
            else
                classQuestions = classQuestions.OrderBy(q => q.OrderIndex).ToList();
        }
        else
        {
            var bankQuestions = await LoadBankQuestionsAsync(assignment!.QuestionBankId!.Value);
            QuizAttemptValidator.ValidateBankQuestionsForDraw(assignment, bankQuestions);

            var drawCount = assignment.QuestionCount ?? bankQuestions.Count;
            drawnQuestions = QuizQuestionDrawHelper.Draw(
                bankQuestions,
                drawCount,
                assignment.EasyPercent,
                assignment.MediumPercent,
                assignment.HardPercent,
                assignment.AllowShuffle);
        }

        var completedAttempts = await _unitOfWork.Submissions.GetAllAsync(
            s => s.AssignmentId == assignmentId
                 && s.StudentId == student.Id
                 && !s.IsDeleted
                 && (s.Status == SubmissionStatus.Graded || s.Status == SubmissionStatus.TurnedIn));

        var now = DateTime.UtcNow;
        var submission = new Submission
        {
            Id = Guid.NewGuid(),
            Code = GenerateSubmissionCode(),
            AssignmentId = assignment!.Id,
            StudentId = student.Id,
            ModuleEnrollmentId = enrollment.Id,
            AttemptNumber = completedAttempts.Count + 1,
            Status = SubmissionStatus.Pending,
            StartedAt = now,
            ExpiresAt = assignment.TimeLimitMinutes.HasValue
                ? now.AddMinutes(assignment.TimeLimitMinutes.Value)
                : null,
            CreatedAt = now,
            CreatedBy = student.Id,
            IsDeleted = false
        };

        await _unitOfWork.Submissions.AddAsync(submission);

        if (classQuestions != null)
            await CreateSnapshotsFromClassSetAsync(assignment, submission, classQuestions, student.Id, now);
        else
            await CreateSnapshotsAsync(assignment, submission, drawnQuestions!, student.Id, now);

        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation(
            "StartQuiz created new attempt. SubmissionId={SubmissionId}, AssignmentId={AssignmentId}, StudentId={StudentId}, UsedClassSet={UsedClassSet}",
            submission.Id, assignmentId, student.Id, classSet != null);

        var snapshotQuestions = await LoadSnapshotQuestionsAsync(submission.Id);

        return new QuizAttemptResponseDto
        {
            SubmissionId = submission.Id,
            AssignmentId = assignment.Id,
            StudentId = student.Id,
            StudentName = student.FullName,
            AttemptNumber = submission.AttemptNumber,
            TimeLimitMinutes = assignment.TimeLimitMinutes,
            StartedAt = submission.StartedAt,
            ExpiresAt = submission.ExpiresAt,
            Questions = snapshotQuestions
                .OrderBy(q => q.OrderIndex)
                .Select(question => new QuizQuestionForStudentDto
                {
                    Id = question.Id,
                    QuestionText = question.QuestionText,
                    QuestionType = question.QuestionType,
                    Points = question.Points,
                    OrderIndex = question.OrderIndex,
                    Options = question.Options
                        .Where(o => !o.IsDeleted)
                        .Select(option => new QuizOptionForStudentDto
                        {
                            Id = option.Id,
                            OptionText = option.OptionText
                        })
                        .ToList()
                })
                .ToList(),
            SavedAnswers = []
        };
    }

    public async Task<QuizAttemptResponseDto?> GetQuiz(Guid submissionId)
    {
        var submission = await _unitOfWork.Submissions.GetByIdAsync(submissionId);
        QuizAttemptValidator.ValidateSubmissionExists(submission, submissionId);
        QuizAttemptValidator.ValidateSubmissionPending(submission!);

        var assignment = await _unitOfWork.Assignments.GetByIdAsync(submission!.AssignmentId);
        if (assignment == null || assignment.IsDeleted)
            return null;

        await QuizAttemptValidator.EnsureCanViewQuizSubmissionAsync(
            _unitOfWork,
            _claimsService,
            submission,
            assignment);

        var subjectStudent = await _unitOfWork.Users.GetByIdAsync(submission.StudentId);
        var snapshotQuestions = await LoadSnapshotQuestionsAsync(submission.Id);
        var savedAnswers = await _unitOfWork.QuizAnswers.GetAllAsync(
            a => a.SubmissionId == submission.Id && !a.IsDeleted);

        return new QuizAttemptResponseDto
        {
            SubmissionId = submission.Id,
            AssignmentId = assignment.Id,
            StudentId = submission.StudentId,
            StudentName = subjectStudent?.FullName,
            AttemptNumber = submission.AttemptNumber,
            TimeLimitMinutes = assignment.TimeLimitMinutes,
            StartedAt = submission.StartedAt,
            ExpiresAt = submission.ExpiresAt,
            Questions = snapshotQuestions
                .OrderBy(q => q.OrderIndex)
                .Select(question => new QuizQuestionForStudentDto
                {
                    Id = question.Id,
                    QuestionText = question.QuestionText,
                    QuestionType = question.QuestionType,
                    Points = question.Points,
                    OrderIndex = question.OrderIndex,
                    Options = question.Options
                        .Where(o => !o.IsDeleted)
                        .Select(option => new QuizOptionForStudentDto
                        {
                            Id = option.Id,
                            OptionText = option.OptionText
                        })
                        .ToList()
                })
                .ToList(),
            SavedAnswers = snapshotQuestions
                .Select(question => new QuizAnswerItemDto
                {
                    QuestionId = question.Id,
                    SelectedOptionIds = savedAnswers
                        .Where(a => a.QuizQuestionId == question.Id)
                        .Select(a => a.QuizOptionId)
                        .ToList()
                })
                .Where(item => item.SelectedOptionIds.Count > 0)
                .ToList()
        };
    }

    public async Task<SaveDraftAnswersResponseDto> SaveDraftAnswers(
        Guid submissionId,
        SaveDraftAnswersRequestDto request)
    {
        var student = await EnrollmentAccessValidator.GetCurrentStudentForEnrollAsync(
            _unitOfWork,
            _claimsService,
            QuizAttemptValidator.QuizForbiddenMessage);

        QuizAttemptValidator.ValidateSaveDraftRequest(request);

        var submission = await _unitOfWork.Submissions.GetByIdAsync(submissionId);
        QuizAttemptValidator.ValidateSubmissionExists(submission, submissionId);
        QuizAttemptValidator.ValidateSubmissionOwnership(submission!, student.Id);
        QuizAttemptValidator.ValidateSubmissionPending(submission!);

        var draftAssignment = await _unitOfWork.Assignments.GetByIdAsync(submission!.AssignmentId);
        if (draftAssignment == null || draftAssignment.IsDeleted)
            throw ErrorHelper.NotFound("Assignment not found.");

        await QuizAttemptValidator.ValidateActiveModuleEnrollmentAsync(
            _unitOfWork,
            student.Id,
            draftAssignment);

        var snapshotQuestions = await LoadSnapshotQuestionsAsync(submissionId);
        QuizAttemptValidator.ValidateSubmissionHasQuizSnapshot(snapshotQuestions);
        QuizAttemptValidator.ValidateAnswersForDraft(snapshotQuestions, request.Answers);

        var savedCount = await UpsertAnswersAsync(submission!, request.Answers, student.Id);

        await _unitOfWork.Submissions.Update(submission!);
        await _unitOfWork.SaveChangesAsync();

        return new SaveDraftAnswersResponseDto
        {
            LastSavedAt = submission!.UpdatedAt ?? DateTime.UtcNow,
            SavedCount = savedCount
        };
    }

    public async Task<QuizResultResponseDto> SubmitQuiz(
        Guid submissionId,
        SubmitQuizAnswersRequestDto request)
    {
        var student = await EnrollmentAccessValidator.GetCurrentStudentForEnrollAsync(
            _unitOfWork,
            _claimsService,
            QuizAttemptValidator.QuizForbiddenMessage);

        QuizAttemptValidator.ValidateSubmitRequest(request);

        var submission = await _unitOfWork.Submissions.GetByIdAsync(submissionId);
        QuizAttemptValidator.ValidateSubmissionExists(submission, submissionId);
        QuizAttemptValidator.ValidateSubmissionOwnership(submission!, student.Id);
        QuizAttemptValidator.ValidateSubmissionPending(submission!);

        var assignment = await _unitOfWork.Assignments.GetByIdAsync(submission!.AssignmentId);
        QuizAttemptValidator.ValidateAssignmentForQuizStart(assignment);
        await QuizAttemptValidator.ValidateActiveModuleEnrollmentAsync(
            _unitOfWork,
            student.Id,
            assignment!);

        var snapshotQuestions = await LoadSnapshotQuestionsAsync(submissionId);
        QuizAttemptValidator.ValidateSubmissionHasQuizSnapshot(snapshotQuestions);

        var savedAnswers = await _unitOfWork.QuizAnswers.GetAllAsync(
            a => a.SubmissionId == submissionId && !a.IsDeleted);

        var mergedAnswers = MergeAnswersWithSavedDrafts(
            snapshotQuestions,
            savedAnswers,
            request?.Answers ?? []);

        QuizAttemptValidator.ValidateAnswersForSubmit(snapshotQuestions, mergedAnswers);

        await UpsertAnswersAsync(submission, mergedAnswers, student.Id);

        var answers = await _unitOfWork.QuizAnswers.GetAllAsync(
            a => a.SubmissionId == submissionId && !a.IsDeleted);

        var grade = QuizScoreCalculator.Calculate(assignment!, snapshotQuestions, answers);
        var submittedAt = DateTime.UtcNow;

        submission.Status = SubmissionStatus.Graded;
        submission.AssignedGrade = grade.AssignedGrade;
        submission.SubmittedAt = submittedAt;

        await _unitOfWork.Submissions.Update(submission);
        await _unitOfWork.SaveChangesAsync();

        await RecalculateEnrollmentProgressAsync(submission);

        if (!grade.Passed)
        {
            await _programPurchaseLifecycle.TryCloseAfterFailedAssignmentAsync(
                student.Id,
                assignment!.Id,
                submission.ModuleEnrollmentId);
        }

        var module = await _unitOfWork.Modules.GetByIdAsync(assignment!.ModuleId);
        Guid? programEnrollmentId = null;
        if (submission.ModuleEnrollmentId.HasValue)
        {
            var moduleEnrollment = await _unitOfWork.ModuleEnrollments.GetByIdAsync(
                submission.ModuleEnrollmentId.Value);
            programEnrollmentId = moduleEnrollment?.ProgramEnrollmentId;
        }

        await _notificationPublisher.PublishAsync(NotificationCatalog.QuizGraded(
            student.Id,
            submission.Id,
            assignment.Id,
            grade.Passed,
            module?.ProgramId,
            assignment.Title,
            programEnrollmentId));

        _logger.LogInformation(
            "SubmitQuiz graded submission. SubmissionId={SubmissionId}, Grade={Grade}, Passed={Passed}",
            submissionId, grade.AssignedGrade, grade.Passed);

        return new QuizResultResponseDto
        {
            SubmissionId = submission.Id,
            AssignmentId = assignment!.Id,
            StudentId = student.Id,
            StudentName = student.FullName,
            AttemptNumber = submission.AttemptNumber,
            StartedAt = submission.StartedAt,
            AssignedGrade = grade.AssignedGrade,
            MaxPoints = assignment.MaxPoints,
            PassScore = assignment.PassScore,
            Passed = grade.Passed,
            CorrectCount = grade.CorrectCount,
            TotalQuestions = grade.TotalQuestions,
            Status = submission.Status,
            SubmittedAt = submission.SubmittedAt
        };
    }

    public async Task<QuizResultResponseDto?> GetQuizResult(Guid submissionId)
    {
        var submission = await _unitOfWork.Submissions.GetByIdAsync(submissionId);
        QuizAttemptValidator.ValidateSubmissionExists(submission, submissionId);
        QuizAttemptValidator.ValidateSubmissionGraded(submission!);

        var assignment = await _unitOfWork.Assignments.GetByIdAsync(submission!.AssignmentId);
        if (assignment == null || assignment.IsDeleted)
            return null;

        await QuizAttemptValidator.EnsureCanViewQuizSubmissionAsync(
            _unitOfWork,
            _claimsService,
            submission,
            assignment);

        var subjectStudent = await _unitOfWork.Users.GetByIdAsync(submission.StudentId);
        var snapshotQuestions = await LoadSnapshotQuestionsAsync(submissionId);
        var answers = await _unitOfWork.QuizAnswers.GetAllAsync(
            a => a.SubmissionId == submissionId && !a.IsDeleted);

        var grade = QuizScoreCalculator.Calculate(assignment, snapshotQuestions, answers);

        return new QuizResultResponseDto
        {
            SubmissionId = submission.Id,
            AssignmentId = assignment.Id,
            StudentId = submission.StudentId,
            StudentName = subjectStudent?.FullName,
            AttemptNumber = submission.AttemptNumber,
            StartedAt = submission.StartedAt,
            AssignedGrade = grade.AssignedGrade,
            MaxPoints = assignment.MaxPoints,
            PassScore = assignment.PassScore,
            Passed = grade.Passed,
            CorrectCount = grade.CorrectCount,
            TotalQuestions = grade.TotalQuestions,
            Status = submission.Status,
            SubmittedAt = submission.SubmittedAt
        };
    }

    private async Task<List<BankQuestion>> LoadBankQuestionsAsync(Guid questionBankId)
    {
        var questions = await _unitOfWork.BankQuestions.GetAllAsync(
            q => q.QuestionBankId == questionBankId && !q.IsDeleted);

        if (questions.Count == 0)
            return questions;

        var questionIds = questions.Select(q => q.Id).ToList();
        var allOptions = await _unitOfWork.BankQuestionOptions.GetAllAsync(
            o => questionIds.Contains(o.BankQuestionId) && !o.IsDeleted);

        var optionsByQuestion = allOptions.GroupBy(o => o.BankQuestionId)
            .ToDictionary(g => g.Key, g => g.ToList());

        foreach (var question in questions)
        {
            question.Options = optionsByQuestion.TryGetValue(question.Id, out var options)
                ? options
                : [];
        }

        return questions;
    }

    private async Task<Guid?> ResolveStudentClassIdForAssignmentAsync(Guid studentId, Assignment assignment)
    {
        var module = await _unitOfWork.Modules.GetByIdAsync(assignment.ModuleId);
        if (module == null || module.IsDeleted)
            return null;

        var classEnrollment = await _unitOfWork.ClassEnrollments.FirstOrDefaultAsync(
            ce => ce.StudentId == studentId
                  && ce.Status == ClassEnrollmentStatus.Active
                  && !ce.IsDeleted
                  && ce.Class.ProgramId == module.ProgramId,
            ce => ce.Class);

        return classEnrollment?.ClassId;
    }

    private async Task<List<ClassQuizQuestion>> LoadClassSetQuestionsAsync(Guid setId)
    {
        var questions = await _unitOfWork.ClassQuizQuestions.GetAllAsync(
            q => q.ClassQuizQuestionSetId == setId && !q.IsDeleted);

        if (questions.Count == 0)
            return questions;

        var questionIds = questions.Select(q => q.Id).ToList();
        var allOptions = await _unitOfWork.ClassQuizQuestionOptions.GetAllAsync(
            o => questionIds.Contains(o.ClassQuizQuestionId) && !o.IsDeleted);

        var optionsByQuestion = allOptions
            .GroupBy(o => o.ClassQuizQuestionId)
            .ToDictionary(g => g.Key, g => g.ToList());

        foreach (var question in questions)
        {
            question.Options = optionsByQuestion.TryGetValue(question.Id, out var options)
                ? options
                : [];
        }

        return questions;
    }

    private async Task CreateSnapshotsFromClassSetAsync(
        Assignment assignment,
        Submission submission,
        IReadOnlyList<ClassQuizQuestion> classQuestions,
        Guid userId,
        DateTime now)
    {
        var quizQuestions = new List<QuizQuestion>();
        var quizOptions = new List<QuizOption>();

        for (var index = 0; index < classQuestions.Count; index++)
        {
            var classQuestion = classQuestions[index];
            var activeOptions = classQuestion.Options.Where(o => !o.IsDeleted).ToList();

            var quizQuestion = new QuizQuestion
            {
                Id = Guid.NewGuid(),
                AssignmentId = assignment.Id,
                SubmissionId = submission.Id,
                BankQuestionId = classQuestion.SourceBankQuestionId,
                QuestionText = classQuestion.QuestionText,
                QuestionType = classQuestion.QuestionType,
                Points = classQuestion.Points,
                OrderIndex = index + 1,
                AttemptNumber = submission.AttemptNumber,
                CreatedAt = now,
                CreatedBy = userId,
                IsDeleted = false
            };

            quizQuestions.Add(quizQuestion);

            var optionEntities = activeOptions
                .Select(option => new QuizOption
                {
                    Id = Guid.NewGuid(),
                    QuestionId = quizQuestion.Id,
                    OptionText = option.OptionText,
                    IsCorrect = option.IsCorrect,
                    CreatedAt = now,
                    CreatedBy = userId,
                    IsDeleted = false
                })
                .ToList();

            if (assignment.ShuffleOptions)
                optionEntities = optionEntities.OrderBy(_ => Random.Shared.Next()).ToList();

            quizOptions.AddRange(optionEntities);
        }

        await _unitOfWork.QuizQuestions.AddRangeAsync(quizQuestions);
        await _unitOfWork.QuizOptions.AddRangeAsync(quizOptions);
    }

    private async Task<List<QuizQuestion>> LoadSnapshotQuestionsAsync(Guid submissionId)
    {
        var questions = await _unitOfWork.QuizQuestions.GetAllAsync(
            q => q.SubmissionId == submissionId && !q.IsDeleted);

        if (questions.Count == 0)
            return questions;

        var questionIds = questions.Select(q => q.Id).ToList();
        var allOptions = await _unitOfWork.QuizOptions.GetAllAsync(
            o => questionIds.Contains(o.QuestionId) && !o.IsDeleted);

        var optionsByQuestion = allOptions.GroupBy(o => o.QuestionId)
            .ToDictionary(g => g.Key, g => g.ToList());

        foreach (var question in questions)
        {
            question.Options = optionsByQuestion.TryGetValue(question.Id, out var options)
                ? options
                : [];
        }

        return questions.OrderBy(q => q.OrderIndex).ToList();
    }

    private async Task CreateSnapshotsAsync(
        Assignment assignment,
        Submission submission,
        IReadOnlyList<BankQuestion> drawnQuestions,
        Guid userId,
        DateTime now)
    {
        var quizQuestions = new List<QuizQuestion>();
        var quizOptions = new List<QuizOption>();

        for (var index = 0; index < drawnQuestions.Count; index++)
        {
            var bankQuestion = drawnQuestions[index];
            var activeBankOptions = bankQuestion.Options.Where(o => !o.IsDeleted).ToList();

            var quizQuestion = new QuizQuestion
            {
                Id = Guid.NewGuid(),
                AssignmentId = assignment.Id,
                SubmissionId = submission.Id,
                BankQuestionId = bankQuestion.Id,
                QuestionText = bankQuestion.QuestionText,
                QuestionType = bankQuestion.QuestionType,
                Points = bankQuestion.Points,
                OrderIndex = index + 1,
                AttemptNumber = submission.AttemptNumber,
                CreatedAt = now,
                CreatedBy = userId,
                IsDeleted = false
            };

            quizQuestions.Add(quizQuestion);

            var optionEntities = activeBankOptions
                .Select(bankOption => new QuizOption
                {
                    Id = Guid.NewGuid(),
                    QuestionId = quizQuestion.Id,
                    OptionText = bankOption.OptionText,
                    IsCorrect = bankOption.IsCorrect,
                    CreatedAt = now,
                    CreatedBy = userId,
                    IsDeleted = false
                })
                .ToList();

            if (assignment.ShuffleOptions)
                optionEntities = optionEntities.OrderBy(_ => Random.Shared.Next()).ToList();

            quizOptions.AddRange(optionEntities);
        }

        await _unitOfWork.QuizQuestions.AddRangeAsync(quizQuestions);
        await _unitOfWork.QuizOptions.AddRangeAsync(quizOptions);
    }

    private async Task<int> UpsertAnswersAsync(
        Submission submission,
        IReadOnlyList<QuizAnswerItemDto> answers,
        Guid userId)
    {
        var now = DateTime.UtcNow;
        var savedCount = 0;

        foreach (var answer in answers)
        {
            await _unitOfWork.QuizAnswers.HardRemove(
                a => a.SubmissionId == submission.Id && a.QuizQuestionId == answer.QuestionId);

            var selectedIds = answer.SelectedOptionIds ?? [];

            foreach (var optionId in selectedIds)
            {
                await _unitOfWork.QuizAnswers.AddAsync(new QuizAnswer
                {
                    Id = Guid.NewGuid(),
                    SubmissionId = submission.Id,
                    QuizQuestionId = answer.QuestionId,
                    QuizOptionId = optionId,
                    CreatedAt = now,
                    CreatedBy = userId,
                    IsDeleted = false
                });

                savedCount++;
            }
        }

        submission.UpdatedAt = now;
        submission.UpdatedBy = userId;
        return savedCount;
    }

    private static List<QuizAnswerItemDto> MergeAnswersWithSavedDrafts(
        IReadOnlyList<QuizQuestion> snapshotQuestions,
        IReadOnlyList<QuizAnswer> savedAnswers,
        IReadOnlyList<QuizAnswerItemDto> requestAnswers)
    {
        var requestByQuestion = requestAnswers.ToDictionary(a => a.QuestionId);
        var savedByQuestion = savedAnswers
            .GroupBy(a => a.QuizQuestionId)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyList<Guid>)g.Select(a => a.QuizOptionId).ToList());

        var merged = new List<QuizAnswerItemDto>();

        foreach (var question in snapshotQuestions)
        {
            if (requestByQuestion.TryGetValue(question.Id, out var requestAnswer))
            {
                merged.Add(requestAnswer);
                continue;
            }

            if (savedByQuestion.TryGetValue(question.Id, out var savedOptionIds) && savedOptionIds.Count > 0)
            {
                merged.Add(new QuizAnswerItemDto
                {
                    QuestionId = question.Id,
                    SelectedOptionIds = savedOptionIds
                });
            }
        }

        return merged;
    }

    private static string GenerateSubmissionCode()
        => $"SUB-{Guid.NewGuid():N}"[..12].ToUpperInvariant();

    /// <summary>
    /// Recomputes module and program progress for the enrollment behind a graded submission,
    /// so passing an assignment immediately advances the curriculum progress.
    /// </summary>
    private async Task RecalculateEnrollmentProgressAsync(Submission submission)
    {
        if (!submission.ModuleEnrollmentId.HasValue)
        {
            return;
        }

        var moduleEnrollment = await _unitOfWork.ModuleEnrollments.GetByIdAsync(submission.ModuleEnrollmentId.Value);
        if (moduleEnrollment == null || moduleEnrollment.IsDeleted)
        {
            return;
        }

        await ActivityProgressCalculationHelper.RecalculateModuleProgressAsync(_unitOfWork, moduleEnrollment);

        if (moduleEnrollment.ProgramEnrollmentId.HasValue)
        {
            await ActivityProgressCalculationHelper.RecalculateProgramProgressAsync(
                _unitOfWork,
                moduleEnrollment.ProgramEnrollmentId.Value,
                moduleEnrollment);
            await TryEnsureProgramCertificateAsync(moduleEnrollment.ProgramEnrollmentId.Value);
        }

        await _unitOfWork.SaveChangesAsync();
    }

    private async Task TryEnsureProgramCertificateAsync(Guid programEnrollmentId)
    {
        try
        {
            await _certificateService.EnsureProgramCertificateInternalAsync(programEnrollmentId);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "[TryEnsureProgramCertificateAsync] Failed for enrollment {EnrollmentId}. Learning progress was not rolled back.",
                programEnrollmentId);
        }
    }
}
