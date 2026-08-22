using Microsoft.Extensions.Logging;
using OboxSteam.Application.Commons;
using OboxSteam.Application.DTOs.ClassQuizQuestionSetDTO;
using OboxSteam.Application.Interfaces;
using OboxSteam.Application.Notifications;
using OboxSteam.Application.Utils;
using OboxSteam.Application.Validation;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;
using OboxSteam.Domain.Interfaces;

namespace OboxSteam.Application.Services;

public sealed class ClassQuizQuestionSetService : IClassQuizQuestionSetService
{
    private const string LockedConflictMessage =
        "Ask a Manager to update the question bank instead.";

    private readonly IUnitOfWork _unitOfWork;
    private readonly IClaimsService _claimsService;
    private readonly INotificationPublisher _notificationPublisher;
    private readonly ILogger<ClassQuizQuestionSetService> _logger;

    public ClassQuizQuestionSetService(
        IUnitOfWork unitOfWork,
        IClaimsService claimsService,
        INotificationPublisher notificationPublisher,
        ILogger<ClassQuizQuestionSetService> logger)
    {
        _unitOfWork = unitOfWork;
        _claimsService = claimsService;
        _notificationPublisher = notificationPublisher;
        _logger = logger;
    }

    public async Task<ClassQuizQuestionSetResponseDto> PullAsync(Guid assignmentId, Guid classId)
    {
        var mentorId = await GetCurrentMentorIdAsync();
        var classEntity = await MentorScopeValidator.EnsureMentorOwnsClassAsync(
            _unitOfWork, mentorId, classId);

        var assignment = await LoadQuizAssignmentAsync(assignmentId);
        await EnsureAssignmentMatchesClassProgramAsync(assignment, classEntity);

        await EnsureNotLockedAsync(assignmentId, classId);

        var existing = await _unitOfWork.ClassQuizQuestionSets.FirstOrDefaultAsync(
            s => s.ClassId == classId
                 && s.AssignmentId == assignmentId
                 && !s.IsDeleted);

        if (existing != null)
        {
            await SoftRemoveSetCascadeAsync(existing);
        }

        var bankQuestions = await LoadBankQuestionsAsync(assignment.QuestionBankId!.Value);
        QuizAttemptValidator.ValidateBankQuestionsForDraw(assignment, bankQuestions);

        var drawCount = assignment.QuestionCount ?? bankQuestions.Count;
        var drawn = QuizQuestionDrawHelper.Draw(
            bankQuestions,
            drawCount,
            assignment.EasyPercent,
            assignment.MediumPercent,
            assignment.HardPercent,
            allowShuffle: true);

        var now = DateTime.UtcNow;
        var set = new ClassQuizQuestionSet
        {
            Id = Guid.NewGuid(),
            ClassId = classId,
            AssignmentId = assignmentId,
            PulledAt = now,
        };

        await _unitOfWork.ClassQuizQuestionSets.AddAsync(set);

        var questions = new List<ClassQuizQuestion>();
        var options = new List<ClassQuizQuestionOption>();

        for (var i = 0; i < drawn.Count; i++)
        {
            var bankQuestion = drawn[i];
            var question = new ClassQuizQuestion
            {
                Id = Guid.NewGuid(),
                ClassQuizQuestionSetId = set.Id,
                SourceBankQuestionId = bankQuestion.Id,
                QuestionText = bankQuestion.QuestionText,
                QuestionType = bankQuestion.QuestionType,
                Points = bankQuestion.Points,
                DifficultyLevel = bankQuestion.DifficultyLevel,
                OrderIndex = i + 1,
            };
            questions.Add(question);

            foreach (var bankOption in bankQuestion.Options.Where(o => !o.IsDeleted))
            {
                options.Add(new ClassQuizQuestionOption
                {
                    Id = Guid.NewGuid(),
                    ClassQuizQuestionId = question.Id,
                    OptionText = bankOption.OptionText,
                    IsCorrect = bankOption.IsCorrect,
                });
            }
        }

        await _unitOfWork.ClassQuizQuestions.AddRangeAsync(questions);
        await _unitOfWork.ClassQuizQuestionOptions.AddRangeAsync(options);
        await _unitOfWork.SaveChangesAsync();

        await _notificationPublisher.PublishAsync(
            NotificationCatalog.ClassQuizSetEditedByMentor(
                assignmentId,
                classId,
                mentorId,
                classEntity.ProgramId,
                "pulled questions",
                $"{questions.Count} question(s) for \"{assignment.Title}\"",
                assignment.ModuleId));

        _logger.LogInformation(
            "[PullAsync] Mentor {MentorId} pulled {Count} questions for ClassId={ClassId}, AssignmentId={AssignmentId}",
            mentorId, questions.Count, classId, assignmentId);

        return await MapSetResponseAsync(set.Id, isLocked: false);
    }

    public async Task<ClassQuizQuestionSetResponseDto> GetAsync(Guid assignmentId, Guid classId)
    {
        var userId = _claimsService.GetCurrentUserId;
        if (userId == Guid.Empty)
            throw ErrorHelper.Unauthorized("Unauthorized access.");

        var user = await _unitOfWork.Users.GetByIdAsync(userId);
        if (user == null || user.IsDeleted)
            throw ErrorHelper.NotFound("Current user not found.");

        if (user.Role == RoleType.Mentor)
        {
            await MentorScopeValidator.EnsureMentorOwnsClassAsync(_unitOfWork, userId, classId);
        }
        else if (user.Role is not (RoleType.Manager or RoleType.Admin))
        {
            throw ErrorHelper.Forbidden("Only mentors of this class, managers, or super admins can view the quiz set.");
        }

        var set = await _unitOfWork.ClassQuizQuestionSets.FirstOrDefaultAsync(
            s => s.ClassId == classId
                 && s.AssignmentId == assignmentId
                 && !s.IsDeleted);

        if (set == null)
            throw ErrorHelper.NotFound("No pulled quiz set found for this class and assignment.");

        var isLocked = await IsLockedAsync(assignmentId, classId);
        return await MapSetResponseAsync(set.Id, isLocked);
    }

    public async Task<ClassQuizQuestionResponseDto> UpdateQuestionAsync(
        Guid assignmentId,
        Guid classId,
        Guid questionId,
        UpdateClassQuizQuestionRequestDto request)
    {
        var mentorId = await GetCurrentMentorIdAsync();
        var classEntity = await MentorScopeValidator.EnsureMentorOwnsClassAsync(
            _unitOfWork, mentorId, classId);

        var assignment = await LoadQuizAssignmentAsync(assignmentId);
        await EnsureAssignmentMatchesClassProgramAsync(assignment, classEntity);
        await EnsureNotLockedAsync(assignmentId, classId);

        var set = await _unitOfWork.ClassQuizQuestionSets.FirstOrDefaultAsync(
            s => s.ClassId == classId
                 && s.AssignmentId == assignmentId
                 && !s.IsDeleted);

        if (set == null)
            throw ErrorHelper.NotFound("No pulled quiz set found for this class and assignment.");

        var question = await _unitOfWork.ClassQuizQuestions.FirstOrDefaultAsync(
            q => q.Id == questionId
                 && q.ClassQuizQuestionSetId == set.Id
                 && !q.IsDeleted);

        if (question == null)
            throw ErrorHelper.NotFound($"Question '{questionId}' not found in this quiz set.");

        if (request.QuestionText != null)
        {
            if (string.IsNullOrWhiteSpace(request.QuestionText))
                throw ErrorHelper.BadRequest("QuestionText cannot be empty.");
            question.QuestionText = request.QuestionText.Trim();
        }

        if (request.QuestionType != null)
        {
            var normalized = request.QuestionType.Trim();
            if (!QuestionTypeConstants.IsValidCanonical(normalized))
                throw ErrorHelper.BadRequest("QuestionType must be SingleChoice or MultipleChoice.");
            question.QuestionType = normalized;
        }

        if (request.Points.HasValue)
        {
            if (request.Points.Value <= 0)
                throw ErrorHelper.BadRequest("Points must be greater than 0.");
            question.Points = request.Points.Value;
        }

        if (request.DifficultyLevel.HasValue)
        {
            if (request.DifficultyLevel.Value is < 1 or > 5)
                throw ErrorHelper.BadRequest("DifficultyLevel must be between 1 and 5.");
            question.DifficultyLevel = request.DifficultyLevel.Value;
        }

        if (request.OrderIndex.HasValue)
            question.OrderIndex = request.OrderIndex.Value;

        if (request.Options != null)
        {
            if (request.Options.Count < 2)
                throw ErrorHelper.BadRequest("At least 2 options are required.");

            var optionTuples = request.Options
                .Select(o => (
                    OptionText: string.IsNullOrWhiteSpace(o.OptionText) ? string.Empty : o.OptionText.Trim(),
                    IsCorrect: o.IsCorrect ?? false))
                .ToList();

            if (optionTuples.Any(o => string.IsNullOrWhiteSpace(o.OptionText)))
                throw ErrorHelper.BadRequest("OptionText cannot be empty.");

            var validationError = BankQuestionValidationHelper.ValidateQuestionRules(
                question.QuestionType,
                optionTuples);

            if (validationError != null)
                throw ErrorHelper.BadRequest(validationError);

            var existingOptions = await _unitOfWork.ClassQuizQuestionOptions.GetAllAsync(
                o => o.ClassQuizQuestionId == question.Id && !o.IsDeleted);

            if (existingOptions.Count > 0)
                await _unitOfWork.ClassQuizQuestionOptions.SoftRemoveRange(existingOptions);

            var newOptions = optionTuples.Select(o => new ClassQuizQuestionOption
            {
                Id = Guid.NewGuid(),
                ClassQuizQuestionId = question.Id,
                OptionText = o.OptionText,
                IsCorrect = o.IsCorrect,
            }).ToList();

            await _unitOfWork.ClassQuizQuestionOptions.AddRangeAsync(newOptions);
        }
        else
        {
            var existingOptions = await _unitOfWork.ClassQuizQuestionOptions.GetAllAsync(
                o => o.ClassQuizQuestionId == question.Id && !o.IsDeleted);

            var validationError = BankQuestionValidationHelper.ValidateQuestionRules(
                question.QuestionType,
                existingOptions.Select(o => (o.OptionText, o.IsCorrect)).ToList());

            if (validationError != null)
                throw ErrorHelper.BadRequest(validationError);
        }

        await _unitOfWork.ClassQuizQuestions.Update(question);
        await _unitOfWork.SaveChangesAsync();

        await _notificationPublisher.PublishAsync(
            NotificationCatalog.ClassQuizSetEditedByMentor(
                assignmentId,
                classId,
                mentorId,
                classEntity.ProgramId,
                "updated a question",
                question.QuestionText.Length > 80
                    ? question.QuestionText[..80] + "…"
                    : question.QuestionText,
                assignment.ModuleId));

        _logger.LogInformation(
            "[UpdateQuestionAsync] Mentor {MentorId} updated question {QuestionId} for ClassId={ClassId}, AssignmentId={AssignmentId}",
            mentorId, questionId, classId, assignmentId);

        return await MapQuestionResponseAsync(question.Id);
    }

    private async Task SoftRemoveSetCascadeAsync(ClassQuizQuestionSet set)
    {
        var questions = await _unitOfWork.ClassQuizQuestions.GetAllAsync(
            q => q.ClassQuizQuestionSetId == set.Id && !q.IsDeleted);

        if (questions.Count > 0)
        {
            var questionIds = questions.Select(q => q.Id).ToList();
            var options = await _unitOfWork.ClassQuizQuestionOptions.GetAllAsync(
                o => questionIds.Contains(o.ClassQuizQuestionId) && !o.IsDeleted);

            if (options.Count > 0)
                await _unitOfWork.ClassQuizQuestionOptions.SoftRemoveRange(options);

            await _unitOfWork.ClassQuizQuestions.SoftRemoveRange(questions);
        }

        await _unitOfWork.ClassQuizQuestionSets.SoftRemove(set);
    }

    private async Task EnsureNotLockedAsync(Guid assignmentId, Guid classId)
    {
        if (await IsLockedAsync(assignmentId, classId))
            throw ErrorHelper.Conflict(LockedConflictMessage);
    }

    private async Task<bool> IsLockedAsync(Guid assignmentId, Guid classId)
    {
        var enrollments = await _unitOfWork.ClassEnrollments.GetAllAsync(
            ce => ce.ClassId == classId
                  && ce.Status == ClassEnrollmentStatus.Active
                  && !ce.IsDeleted);

        if (enrollments.Count == 0)
            return false;

        var studentIds = enrollments.Select(ce => ce.StudentId).Distinct().ToList();
        var submission = await _unitOfWork.Submissions.FirstOrDefaultAsync(
            s => s.AssignmentId == assignmentId
                 && studentIds.Contains(s.StudentId)
                 && !s.IsDeleted);

        return submission != null;
    }

    private async Task<Assignment> LoadQuizAssignmentAsync(Guid assignmentId)
    {
        var assignment = await _unitOfWork.Assignments.GetByIdAsync(assignmentId);
        if (assignment == null || assignment.IsDeleted)
            throw ErrorHelper.NotFound($"Assignment '{assignmentId}' not found.");

        if (assignment.AssignmentType != AssignmentType.Quiz)
            throw ErrorHelper.BadRequest("Only Quiz assignments support a class quiz question set.");

        if (!assignment.QuestionBankId.HasValue)
            throw ErrorHelper.BadRequest("Assignment has no linked question bank.");

        return assignment;
    }

    private async Task EnsureAssignmentMatchesClassProgramAsync(
        Assignment assignment,
        Class classEntity)
    {
        var module = await _unitOfWork.Modules.GetByIdAsync(assignment.ModuleId);
        if (module == null || module.IsDeleted)
            throw ErrorHelper.NotFound($"Module '{assignment.ModuleId}' not found.");

        if (module.ProgramId != classEntity.ProgramId)
            throw ErrorHelper.BadRequest(MentorScopeValidator.ClassProgramMismatchMessage);
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

        var optionsByQuestion = allOptions
            .GroupBy(o => o.BankQuestionId)
            .ToDictionary(g => g.Key, g => g.ToList());

        foreach (var question in questions)
        {
            question.Options = optionsByQuestion.TryGetValue(question.Id, out var options)
                ? options
                : [];
        }

        return questions;
    }

    private async Task<ClassQuizQuestionSetResponseDto> MapSetResponseAsync(Guid setId, bool isLocked)
    {
        var set = await _unitOfWork.ClassQuizQuestionSets.GetByIdAsync(setId)
                  ?? throw ErrorHelper.NotFound("Quiz set not found.");

        var questions = await _unitOfWork.ClassQuizQuestions.GetAllAsync(
            q => q.ClassQuizQuestionSetId == setId && !q.IsDeleted);

        var questionIds = questions.Select(q => q.Id).ToList();
        var options = questionIds.Count == 0
            ? new List<ClassQuizQuestionOption>()
            : await _unitOfWork.ClassQuizQuestionOptions.GetAllAsync(
                o => questionIds.Contains(o.ClassQuizQuestionId) && !o.IsDeleted);

        var optionsByQuestion = options
            .GroupBy(o => o.ClassQuizQuestionId)
            .ToDictionary(g => g.Key, g => g.ToList());

        return new ClassQuizQuestionSetResponseDto
        {
            Id = set.Id,
            ClassId = set.ClassId,
            AssignmentId = set.AssignmentId,
            PulledAt = set.PulledAt,
            IsLocked = isLocked,
            Questions = questions
                .OrderBy(q => q.OrderIndex)
                .Select(q => new ClassQuizQuestionResponseDto
                {
                    Id = q.Id,
                    SourceBankQuestionId = q.SourceBankQuestionId,
                    QuestionText = q.QuestionText,
                    QuestionType = q.QuestionType,
                    Points = q.Points,
                    DifficultyLevel = q.DifficultyLevel,
                    OrderIndex = q.OrderIndex,
                    Options = optionsByQuestion.GetValueOrDefault(q.Id, new List<ClassQuizQuestionOption>())
                        .Select(o => new ClassQuizQuestionOptionResponseDto
                        {
                            Id = o.Id,
                            OptionText = o.OptionText,
                            IsCorrect = o.IsCorrect,
                        })
                        .ToList(),
                })
                .ToList(),
        };
    }

    private async Task<ClassQuizQuestionResponseDto> MapQuestionResponseAsync(Guid questionId)
    {
        var question = await _unitOfWork.ClassQuizQuestions.GetByIdAsync(questionId)
                       ?? throw ErrorHelper.NotFound("Question not found.");

        var options = await _unitOfWork.ClassQuizQuestionOptions.GetAllAsync(
            o => o.ClassQuizQuestionId == questionId && !o.IsDeleted);

        return new ClassQuizQuestionResponseDto
        {
            Id = question.Id,
            SourceBankQuestionId = question.SourceBankQuestionId,
            QuestionText = question.QuestionText,
            QuestionType = question.QuestionType,
            Points = question.Points,
            DifficultyLevel = question.DifficultyLevel,
            OrderIndex = question.OrderIndex,
            Options = options.Select(o => new ClassQuizQuestionOptionResponseDto
            {
                Id = o.Id,
                OptionText = o.OptionText,
                IsCorrect = o.IsCorrect,
            }).ToList(),
        };
    }

    private async Task<Guid> GetCurrentMentorIdAsync()
    {
        var userId = _claimsService.GetCurrentUserId;
        if (userId == Guid.Empty)
            throw ErrorHelper.Unauthorized("Unauthorized access.");

        var user = await _unitOfWork.Users.GetByIdAsync(userId);
        if (user == null || user.IsDeleted)
            throw ErrorHelper.NotFound("Current user not found.");

        if (user.Role != RoleType.Mentor)
            throw ErrorHelper.Forbidden("Only mentors can perform this action.");

        return userId;
    }
}
