using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OboxSteam.Application.Commons;
using OboxSteam.Application.DTOs.QuizDTO;
using OboxSteam.Application.Exceptions;
using OboxSteam.Application.Interfaces;
using OboxSteam.Application.Notifications;
using OboxSteam.Application.Services;
using OboxSteam.Application.Validation;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;
using OboxSteam.Test.Helpers;

namespace OboxSteam.Test.UnitTests;

public sealed class QuizAttemptServiceTests
{
    private readonly Guid _studentId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private readonly Guid _moduleId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private readonly Guid _programId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private readonly Guid _assignmentId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private readonly Guid _questionBankId = Guid.Parse("55555555-5555-5555-5555-555555555555");
    private readonly Guid _enrollmentId = Guid.Parse("66666666-6666-6666-6666-666666666666");

    private readonly InMemoryUnitOfWork _db = new();
    private readonly Mock<IClaimsService> _claimsService = new();
    private readonly Mock<ICertificateService> _certificateService = new();
    private readonly Mock<INotificationPublisher> _notificationPublisher = new();
    private readonly Mock<ICurrentTime> _currentTime = new();

    private QuizAttemptService CreateSut()
    {
        _claimsService.Setup(c => c.GetCurrentUserId).Returns(_studentId);
        _currentTime.Setup(t => t.GetCurrentTime()).Returns(DateTime.UtcNow);
        _notificationPublisher
            .Setup(n => n.PublishAsync(It.IsAny<NotificationCommand>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _certificateService
            .Setup(c => c.EnsureProgramCertificateInternalAsync(It.IsAny<Guid>()))
            .ReturnsAsync((OboxSteam.Application.DTOs.CertificateDTO.CertificateDetailDto?)null);

        var lifecycle = new ProgramPurchaseLifecycle(
            _db,
            _currentTime.Object,
            _notificationPublisher.Object,
            NullLogger<ProgramPurchaseLifecycle>.Instance);

        return new QuizAttemptService(
            _claimsService.Object,
            _db,
            _certificateService.Object,
            _notificationPublisher.Object,
            NullLogger<QuizAttemptService>.Instance,
            lifecycle);
    }

    private void SeedStudentAndEnrollment(ModuleType moduleType = ModuleType.Theory)
    {
        _db.Users.Seed(new User
        {
            Id = _studentId,
            Code = "STD-001",
            Email = "student@test.com",
            Role = RoleType.Student,
            IsDeleted = false
        });

        _db.Modules.Seed(new Module
        {
            Id = _moduleId,
            Code = "MOD-001",
            Name = "Module 1",
            ProgramId = _programId,
            ModuleType = moduleType,
            IsDeleted = false
        });

        _db.ModuleEnrollments.Seed(new ModuleEnrollment
        {
            Id = _enrollmentId,
            StudentId = _studentId,
            ModuleId = _moduleId,
            Status = EnrollmentStatus.Active,
            ProgramEnrollmentId = null,
            IsDeleted = false
        });
    }

    private Assignment SeedQuizAssignment(int maxAttempts = 3, decimal maxPoints = 10m, decimal passScore = 5m)
    {
        var assignment = new Assignment
        {
            Id = _assignmentId,
            Code = "ASN-QUIZ-001",
            ModuleId = _moduleId,
            Title = "Unit Quiz",
            AssignmentType = AssignmentType.Quiz,
            QuestionBankId = _questionBankId,
            QuestionCount = 1,
            MaxPoints = (int)maxPoints,
            PassScore = passScore,
            MaxAttempts = maxAttempts,
            AllowShuffle = false,
            ShuffleOptions = false,
            EasyPercent = 100,
            MediumPercent = 0,
            HardPercent = 0,
            TimeLimitMinutes = 30,
            IsRequiredForModulePass = false,
            IsDeleted = false
        };

        _db.Assignments.Seed(assignment);
        return assignment;
    }

    private (BankQuestion Question, BankQuestionOption Correct, BankQuestionOption Wrong) SeedBankQuestion()
    {
        var questionId = Guid.Parse("77777777-7777-7777-7777-777777777777");
        var correctId = Guid.Parse("88888888-8888-8888-8888-888888888888");
        var wrongId = Guid.Parse("99999999-9999-9999-9999-999999999999");

        var question = new BankQuestion
        {
            Id = questionId,
            QuestionBankId = _questionBankId,
            QuestionText = "What is 2 + 2?",
            QuestionType = QuestionTypeConstants.SingleChoice,
            Points = 1,
            DifficultyLevel = 1,
            IsDeleted = false
        };

        var correct = new BankQuestionOption
        {
            Id = correctId,
            BankQuestionId = questionId,
            OptionText = "4",
            IsCorrect = true,
            IsDeleted = false
        };

        var wrong = new BankQuestionOption
        {
            Id = wrongId,
            BankQuestionId = questionId,
            OptionText = "5",
            IsCorrect = false,
            IsDeleted = false
        };

        _db.BankQuestions.Seed(question);
        _db.BankQuestionOptions.Seed(correct, wrong);
        return (question, correct, wrong);
    }

    /// <summary>
    /// Seeds a submission with one single-choice snapshot question (correct + wrong options).
    /// </summary>
    private (Guid SubmissionId, Guid QuestionId, Guid CorrectOptionId, Guid WrongOptionId) SeedAttemptSnapshot(
        SubmissionStatus status = SubmissionStatus.Pending,
        Guid? studentId = null,
        decimal? assignedGrade = null,
        DateTime? submittedAt = null)
    {
        var submissionId = Guid.NewGuid();
        var questionId = Guid.NewGuid();
        var correctOptionId = Guid.NewGuid();
        var wrongOptionId = Guid.NewGuid();

        _db.Submissions.Seed(new Submission
        {
            Id = submissionId,
            Code = $"SUB-{submissionId:N}"[..12].ToUpperInvariant(),
            AssignmentId = _assignmentId,
            StudentId = studentId ?? _studentId,
            ModuleEnrollmentId = _enrollmentId,
            AttemptNumber = 1,
            Status = status,
            AssignedGrade = assignedGrade,
            StartedAt = DateTime.UtcNow.AddMinutes(-5),
            SubmittedAt = submittedAt,
            IsDeleted = false
        });

        _db.QuizQuestions.Seed(new QuizQuestion
        {
            Id = questionId,
            AssignmentId = _assignmentId,
            SubmissionId = submissionId,
            QuestionText = "Snapshot Q",
            QuestionType = QuestionTypeConstants.SingleChoice,
            Points = 1,
            OrderIndex = 1,
            AttemptNumber = 1,
            IsDeleted = false
        });

        _db.QuizOptions.Seed(
            new QuizOption
            {
                Id = correctOptionId,
                QuestionId = questionId,
                OptionText = "Correct",
                IsCorrect = true,
                IsDeleted = false
            },
            new QuizOption
            {
                Id = wrongOptionId,
                QuestionId = questionId,
                OptionText = "Wrong",
                IsCorrect = false,
                IsDeleted = false
            });

        return (submissionId, questionId, correctOptionId, wrongOptionId);
    }

    [Fact]
    public async Task StartQuiz_CreatesNewAttempt_FromQuestionBank()
    {
        SeedStudentAndEnrollment();
        SeedQuizAssignment();
        SeedBankQuestion();
        var sut = CreateSut();

        var result = await sut.StartQuiz(_assignmentId);

        Assert.Equal(_assignmentId, result.AssignmentId);
        Assert.Equal(1, result.AttemptNumber);
        Assert.Equal(30, result.TimeLimitMinutes);
        Assert.Single(result.Questions);
        Assert.Empty(result.SavedAnswers);
        Assert.Single(_db.Submissions.Items);
        Assert.Equal(SubmissionStatus.Pending, _db.Submissions.Items[0].Status);
        Assert.Single(_db.QuizQuestions.Items);
        Assert.Equal(2, _db.QuizOptions.Items.Count);
        Assert.Equal(1, _db.SaveChangesCallCount);
    }

    [Fact]
    public async Task StartQuiz_ResumesPendingSubmission()
    {
        SeedStudentAndEnrollment();
        SeedQuizAssignment();

        var submissionId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var questionId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var optionId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");

        _db.Submissions.Seed(new Submission
        {
            Id = submissionId,
            Code = "SUB-PENDING1",
            AssignmentId = _assignmentId,
            StudentId = _studentId,
            ModuleEnrollmentId = _enrollmentId,
            AttemptNumber = 1,
            Status = SubmissionStatus.Pending,
            StartedAt = DateTime.UtcNow.AddMinutes(-5),
            IsDeleted = false
        });

        _db.QuizQuestions.Seed(new QuizQuestion
        {
            Id = questionId,
            AssignmentId = _assignmentId,
            SubmissionId = submissionId,
            QuestionText = "Pending Q",
            QuestionType = QuestionTypeConstants.SingleChoice,
            Points = 1,
            OrderIndex = 1,
            AttemptNumber = 1,
            IsDeleted = false
        });

        _db.QuizOptions.Seed(new QuizOption
        {
            Id = optionId,
            QuestionId = questionId,
            OptionText = "A",
            IsCorrect = true,
            IsDeleted = false
        });

        var sut = CreateSut();

        var result = await sut.StartQuiz(_assignmentId);

        Assert.Equal(submissionId, result.SubmissionId);
        Assert.Equal(1, result.AttemptNumber);
        Assert.Single(result.Questions);
        Assert.Single(_db.Submissions.Items);
        Assert.Equal(0, _db.SaveChangesCallCount);
    }

    [Fact]
    public async Task StartQuiz_ThrowsNotFound_WhenAssignmentMissing()
    {
        SeedStudentAndEnrollment();
        var sut = CreateSut();

        await Assert.ThrowsAsync<NotFoundException>(() => sut.StartQuiz(_assignmentId));
    }

    [Fact]
    public async Task StartQuiz_ThrowsConflict_WhenMaxAttemptsReached()
    {
        SeedStudentAndEnrollment(ModuleType.Experiential);
        SeedQuizAssignment(maxAttempts: 1);
        SeedBankQuestion();

        _db.Submissions.Seed(new Submission
        {
            Id = Guid.NewGuid(),
            Code = "SUB-DONE0001",
            AssignmentId = _assignmentId,
            StudentId = _studentId,
            ModuleEnrollmentId = _enrollmentId,
            AttemptNumber = 1,
            Status = SubmissionStatus.Graded,
            AssignedGrade = 10,
            IsDeleted = false
        });

        var sut = CreateSut();

        var ex = await Assert.ThrowsAsync<ConflictException>(() => sut.StartQuiz(_assignmentId));
        Assert.Contains("Maximum number of attempts", ex.Message);
    }

    [Fact]
    public async Task StartQuiz_ThrowsForbidden_WhenCallerIsNotStudent()
    {
        _db.Users.Seed(new User
        {
            Id = _studentId,
            Code = "MGR-001",
            Email = "manager@test.com",
            Role = RoleType.Manager,
            IsDeleted = false
        });

        var sut = CreateSut();

        var ex = await Assert.ThrowsAsync<ForbiddenException>(() => sut.StartQuiz(_assignmentId));
        Assert.Equal(QuizAttemptValidator.QuizForbiddenMessage, ex.Message);
    }

    [Fact]
    public async Task GetQuiz_ReturnsPendingAttemptWithSavedAnswers()
    {
        SeedStudentAndEnrollment();
        SeedQuizAssignment();

        var submissionId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");
        var questionId = Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee");
        var optionId = Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff");

        _db.Submissions.Seed(new Submission
        {
            Id = submissionId,
            Code = "SUB-GET00001",
            AssignmentId = _assignmentId,
            StudentId = _studentId,
            ModuleEnrollmentId = _enrollmentId,
            AttemptNumber = 1,
            Status = SubmissionStatus.Pending,
            StartedAt = DateTime.UtcNow,
            IsDeleted = false
        });

        _db.QuizQuestions.Seed(new QuizQuestion
        {
            Id = questionId,
            AssignmentId = _assignmentId,
            SubmissionId = submissionId,
            QuestionText = "Q1",
            QuestionType = QuestionTypeConstants.SingleChoice,
            Points = 1,
            OrderIndex = 1,
            AttemptNumber = 1,
            IsDeleted = false
        });

        _db.QuizOptions.Seed(new QuizOption
        {
            Id = optionId,
            QuestionId = questionId,
            OptionText = "Yes",
            IsCorrect = true,
            IsDeleted = false
        });

        _db.QuizAnswers.Seed(new QuizAnswer
        {
            Id = Guid.NewGuid(),
            SubmissionId = submissionId,
            QuizQuestionId = questionId,
            QuizOptionId = optionId,
            IsDeleted = false
        });

        var sut = CreateSut();

        var result = await sut.GetQuiz(submissionId);

        Assert.NotNull(result);
        Assert.Equal(submissionId, result!.SubmissionId);
        Assert.Single(result.Questions);
        Assert.Single(result.SavedAnswers);
        Assert.Equal(questionId, result.SavedAnswers[0].QuestionId);
        Assert.Equal(optionId, result.SavedAnswers[0].SelectedOptionIds[0]);
    }

    [Fact]
    public async Task GetQuiz_ThrowsForbidden_WhenSubmissionBelongsToAnotherStudent()
    {
        SeedStudentAndEnrollment();
        SeedQuizAssignment();

        var submissionId = Guid.NewGuid();
        _db.Submissions.Seed(new Submission
        {
            Id = submissionId,
            Code = "SUB-OTHER001",
            AssignmentId = _assignmentId,
            StudentId = Guid.NewGuid(),
            ModuleEnrollmentId = _enrollmentId,
            AttemptNumber = 1,
            Status = SubmissionStatus.Pending,
            IsDeleted = false
        });

        var sut = CreateSut();

        await Assert.ThrowsAsync<ForbiddenException>(() => sut.GetQuiz(submissionId));
    }

    [Fact]
    public async Task SaveDraftAnswers_UpsertsAnswersAndReturnsSavedCount()
    {
        SeedStudentAndEnrollment();
        SeedQuizAssignment();

        var submissionId = Guid.NewGuid();
        var questionId = Guid.NewGuid();
        var optionId = Guid.NewGuid();

        _db.Submissions.Seed(new Submission
        {
            Id = submissionId,
            Code = "SUB-DRAFT001",
            AssignmentId = _assignmentId,
            StudentId = _studentId,
            ModuleEnrollmentId = _enrollmentId,
            AttemptNumber = 1,
            Status = SubmissionStatus.Pending,
            IsDeleted = false
        });

        _db.QuizQuestions.Seed(new QuizQuestion
        {
            Id = questionId,
            AssignmentId = _assignmentId,
            SubmissionId = submissionId,
            QuestionText = "Draft Q",
            QuestionType = QuestionTypeConstants.SingleChoice,
            Points = 1,
            OrderIndex = 1,
            AttemptNumber = 1,
            IsDeleted = false
        });

        _db.QuizOptions.Seed(new QuizOption
        {
            Id = optionId,
            QuestionId = questionId,
            OptionText = "A",
            IsCorrect = true,
            IsDeleted = false
        });

        var sut = CreateSut();

        var response = await sut.SaveDraftAnswers(submissionId, new SaveDraftAnswersRequestDto
        {
            Answers =
            [
                new QuizAnswerItemDto
                {
                    QuestionId = questionId,
                    SelectedOptionIds = [optionId]
                }
            ]
        });

        Assert.Equal(1, response.SavedCount);
        Assert.Single(_db.QuizAnswers.Items);
        Assert.Equal(optionId, _db.QuizAnswers.Items[0].QuizOptionId);
        Assert.Equal(1, _db.SaveChangesCallCount);
    }

    [Fact]
    public async Task SubmitQuiz_GradesCorrectAnswers_AndPublishesNotification()
    {
        SeedStudentAndEnrollment();
        SeedQuizAssignment(maxPoints: 10m, passScore: 5m);

        var submissionId = Guid.NewGuid();
        var questionId = Guid.NewGuid();
        var correctOptionId = Guid.NewGuid();
        var wrongOptionId = Guid.NewGuid();

        _db.Submissions.Seed(new Submission
        {
            Id = submissionId,
            Code = "SUB-SUBMIT01",
            AssignmentId = _assignmentId,
            StudentId = _studentId,
            ModuleEnrollmentId = _enrollmentId,
            AttemptNumber = 1,
            Status = SubmissionStatus.Pending,
            StartedAt = DateTime.UtcNow.AddMinutes(-2),
            IsDeleted = false
        });

        _db.QuizQuestions.Seed(new QuizQuestion
        {
            Id = questionId,
            AssignmentId = _assignmentId,
            SubmissionId = submissionId,
            QuestionText = "2+2?",
            QuestionType = QuestionTypeConstants.SingleChoice,
            Points = 1,
            OrderIndex = 1,
            AttemptNumber = 1,
            IsDeleted = false
        });

        _db.QuizOptions.Seed(
            new QuizOption
            {
                Id = correctOptionId,
                QuestionId = questionId,
                OptionText = "4",
                IsCorrect = true,
                IsDeleted = false
            },
            new QuizOption
            {
                Id = wrongOptionId,
                QuestionId = questionId,
                OptionText = "5",
                IsCorrect = false,
                IsDeleted = false
            });

        var sut = CreateSut();

        var result = await sut.SubmitQuiz(submissionId, new SubmitQuizAnswersRequestDto
        {
            Answers =
            [
                new QuizAnswerItemDto
                {
                    QuestionId = questionId,
                    SelectedOptionIds = [correctOptionId]
                }
            ]
        });

        Assert.Equal(SubmissionStatus.Graded, result.Status);
        Assert.Equal(10m, result.AssignedGrade);
        Assert.True(result.Passed);
        Assert.Equal(1, result.CorrectCount);
        Assert.Equal(1, result.TotalQuestions);
        Assert.Equal(SubmissionStatus.Graded, _db.Submissions.Items[0].Status);
        Assert.NotNull(_db.Submissions.Items[0].SubmittedAt);

        _notificationPublisher.Verify(
            n => n.PublishAsync(It.IsAny<NotificationCommand>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SubmitQuiz_ThrowsBadRequest_WhenAnyQuestionUnanswered()
    {
        SeedStudentAndEnrollment();
        SeedQuizAssignment();

        var submissionId = Guid.NewGuid();
        var questionId = Guid.NewGuid();
        var optionId = Guid.NewGuid();

        _db.Submissions.Seed(new Submission
        {
            Id = submissionId,
            Code = "SUB-MISS0001",
            AssignmentId = _assignmentId,
            StudentId = _studentId,
            ModuleEnrollmentId = _enrollmentId,
            AttemptNumber = 1,
            Status = SubmissionStatus.Pending,
            IsDeleted = false
        });

        _db.QuizQuestions.Seed(new QuizQuestion
        {
            Id = questionId,
            AssignmentId = _assignmentId,
            SubmissionId = submissionId,
            QuestionText = "Q",
            QuestionType = QuestionTypeConstants.SingleChoice,
            Points = 1,
            OrderIndex = 1,
            AttemptNumber = 1,
            IsDeleted = false
        });

        _db.QuizOptions.Seed(new QuizOption
        {
            Id = optionId,
            QuestionId = questionId,
            OptionText = "A",
            IsCorrect = true,
            IsDeleted = false
        });

        var sut = CreateSut();

        await Assert.ThrowsAsync<BadRequestException>(() =>
            sut.SubmitQuiz(submissionId, new SubmitQuizAnswersRequestDto
            {
                Answers = []
            }));
    }

    [Fact]
    public async Task GetQuizResult_ReturnsGradedScore()
    {
        SeedStudentAndEnrollment();
        SeedQuizAssignment(maxPoints: 10m, passScore: 5m);

        var submissionId = Guid.NewGuid();
        var questionId = Guid.NewGuid();
        var correctOptionId = Guid.NewGuid();

        _db.Submissions.Seed(new Submission
        {
            Id = submissionId,
            Code = "SUB-RESULT01",
            AssignmentId = _assignmentId,
            StudentId = _studentId,
            ModuleEnrollmentId = _enrollmentId,
            AttemptNumber = 1,
            Status = SubmissionStatus.Graded,
            AssignedGrade = 10,
            StartedAt = DateTime.UtcNow.AddMinutes(-10),
            SubmittedAt = DateTime.UtcNow.AddMinutes(-1),
            IsDeleted = false
        });

        _db.QuizQuestions.Seed(new QuizQuestion
        {
            Id = questionId,
            AssignmentId = _assignmentId,
            SubmissionId = submissionId,
            QuestionText = "Q",
            QuestionType = QuestionTypeConstants.SingleChoice,
            Points = 1,
            OrderIndex = 1,
            AttemptNumber = 1,
            IsDeleted = false
        });

        _db.QuizOptions.Seed(new QuizOption
        {
            Id = correctOptionId,
            QuestionId = questionId,
            OptionText = "OK",
            IsCorrect = true,
            IsDeleted = false
        });

        _db.QuizAnswers.Seed(new QuizAnswer
        {
            Id = Guid.NewGuid(),
            SubmissionId = submissionId,
            QuizQuestionId = questionId,
            QuizOptionId = correctOptionId,
            IsDeleted = false
        });

        var sut = CreateSut();

        var result = await sut.GetQuizResult(submissionId);

        Assert.NotNull(result);
        Assert.Equal(10m, result!.AssignedGrade);
        Assert.True(result.Passed);
        Assert.Equal(SubmissionStatus.Graded, result.Status);
    }

    [Fact]
    public async Task GetQuizResult_AllowsStudent_WhenModuleEnrollmentCompleted()
    {
        SeedStudentAndEnrollment();
        var enrollment = _db.ModuleEnrollments.Items.Single(me => me.Id == _enrollmentId);
        enrollment.Status = EnrollmentStatus.Completed;
        SeedQuizAssignment(maxPoints: 10m, passScore: 5m);

        var submissionId = Guid.NewGuid();
        var questionId = Guid.NewGuid();
        var correctOptionId = Guid.NewGuid();

        _db.Submissions.Seed(new Submission
        {
            Id = submissionId,
            Code = "SUB-RESULT02",
            AssignmentId = _assignmentId,
            StudentId = _studentId,
            ModuleEnrollmentId = _enrollmentId,
            AttemptNumber = 1,
            Status = SubmissionStatus.Graded,
            AssignedGrade = 10,
            StartedAt = DateTime.UtcNow.AddMinutes(-10),
            SubmittedAt = DateTime.UtcNow.AddMinutes(-1),
            IsDeleted = false
        });

        _db.QuizQuestions.Seed(new QuizQuestion
        {
            Id = questionId,
            AssignmentId = _assignmentId,
            SubmissionId = submissionId,
            QuestionText = "Q",
            QuestionType = QuestionTypeConstants.SingleChoice,
            Points = 1,
            OrderIndex = 1,
            AttemptNumber = 1,
            IsDeleted = false
        });

        _db.QuizOptions.Seed(new QuizOption
        {
            Id = correctOptionId,
            QuestionId = questionId,
            OptionText = "OK",
            IsCorrect = true,
            IsDeleted = false
        });

        _db.QuizAnswers.Seed(new QuizAnswer
        {
            Id = Guid.NewGuid(),
            SubmissionId = submissionId,
            QuizQuestionId = questionId,
            QuizOptionId = correctOptionId,
            IsDeleted = false
        });

        var sut = CreateSut();

        var result = await sut.GetQuizResult(submissionId);

        Assert.NotNull(result);
        Assert.Equal(10m, result!.AssignedGrade);
        Assert.True(result.Passed);
    }

    [Fact]
    public async Task GetQuizResult_ThrowsConflict_WhenSubmissionStillPending()
    {
        SeedStudentAndEnrollment();
        SeedQuizAssignment();

        var submissionId = Guid.NewGuid();
        _db.Submissions.Seed(new Submission
        {
            Id = submissionId,
            Code = "SUB-STILL001",
            AssignmentId = _assignmentId,
            StudentId = _studentId,
            ModuleEnrollmentId = _enrollmentId,
            AttemptNumber = 1,
            Status = SubmissionStatus.Pending,
            IsDeleted = false
        });

        var sut = CreateSut();

        await Assert.ThrowsAsync<ConflictException>(() => sut.GetQuizResult(submissionId));
    }

    // ── Additional coverage: StartQuiz unhappy / edge ─────────────────────────

    [Fact]
    public async Task StartQuiz_ThrowsBadRequest_WhenAssignmentIdEmpty()
    {
        SeedStudentAndEnrollment();
        var sut = CreateSut();

        var ex = await Assert.ThrowsAsync<BadRequestException>(() => sut.StartQuiz(Guid.Empty));
        Assert.Equal("AssignmentId is required.", ex.Message);
    }

    [Fact]
    public async Task StartQuiz_ThrowsBadRequest_WhenAssignmentIsNotQuiz()
    {
        SeedStudentAndEnrollment();
        var assignment = SeedQuizAssignment();
        assignment.AssignmentType = AssignmentType.FileUpload;
        var sut = CreateSut();

        var ex = await Assert.ThrowsAsync<BadRequestException>(() => sut.StartQuiz(_assignmentId));
        Assert.Equal("This assignment is not a quiz.", ex.Message);
    }

    [Fact]
    public async Task StartQuiz_ThrowsConflict_WhenAssignmentNoLongerAvailable()
    {
        SeedStudentAndEnrollment();
        var assignment = SeedQuizAssignment();
        assignment.AvailableUntil = DateTime.UtcNow.AddMinutes(-1);
        SeedBankQuestion();
        var sut = CreateSut();

        var ex = await Assert.ThrowsAsync<ConflictException>(() => sut.StartQuiz(_assignmentId));
        Assert.Equal("Assignment is no longer available.", ex.Message);
    }

    [Fact]
    public async Task StartQuiz_ThrowsForbidden_WhenNoActiveModuleEnrollment()
    {
        _db.Users.Seed(new User
        {
            Id = _studentId,
            Code = "STD-001",
            Email = "student@test.com",
            Role = RoleType.Student,
            IsDeleted = false
        });
        SeedQuizAssignment();
        SeedBankQuestion();
        var sut = CreateSut();

        var ex = await Assert.ThrowsAsync<ForbiddenException>(() => sut.StartQuiz(_assignmentId));
        Assert.Contains("active module enrollment", ex.Message);
    }

    [Fact]
    public async Task StartQuiz_ThrowsForbidden_WhenProgramEnrollmentClosed()
    {
        var programEnrollmentId = Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff");
        SeedStudentAndEnrollment();
        _db.ModuleEnrollments.Items[0].ProgramEnrollmentId = programEnrollmentId;
        _db.ProgramEnrollments.Seed(new ProgramEnrollment
        {
            Id = programEnrollmentId,
            StudentId = _studentId,
            ProgramId = _programId,
            Status = EnrollmentStatus.Failed,
            IsDeleted = false
        });
        SeedQuizAssignment();
        SeedBankQuestion();
        var sut = CreateSut();

        var ex = await Assert.ThrowsAsync<ForbiddenException>(() => sut.StartQuiz(_assignmentId));
        Assert.Contains("enrollment has ended", ex.Message);
    }

    [Fact]
    public async Task StartQuiz_ThrowsBadRequest_WhenQuestionBankEmpty()
    {
        SeedStudentAndEnrollment();
        SeedQuizAssignment();
        var sut = CreateSut();

        var ex = await Assert.ThrowsAsync<BadRequestException>(() => sut.StartQuiz(_assignmentId));
        Assert.Equal("Question bank has no questions.", ex.Message);
    }

    // ── GetQuiz unhappy ───────────────────────────────────────────────────────

    [Fact]
    public async Task GetQuiz_ThrowsNotFound_WhenSubmissionMissing()
    {
        SeedStudentAndEnrollment();
        var sut = CreateSut();

        await Assert.ThrowsAsync<NotFoundException>(() => sut.GetQuiz(Guid.NewGuid()));
    }

    [Fact]
    public async Task GetQuiz_ThrowsConflict_WhenSubmissionNotPending()
    {
        SeedStudentAndEnrollment();
        SeedQuizAssignment();
        var (submissionId, _, _, _) = SeedAttemptSnapshot(status: SubmissionStatus.Graded, assignedGrade: 10m);
        var sut = CreateSut();

        var ex = await Assert.ThrowsAsync<ConflictException>(() => sut.GetQuiz(submissionId));
        Assert.Equal("This submission is no longer in progress.", ex.Message);
    }

    // ── SaveDraftAnswers unhappy ──────────────────────────────────────────────

    [Fact]
    public async Task SaveDraftAnswers_ThrowsBadRequest_WhenRequestNull()
    {
        SeedStudentAndEnrollment();
        SeedQuizAssignment();
        var (submissionId, _, _, _) = SeedAttemptSnapshot();
        var sut = CreateSut();

        await Assert.ThrowsAsync<BadRequestException>(() =>
            sut.SaveDraftAnswers(submissionId, null!));
    }

    [Fact]
    public async Task SaveDraftAnswers_ThrowsBadRequest_WhenOptionDoesNotBelongToQuestion()
    {
        SeedStudentAndEnrollment();
        SeedQuizAssignment();
        var (submissionId, questionId, _, _) = SeedAttemptSnapshot();
        var sut = CreateSut();

        var ex = await Assert.ThrowsAsync<BadRequestException>(() =>
            sut.SaveDraftAnswers(submissionId, new SaveDraftAnswersRequestDto
            {
                Answers =
                [
                    new QuizAnswerItemDto
                    {
                        QuestionId = questionId,
                        SelectedOptionIds = [Guid.NewGuid()]
                    }
                ]
            }));

        Assert.Contains("does not belong to question", ex.Message);
    }

    [Fact]
    public async Task SaveDraftAnswers_ThrowsConflict_WhenSubmissionAlreadyGraded()
    {
        SeedStudentAndEnrollment();
        SeedQuizAssignment();
        var (submissionId, questionId, correctOptionId, _) =
            SeedAttemptSnapshot(status: SubmissionStatus.Graded, assignedGrade: 10m);
        var sut = CreateSut();

        await Assert.ThrowsAsync<ConflictException>(() =>
            sut.SaveDraftAnswers(submissionId, new SaveDraftAnswersRequestDto
            {
                Answers =
                [
                    new QuizAnswerItemDto
                    {
                        QuestionId = questionId,
                        SelectedOptionIds = [correctOptionId]
                    }
                ]
            }));
    }

    // ── SubmitQuiz additional ─────────────────────────────────────────────────

    [Fact]
    public async Task SubmitQuiz_MarksFailed_WhenAnswerIsWrong()
    {
        SeedStudentAndEnrollment();
        SeedQuizAssignment(maxPoints: 10m, passScore: 5m);
        var (submissionId, questionId, _, wrongOptionId) = SeedAttemptSnapshot();
        var sut = CreateSut();

        var result = await sut.SubmitQuiz(submissionId, new SubmitQuizAnswersRequestDto
        {
            Answers =
            [
                new QuizAnswerItemDto
                {
                    QuestionId = questionId,
                    SelectedOptionIds = [wrongOptionId]
                }
            ]
        });

        Assert.Equal(0m, result.AssignedGrade);
        Assert.False(result.Passed);
        Assert.Equal(0, result.CorrectCount);
        Assert.Equal(SubmissionStatus.Graded, result.Status);
    }

    [Fact]
    public async Task SubmitQuiz_UsesSavedDraft_WhenRequestOmitsAnswer()
    {
        SeedStudentAndEnrollment();
        SeedQuizAssignment(maxPoints: 10m, passScore: 5m);
        var (submissionId, questionId, correctOptionId, _) = SeedAttemptSnapshot();

        _db.QuizAnswers.Seed(new QuizAnswer
        {
            Id = Guid.NewGuid(),
            SubmissionId = submissionId,
            QuizQuestionId = questionId,
            QuizOptionId = correctOptionId,
            IsDeleted = false
        });

        var sut = CreateSut();

        // Empty request list → merge pulls the saved draft answer.
        var result = await sut.SubmitQuiz(submissionId, new SubmitQuizAnswersRequestDto
        {
            Answers = []
        });

        Assert.Equal(10m, result.AssignedGrade);
        Assert.True(result.Passed);
        Assert.Equal(1, result.CorrectCount);
    }

    [Fact]
    public async Task SubmitQuiz_ThrowsForbidden_WhenSubmissionBelongsToAnotherStudent()
    {
        SeedStudentAndEnrollment();
        SeedQuizAssignment();
        var (submissionId, questionId, correctOptionId, _) =
            SeedAttemptSnapshot(studentId: Guid.NewGuid());
        var sut = CreateSut();

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            sut.SubmitQuiz(submissionId, new SubmitQuizAnswersRequestDto
            {
                Answers =
                [
                    new QuizAnswerItemDto
                    {
                        QuestionId = questionId,
                        SelectedOptionIds = [correctOptionId]
                    }
                ]
            }));
    }

    // ── GetQuizResult additional ──────────────────────────────────────────────

    [Fact]
    public async Task GetQuizResult_ThrowsForbidden_WhenSubmissionBelongsToAnotherStudent()
    {
        SeedStudentAndEnrollment();
        SeedQuizAssignment();
        var (submissionId, questionId, correctOptionId, _) =
            SeedAttemptSnapshot(
                status: SubmissionStatus.Graded,
                studentId: Guid.NewGuid(),
                assignedGrade: 10m,
                submittedAt: DateTime.UtcNow);

        _db.QuizAnswers.Seed(new QuizAnswer
        {
            Id = Guid.NewGuid(),
            SubmissionId = submissionId,
            QuizQuestionId = questionId,
            QuizOptionId = correctOptionId,
            IsDeleted = false
        });

        var sut = CreateSut();

        await Assert.ThrowsAsync<ForbiddenException>(() => sut.GetQuizResult(submissionId));
    }

    [Fact]
    public async Task GetQuizResult_ReturnsNull_WhenAssignmentDeleted()
    {
        SeedStudentAndEnrollment();
        var assignment = SeedQuizAssignment(maxPoints: 10m, passScore: 5m);
        var (submissionId, questionId, correctOptionId, _) =
            SeedAttemptSnapshot(
                status: SubmissionStatus.Graded,
                assignedGrade: 10m,
                submittedAt: DateTime.UtcNow);

        _db.QuizAnswers.Seed(new QuizAnswer
        {
            Id = Guid.NewGuid(),
            SubmissionId = submissionId,
            QuizQuestionId = questionId,
            QuizOptionId = correctOptionId,
            IsDeleted = false
        });

        assignment.IsDeleted = true;
        var sut = CreateSut();

        var result = await sut.GetQuizResult(submissionId);

        Assert.Null(result);
    }

    [Fact]
    public async Task StartQuiz_CreatesAttempt_FromClassQuestionSet()
    {
        SeedStudentAndEnrollment();
        SeedQuizAssignment();

        var classId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var setId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var classQuestionId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
        var classOptionCorrectId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");
        var classOptionWrongId = Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee");
        var programEnrollmentId = Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff");

        var classEntity = new Class
        {
            Id = classId,
            Code = "CLS-Q",
            Name = "Quiz Cohort",
            ProgramId = _programId,
            Status = ClassStatus.Open,
            MaxCapacity = 30,
            StartDate = DateTime.UtcNow.AddDays(-7),
            EndDate = DateTime.UtcNow.AddDays(30),
            IsDeleted = false
        };
        _db.Classes.Seed(classEntity);
        _db.ProgramEnrollments.Seed(new ProgramEnrollment
        {
            Id = programEnrollmentId,
            StudentId = _studentId,
            ProgramId = _programId,
            Status = EnrollmentStatus.Active,
            IsDeleted = false
        });
        _db.ClassEnrollments.Seed(new ClassEnrollment
        {
            Id = Guid.NewGuid(),
            ClassId = classId,
            Class = classEntity,
            StudentId = _studentId,
            ProgramEnrollmentId = programEnrollmentId,
            Status = ClassEnrollmentStatus.Active,
            IsDeleted = false
        });
        _db.ClassQuizQuestionSets.Seed(new ClassQuizQuestionSet
        {
            Id = setId,
            ClassId = classId,
            AssignmentId = _assignmentId,
            PulledAt = DateTime.UtcNow,
            IsDeleted = false
        });

        var classQuestion = new ClassQuizQuestion
        {
            Id = classQuestionId,
            ClassQuizQuestionSetId = setId,
            QuestionText = "Class-set Q",
            QuestionType = QuestionTypeConstants.SingleChoice,
            Points = 1,
            OrderIndex = 1,
            DifficultyLevel = 1,
            IsDeleted = false,
            Options =
            [
                new ClassQuizQuestionOption
                {
                    Id = classOptionCorrectId,
                    ClassQuizQuestionId = classQuestionId,
                    OptionText = "A",
                    IsCorrect = true,
                    IsDeleted = false
                },
                new ClassQuizQuestionOption
                {
                    Id = classOptionWrongId,
                    ClassQuizQuestionId = classQuestionId,
                    OptionText = "B",
                    IsCorrect = false,
                    IsDeleted = false
                }
            ]
        };
        _db.ClassQuizQuestions.Seed(classQuestion);
        _db.ClassQuizQuestionOptions.Seed(classQuestion.Options.ToArray());

        var sut = CreateSut();
        var result = await sut.StartQuiz(_assignmentId);

        Assert.Equal(_assignmentId, result.AssignmentId);
        Assert.Single(result.Questions);
        Assert.Equal("Class-set Q", result.Questions[0].QuestionText);
        Assert.Equal(2, result.Questions[0].Options.Count);
        Assert.Single(_db.Submissions.Items);
        Assert.Single(_db.QuizQuestions.Items);
        Assert.Equal(2, _db.QuizOptions.Items.Count);
    }

    [Fact]
    public async Task SubmitQuiz_RecalculatesProgramProgress_AndCallsCertificate()
    {
        var programEnrollmentId = Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff");
        SeedStudentAndEnrollment();
        _db.ModuleEnrollments.Items[0].ProgramEnrollmentId = programEnrollmentId;
        _db.ProgramEnrollments.Seed(new ProgramEnrollment
        {
            Id = programEnrollmentId,
            StudentId = _studentId,
            ProgramId = _programId,
            Status = EnrollmentStatus.Active,
            IsDeleted = false
        });
        SeedQuizAssignment(maxPoints: 10m, passScore: 5m);
        var (submissionId, questionId, correctOptionId, _) = SeedAttemptSnapshot();
        var sut = CreateSut();

        var result = await sut.SubmitQuiz(submissionId, new SubmitQuizAnswersRequestDto
        {
            Answers =
            [
                new QuizAnswerItemDto
                {
                    QuestionId = questionId,
                    SelectedOptionIds = [correctOptionId]
                }
            ]
        });

        Assert.Equal(SubmissionStatus.Graded, result.Status);
        _certificateService.Verify(
            c => c.EnsureProgramCertificateInternalAsync(programEnrollmentId),
            Times.Once);
    }

    [Fact]
    public async Task SubmitQuiz_Continues_WhenCertificateThrows()
    {
        var programEnrollmentId = Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff");
        SeedStudentAndEnrollment();
        _db.ModuleEnrollments.Items[0].ProgramEnrollmentId = programEnrollmentId;
        _db.ProgramEnrollments.Seed(new ProgramEnrollment
        {
            Id = programEnrollmentId,
            StudentId = _studentId,
            ProgramId = _programId,
            Status = EnrollmentStatus.Active,
            IsDeleted = false
        });
        SeedQuizAssignment(maxPoints: 10m, passScore: 5m);
        var (submissionId, questionId, correctOptionId, _) = SeedAttemptSnapshot();
        var sut = CreateSut();
        _certificateService
            .Setup(c => c.EnsureProgramCertificateInternalAsync(It.IsAny<Guid>()))
            .ThrowsAsync(new InvalidOperationException("cert failed"));

        var result = await sut.SubmitQuiz(submissionId, new SubmitQuizAnswersRequestDto
        {
            Answers =
            [
                new QuizAnswerItemDto
                {
                    QuestionId = questionId,
                    SelectedOptionIds = [correctOptionId]
                }
            ]
        });

        Assert.Equal(SubmissionStatus.Graded, result.Status);
    }

    // ── Mentor / staff view access ────────────────────────────────────────────

    [Fact]
    public async Task GetQuizResult_AsMentor_ReturnsStudentResult()
    {
        var mentorId = Guid.Parse("aaaaaaaa-1111-1111-1111-111111111111");
        var classId = Guid.Parse("bbbbbbbb-1111-1111-1111-111111111111");
        SeedStudentAndEnrollment();
        SeedClassRosterForMentor(mentorId, classId);
        SeedQuizAssignment(maxPoints: 10m, passScore: 5m);
        var (submissionId, questionId, correctOptionId, _) =
            SeedAttemptSnapshot(
                status: SubmissionStatus.Graded,
                assignedGrade: 10m,
                submittedAt: DateTime.UtcNow);

        _db.QuizAnswers.Seed(new QuizAnswer
        {
            Id = Guid.NewGuid(),
            SubmissionId = submissionId,
            QuizQuestionId = questionId,
            QuizOptionId = correctOptionId,
            IsDeleted = false
        });

        var sut = CreateSut();
        _claimsService.Setup(c => c.GetCurrentUserId).Returns(mentorId);

        var result = await sut.GetQuizResult(submissionId);

        Assert.NotNull(result);
        Assert.Equal(_studentId, result!.StudentId);
        Assert.Equal(10m, result.AssignedGrade);
        Assert.True(result.Passed);
    }

    [Fact]
    public async Task GetQuiz_AsMentor_ReturnsPendingAttempt()
    {
        var mentorId = Guid.Parse("aaaaaaaa-2222-2222-2222-222222222222");
        var classId = Guid.Parse("bbbbbbbb-2222-2222-2222-222222222222");
        SeedStudentAndEnrollment();
        SeedClassRosterForMentor(mentorId, classId);
        SeedQuizAssignment();
        var (submissionId, _, _, _) = SeedAttemptSnapshot(status: SubmissionStatus.Pending);

        var sut = CreateSut();
        _claimsService.Setup(c => c.GetCurrentUserId).Returns(mentorId);

        var result = await sut.GetQuiz(submissionId);

        Assert.NotNull(result);
        Assert.Equal(_studentId, result!.StudentId);
        Assert.Equal(submissionId, result.SubmissionId);
    }

    [Fact]
    public async Task GetQuizResult_AsOtherMentor_ThrowsForbidden()
    {
        var mentorId = Guid.Parse("aaaaaaaa-3333-3333-3333-333333333333");
        var otherMentorId = Guid.Parse("cccccccc-3333-3333-3333-333333333333");
        var classId = Guid.Parse("bbbbbbbb-3333-3333-3333-333333333333");
        SeedStudentAndEnrollment();
        SeedClassRosterForMentor(mentorId, classId);
        SeedQuizAssignment(maxPoints: 10m, passScore: 5m);
        var (submissionId, _, _, _) =
            SeedAttemptSnapshot(
                status: SubmissionStatus.Graded,
                assignedGrade: 10m,
                submittedAt: DateTime.UtcNow);

        _db.Users.Seed(new User
        {
            Id = otherMentorId,
            Code = "MNT-OTHER",
            Role = RoleType.Mentor,
            IsDeleted = false
        });

        var sut = CreateSut();
        _claimsService.Setup(c => c.GetCurrentUserId).Returns(otherMentorId);

        await Assert.ThrowsAsync<ForbiddenException>(() => sut.GetQuizResult(submissionId));
    }

    private void SeedClassRosterForMentor(Guid mentorId, Guid classId)
    {
        _db.Users.Seed(new User
        {
            Id = mentorId,
            Code = $"MNT-{mentorId:N}"[..12],
            Role = RoleType.Mentor,
            FullName = "Class Mentor",
            IsDeleted = false
        });

        var classEntity = new Class
        {
            Id = classId,
            Code = "CLS-001",
            Name = "Cohort 1",
            ProgramId = _programId,
            MentorId = mentorId,
            StartDate = DateTime.UtcNow.AddDays(-7),
            EndDate = DateTime.UtcNow.AddDays(60),
            MaxCapacity = 30,
            Status = ClassStatus.InProgress,
            IsDeleted = false
        };
        _db.Classes.Seed(classEntity);

        var programEnrollmentId = Guid.NewGuid();
        _db.ProgramEnrollments.Seed(new ProgramEnrollment
        {
            Id = programEnrollmentId,
            StudentId = _studentId,
            ProgramId = _programId,
            Status = EnrollmentStatus.Active,
            IsDeleted = false
        });

        _db.ClassEnrollments.Seed(new ClassEnrollment
        {
            Id = Guid.NewGuid(),
            ClassId = classId,
            StudentId = _studentId,
            ProgramEnrollmentId = programEnrollmentId,
            Status = ClassEnrollmentStatus.Active,
            Class = classEntity,
            IsDeleted = false
        });
    }
}
