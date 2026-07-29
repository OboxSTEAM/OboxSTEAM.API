using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OboxSteam.Application.Commons;
using OboxSteam.Application.DTOs.ClassQuizQuestionSetDTO;
using OboxSteam.Application.Exceptions;
using OboxSteam.Application.Interfaces;
using OboxSteam.Application.Notifications;
using OboxSteam.Application.Services;
using OboxSteam.Application.Validation;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;
using OboxSteam.Test.Helpers;

namespace OboxSteam.Test.UnitTests;

public sealed class ClassQuizQuestionSetServiceTests
{
    private readonly Guid _mentorId = Guid.Parse("14141414-1414-1414-1414-141414141414");
    private readonly Guid _otherMentorId = Guid.Parse("15151515-1515-1515-1515-151515151515");
    private readonly Guid _managerId = Guid.Parse("13131313-1313-1313-1313-131313131313");
    private readonly Guid _studentId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private readonly Guid _superAdminId = Guid.Parse("12121212-1212-1212-1212-121212121212");
    private readonly Guid _programId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private readonly Guid _otherProgramId = Guid.Parse("23232323-2323-2323-2323-232323232323");
    private readonly Guid _moduleId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private readonly Guid _classId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private readonly Guid _assignmentId = Guid.Parse("55555555-5555-5555-5555-555555555555");
    private readonly Guid _questionBankId = Guid.Parse("66666666-6666-6666-6666-666666666666");
    private readonly Guid _setId = Guid.Parse("77777777-7777-7777-7777-777777777777");
    private readonly Guid _questionId = Guid.Parse("88888888-8888-8888-8888-888888888888");
    private readonly Guid _programEnrollmentId = Guid.Parse("99999999-9999-9999-9999-999999999999");

    private readonly DateTime _now = DateTime.UtcNow;

    private readonly InMemoryUnitOfWork _db = new();
    private readonly Mock<IClaimsService> _claimsService = new();
    private readonly Mock<INotificationPublisher> _notificationPublisher = new();

    private ClassQuizQuestionSetService CreateSut(Guid? currentUserId = null)
    {
        _claimsService.Setup(c => c.GetCurrentUserId).Returns(currentUserId ?? _mentorId);
        _notificationPublisher
            .Setup(n => n.PublishAsync(It.IsAny<NotificationCommand>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        return new ClassQuizQuestionSetService(
            _db,
            _claimsService.Object,
            _notificationPublisher.Object,
            NullLogger<ClassQuizQuestionSetService>.Instance);
    }

    private void SeedUser(Guid id, RoleType role, string code)
    {
        _db.Users.Seed(new User
        {
            Id = id,
            Code = code,
            Email = $"{code.ToLower()}@test.com",
            FullName = code,
            Role = role,
            Status = AccountStatus.Active,
            IsDeleted = false,
        });
    }

    private void SeedProgram(Guid? id = null)
    {
        var programId = id ?? _programId;
        _db.Programs.Seed(new Program
        {
            Id = programId,
            Code = programId == _otherProgramId ? "PRG-002" : "PRG-001",
            Name = "Robotics",
            Category = ProgramCategory.Technology,
            Level = DifficultyLevel.Beginner,
            IsDeleted = false,
        });
    }

    private void SeedModule(Guid? programId = null)
    {
        _db.Modules.Seed(new Module
        {
            Id = _moduleId,
            Code = "MOD-001",
            Name = "Module 1",
            ProgramId = programId ?? _programId,
            ModuleType = ModuleType.Theory,
            IsDeleted = false,
        });
    }

    private Class SeedClass(Guid? mentorId = null, Guid? programId = null)
    {
        var entity = new Class
        {
            Id = _classId,
            Code = "CLS-001",
            Name = "Cohort A",
            ProgramId = programId ?? _programId,
            MentorId = mentorId ?? _mentorId,
            Status = ClassStatus.Open,
            MaxCapacity = 20,
            StartDate = _now.AddDays(1),
            EndDate = _now.AddDays(30),
            IsDeleted = false,
        };
        _db.Classes.Seed(entity);
        return entity;
    }

    private Assignment SeedQuizAssignment(
        AssignmentType type = AssignmentType.Quiz,
        Guid? questionBankId = null,
        int questionCount = 2)
    {
        var assignment = new Assignment
        {
            Id = _assignmentId,
            Code = "ASN-QUIZ-001",
            ModuleId = _moduleId,
            Title = "Unit Quiz",
            AssignmentType = type,
            QuestionBankId = questionBankId ?? _questionBankId,
            QuestionCount = questionCount,
            MaxPoints = 10,
            PassScore = 5,
            MaxAttempts = 3,
            EasyPercent = 100,
            MediumPercent = 0,
            HardPercent = 0,
            IsDeleted = false,
        };
        _db.Assignments.Seed(assignment);
        return assignment;
    }

    private void SeedBankQuestions(int count = 2)
    {
        for (var i = 0; i < count; i++)
        {
            var questionId = Guid.Parse($"aaaaaaa{i}-aaaa-aaaa-aaaa-aaaaaaaaaaa{i}");
            _db.BankQuestions.Seed(new BankQuestion
            {
                Id = questionId,
                QuestionBankId = _questionBankId,
                QuestionText = $"Bank question {i + 1}",
                QuestionType = QuestionTypeConstants.SingleChoice,
                Points = 1,
                DifficultyLevel = 1,
                IsDeleted = false,
            });

            _db.BankQuestionOptions.Seed(
                new BankQuestionOption
                {
                    Id = Guid.Parse($"bbbbbbb{i}-bbbb-bbbb-bbbb-bbbbbbbbbbb{i}"),
                    BankQuestionId = questionId,
                    OptionText = "Correct",
                    IsCorrect = true,
                    IsDeleted = false,
                },
                new BankQuestionOption
                {
                    Id = Guid.Parse($"ccccccc{i}-cccc-cccc-cccc-ccccccccccc{i}"),
                    BankQuestionId = questionId,
                    OptionText = "Wrong",
                    IsCorrect = false,
                    IsDeleted = false,
                });
        }
    }

    private void SeedClassEnrollment()
    {
        _db.ClassEnrollments.Seed(new ClassEnrollment
        {
            Id = Guid.NewGuid(),
            ClassId = _classId,
            StudentId = _studentId,
            ProgramEnrollmentId = _programEnrollmentId,
            Status = ClassEnrollmentStatus.Active,
            IsDeleted = false,
        });
    }

    private void SeedSubmission()
    {
        _db.Submissions.Seed(new Submission
        {
            Id = Guid.NewGuid(),
            Code = "SUB-001",
            AssignmentId = _assignmentId,
            StudentId = _studentId,
            ModuleEnrollmentId = Guid.NewGuid(),
            AttemptNumber = 1,
            Status = SubmissionStatus.Pending,
            IsDeleted = false,
        });
    }

    private (ClassQuizQuestionSet Set, ClassQuizQuestion Question) SeedPulledSet(
        string questionText = "Pulled question",
        bool isDeleted = false)
    {
        var set = new ClassQuizQuestionSet
        {
            Id = _setId,
            ClassId = _classId,
            AssignmentId = _assignmentId,
            PulledAt = _now.AddHours(-1),
            IsDeleted = isDeleted,
        };
        _db.ClassQuizQuestionSets.Seed(set);

        var question = new ClassQuizQuestion
        {
            Id = _questionId,
            ClassQuizQuestionSetId = _setId,
            SourceBankQuestionId = Guid.NewGuid(),
            QuestionText = questionText,
            QuestionType = QuestionTypeConstants.SingleChoice,
            Points = 1,
            DifficultyLevel = 1,
            OrderIndex = 1,
            IsDeleted = false,
        };
        _db.ClassQuizQuestions.Seed(question);

        _db.ClassQuizQuestionOptions.Seed(
            new ClassQuizQuestionOption
            {
                Id = Guid.NewGuid(),
                ClassQuizQuestionId = _questionId,
                OptionText = "A",
                IsCorrect = true,
                IsDeleted = false,
            },
            new ClassQuizQuestionOption
            {
                Id = Guid.NewGuid(),
                ClassQuizQuestionId = _questionId,
                OptionText = "B",
                IsCorrect = false,
                IsDeleted = false,
            });

        return (set, question);
    }

    private void SeedBaseScenario()
    {
        SeedUser(_mentorId, RoleType.Mentor, "MNT-001");
        SeedProgram();
        SeedModule();
        SeedClass();
        SeedQuizAssignment();
        SeedBankQuestions();
    }

    // ── PullAsync ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task PullAsync_PullsQuestionsFromBank()
    {
        SeedBaseScenario();
        var sut = CreateSut();

        var result = await sut.PullAsync(_assignmentId, _classId);

        Assert.Equal(_classId, result.ClassId);
        Assert.Equal(_assignmentId, result.AssignmentId);
        Assert.False(result.IsLocked);
        Assert.Equal(2, result.Questions.Count);
        Assert.All(result.Questions, q =>
        {
            Assert.Equal(QuestionTypeConstants.SingleChoice, q.QuestionType);
            Assert.Equal(2, q.Options.Count);
        });
        Assert.Single(_db.ClassQuizQuestionSets.Items, s => !s.IsDeleted);
    }

    [Fact]
    public async Task PullAsync_ReplacesExistingSet()
    {
        SeedBaseScenario();
        SeedPulledSet("Old question");
        var sut = CreateSut();

        var result = await sut.PullAsync(_assignmentId, _classId);

        Assert.Equal(2, result.Questions.Count);
        Assert.True(_db.ClassQuizQuestionSets.Items.Single(s => s.Id == _setId).IsDeleted);
        Assert.True(_db.ClassQuizQuestions.Items.Single(q => q.Id == _questionId).IsDeleted);
        Assert.Equal(1, _db.ClassQuizQuestionSets.Items.Count(s => !s.IsDeleted));
    }

    [Fact]
    public async Task PullAsync_PublishesNotification()
    {
        SeedBaseScenario();
        var sut = CreateSut();

        await sut.PullAsync(_assignmentId, _classId);

        _notificationPublisher.Verify(
            n => n.PublishAsync(It.IsAny<NotificationCommand>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task PullAsync_Throws_WhenNotMentor()
    {
        SeedUser(_managerId, RoleType.Manager, "MGR-001");
        SeedBaseScenario();
        var sut = CreateSut(_managerId);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            sut.PullAsync(_assignmentId, _classId));
    }

    [Fact]
    public async Task PullAsync_Throws_WhenMentorDoesNotOwnClass()
    {
        SeedUser(_mentorId, RoleType.Mentor, "MNT-001");
        SeedUser(_otherMentorId, RoleType.Mentor, "MNT-002");
        SeedProgram();
        SeedModule();
        SeedClass(mentorId: _otherMentorId);
        SeedQuizAssignment();
        SeedBankQuestions();
        var sut = CreateSut();

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            sut.PullAsync(_assignmentId, _classId));
    }

    [Fact]
    public async Task PullAsync_Throws_WhenAssignmentNotQuiz()
    {
        SeedUser(_mentorId, RoleType.Mentor, "MNT-001");
        SeedProgram();
        SeedModule();
        SeedClass();
        SeedBankQuestions();
        SeedQuizAssignment(type: AssignmentType.FileUpload);
        var sut = CreateSut();

        await Assert.ThrowsAsync<BadRequestException>(() =>
            sut.PullAsync(_assignmentId, _classId));
    }

    [Fact]
    public async Task PullAsync_Throws_WhenAssignmentHasNoQuestionBank()
    {
        SeedUser(_mentorId, RoleType.Mentor, "MNT-001");
        SeedProgram();
        SeedModule();
        SeedClass();
        SeedBankQuestions();
        _db.Assignments.Seed(new Assignment
        {
            Id = _assignmentId,
            Code = "ASN-QUIZ-001",
            ModuleId = _moduleId,
            Title = "Unit Quiz",
            AssignmentType = AssignmentType.Quiz,
            QuestionBankId = null,
            QuestionCount = 2,
            MaxPoints = 10,
            PassScore = 5,
            EasyPercent = 100,
            MediumPercent = 0,
            HardPercent = 0,
            IsDeleted = false,
        });
        var sut = CreateSut();

        await Assert.ThrowsAsync<BadRequestException>(() =>
            sut.PullAsync(_assignmentId, _classId));
    }

    [Fact]
    public async Task PullAsync_Throws_WhenProgramMismatch()
    {
        SeedUser(_mentorId, RoleType.Mentor, "MNT-001");
        SeedProgram();
        SeedProgram(_otherProgramId);
        SeedModule();
        SeedClass(programId: _otherProgramId);
        SeedQuizAssignment();
        SeedBankQuestions();
        var sut = CreateSut();

        var ex = await Assert.ThrowsAsync<BadRequestException>(() =>
            sut.PullAsync(_assignmentId, _classId));

        Assert.Equal(MentorScopeValidator.ClassProgramMismatchMessage, ex.Message);
    }

    [Fact]
    public async Task PullAsync_Throws_WhenLockedDueToSubmission()
    {
        SeedUser(_studentId, RoleType.Student, "STD-001");
        SeedBaseScenario();
        SeedClassEnrollment();
        SeedSubmission();
        var sut = CreateSut();

        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            sut.PullAsync(_assignmentId, _classId));

        Assert.Contains("Ask a Manager to update the question bank instead.", ex.Message);
    }

    [Fact]
    public async Task PullAsync_Throws_WhenUnauthorized()
    {
        var sut = CreateSut(Guid.Empty);

        await Assert.ThrowsAsync<UnauthorizedException>(() =>
            sut.PullAsync(_assignmentId, _classId));
    }

    // ── GetAsync ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAsync_ReturnsSet_ForMentor()
    {
        SeedUser(_mentorId, RoleType.Mentor, "MNT-001");
        SeedProgram();
        SeedClass();
        SeedPulledSet();
        var sut = CreateSut();

        var result = await sut.GetAsync(_assignmentId, _classId);

        Assert.Equal(_setId, result.Id);
        Assert.Single(result.Questions);
        Assert.Equal("Pulled question", result.Questions[0].QuestionText);
    }

    [Fact]
    public async Task GetAsync_ReturnsSet_ForManager()
    {
        SeedUser(_managerId, RoleType.Manager, "MGR-001");
        SeedProgram();
        SeedClass();
        SeedPulledSet();
        var sut = CreateSut(_managerId);

        var result = await sut.GetAsync(_assignmentId, _classId);

        Assert.Equal(_setId, result.Id);
    }

    [Fact]
    public async Task GetAsync_ReturnsSet_ForSuperAdmin()
    {
        SeedUser(_superAdminId, RoleType.SuperAdmin, "SA-001");
        SeedProgram();
        SeedClass();
        SeedPulledSet();
        var sut = CreateSut(_superAdminId);

        var result = await sut.GetAsync(_assignmentId, _classId);

        Assert.Equal(_setId, result.Id);
    }

    [Fact]
    public async Task GetAsync_Throws_Forbidden_ForStudent()
    {
        SeedUser(_studentId, RoleType.Student, "STD-001");
        SeedProgram();
        SeedClass();
        SeedPulledSet();
        var sut = CreateSut(_studentId);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            sut.GetAsync(_assignmentId, _classId));
    }

    [Fact]
    public async Task GetAsync_Throws_WhenSetNotFound()
    {
        SeedUser(_mentorId, RoleType.Mentor, "MNT-001");
        SeedProgram();
        SeedClass();
        var sut = CreateSut();

        await Assert.ThrowsAsync<NotFoundException>(() =>
            sut.GetAsync(_assignmentId, _classId));
    }

    [Fact]
    public async Task GetAsync_ReturnsIsLocked_WhenSubmissionExists()
    {
        SeedUser(_mentorId, RoleType.Mentor, "MNT-001");
        SeedUser(_studentId, RoleType.Student, "STD-001");
        SeedProgram();
        SeedClass();
        SeedPulledSet();
        SeedClassEnrollment();
        SeedSubmission();
        var sut = CreateSut();

        var result = await sut.GetAsync(_assignmentId, _classId);

        Assert.True(result.IsLocked);
    }

    // ── UpdateQuestionAsync ───────────────────────────────────────────────────

    [Fact]
    public async Task UpdateQuestion_UpdatesFieldsAndOptions()
    {
        SeedBaseScenario();
        SeedPulledSet();
        var sut = CreateSut();

        var result = await sut.UpdateQuestionAsync(
            _assignmentId,
            _classId,
            _questionId,
            new UpdateClassQuizQuestionRequestDto
            {
                QuestionText = "  Updated text  ",
                Points = 2,
                DifficultyLevel = 2,
                OrderIndex = 3,
                Options =
                [
                    new UpdateClassQuizQuestionOptionRequestDto { OptionText = "New A", IsCorrect = true },
                    new UpdateClassQuizQuestionOptionRequestDto { OptionText = "New B", IsCorrect = false },
                ],
            });

        Assert.Equal("Updated text", result.QuestionText);
        Assert.Equal(2, result.Points);
        Assert.Equal(2, result.DifficultyLevel);
        Assert.Equal(3, result.OrderIndex);
        Assert.Equal(2, result.Options.Count);
        Assert.Contains(result.Options, o => o.OptionText == "New A" && o.IsCorrect);
        _notificationPublisher.Verify(
            n => n.PublishAsync(It.IsAny<NotificationCommand>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task UpdateQuestion_Throws_ValidationErrors()
    {
        SeedBaseScenario();
        SeedPulledSet();
        var sut = CreateSut();

        await Assert.ThrowsAsync<BadRequestException>(() =>
            sut.UpdateQuestionAsync(_assignmentId, _classId, _questionId,
                new UpdateClassQuizQuestionRequestDto { QuestionText = "   " }));

        await Assert.ThrowsAsync<BadRequestException>(() =>
            sut.UpdateQuestionAsync(_assignmentId, _classId, _questionId,
                new UpdateClassQuizQuestionRequestDto { QuestionType = "Invalid" }));

        await Assert.ThrowsAsync<BadRequestException>(() =>
            sut.UpdateQuestionAsync(_assignmentId, _classId, _questionId,
                new UpdateClassQuizQuestionRequestDto { Points = 0 }));

        await Assert.ThrowsAsync<BadRequestException>(() =>
            sut.UpdateQuestionAsync(_assignmentId, _classId, _questionId,
                new UpdateClassQuizQuestionRequestDto { DifficultyLevel = 6 }));

        await Assert.ThrowsAsync<BadRequestException>(() =>
            sut.UpdateQuestionAsync(_assignmentId, _classId, _questionId,
                new UpdateClassQuizQuestionRequestDto
                {
                    Options =
                    [
                        new UpdateClassQuizQuestionOptionRequestDto { OptionText = "Only one", IsCorrect = true },
                    ],
                }));
    }

    [Fact]
    public async Task UpdateQuestion_Throws_WhenLocked()
    {
        SeedUser(_studentId, RoleType.Student, "STD-001");
        SeedBaseScenario();
        SeedPulledSet();
        SeedClassEnrollment();
        SeedSubmission();
        var sut = CreateSut();

        await Assert.ThrowsAsync<ConflictException>(() =>
            sut.UpdateQuestionAsync(
                _assignmentId,
                _classId,
                _questionId,
                new UpdateClassQuizQuestionRequestDto { QuestionText = "Blocked" }));
    }

    [Fact]
    public async Task UpdateQuestion_Throws_WhenSetNotFound()
    {
        SeedBaseScenario();
        var sut = CreateSut();

        await Assert.ThrowsAsync<NotFoundException>(() =>
            sut.UpdateQuestionAsync(
                _assignmentId,
                _classId,
                _questionId,
                new UpdateClassQuizQuestionRequestDto { QuestionText = "Missing set" }));
    }

    [Fact]
    public async Task UpdateQuestion_Throws_WhenQuestionNotFound()
    {
        SeedBaseScenario();
        SeedPulledSet();
        var sut = CreateSut();
        var missingQuestionId = Guid.Parse("abababab-abab-abab-abab-abababababab");

        await Assert.ThrowsAsync<NotFoundException>(() =>
            sut.UpdateQuestionAsync(
                _assignmentId,
                _classId,
                missingQuestionId,
                new UpdateClassQuizQuestionRequestDto { QuestionText = "Missing question" }));
    }
}
