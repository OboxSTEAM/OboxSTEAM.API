using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OboxSteam.Application.DTOs.AssignmentDTO;
using OboxSteam.Application.Exceptions;
using OboxSteam.Application.Interfaces;
using OboxSteam.Application.Notifications;
using OboxSteam.Application.Services;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;
using OboxSteam.Test.Helpers;

namespace OboxSteam.Test.UnitTests;

public sealed class AssignmentServiceTests
{
    private readonly Guid _managerId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private readonly Guid _mentorId = Guid.Parse("12121212-1212-1212-1212-121212121212");
    private readonly Guid _studentId = Guid.Parse("13131313-1313-1313-1313-131313131313");
    private readonly Guid _moduleId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private readonly Guid _programId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private readonly Guid _courseId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private readonly Guid _questionBankId = Guid.Parse("55555555-5555-5555-5555-555555555555");
    private readonly Guid _assignmentId = Guid.Parse("66666666-6666-6666-6666-666666666666");
    private readonly Guid _classId = Guid.Parse("77777777-7777-7777-7777-777777777777");

    private readonly InMemoryUnitOfWork _db = new();
    private readonly Mock<IClaimsService> _claimsService = new();
    private readonly Mock<INotificationPublisher> _notificationPublisher = new();

    private AssignmentService CreateSut(Guid? currentUserId = null)
    {
        _claimsService.Setup(c => c.GetCurrentUserId).Returns(currentUserId ?? _managerId);
        _notificationPublisher
            .Setup(n => n.PublishAsync(It.IsAny<NotificationCommand>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _notificationPublisher
            .Setup(n => n.PublishManyAsync(
                It.IsAny<IReadOnlyList<NotificationCommand>>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        return new AssignmentService(
            _claimsService.Object,
            _db,
            NullLogger<AssignmentService>.Instance,
            _notificationPublisher.Object);
    }

    private void SeedModule(Guid? moduleId = null)
    {
        _db.Modules.Seed(new Module
        {
            Id = moduleId ?? _moduleId,
            Code = "MOD-001",
            Name = "Module 1",
            ProgramId = _programId,
            ModuleType = ModuleType.Theory,
            IsDeleted = false
        });
    }

    private void SeedCourse(Guid? courseId = null, Guid? moduleId = null)
    {
        _db.Courses.Seed(new Course
        {
            Id = courseId ?? _courseId,
            Code = "CRS-001",
            Name = "Course 1",
            ModuleId = moduleId ?? _moduleId,
            IsDeleted = false
        });
    }

    private void SeedManager()
    {
        _db.Users.Seed(new User
        {
            Id = _managerId,
            Code = "MGR-001",
            Email = "manager@test.com",
            Role = RoleType.Manager,
            IsDeleted = false
        });
    }

    private void SeedMentorWithClass()
    {
        _db.Users.Seed(new User
        {
            Id = _mentorId,
            Code = "MNT-001",
            Email = "mentor@test.com",
            Role = RoleType.Mentor,
            IsDeleted = false
        });

        _db.Classes.Seed(new Class
        {
            Id = _classId,
            Code = "CLS-001",
            Name = "Cohort A",
            ProgramId = _programId,
            MentorId = _mentorId,
            Status = ClassStatus.Open,
            StartDate = DateTime.UtcNow.AddDays(-7),
            EndDate = DateTime.UtcNow.AddDays(60),
            MaxCapacity = 30,
            IsDeleted = false
        });
    }

    private Assignment SeedAssignment(
        AssignmentType type = AssignmentType.FileUpload,
        string code = "ASN-001",
        Guid? questionBankId = null,
        bool isDeleted = false)
    {
        var assignment = new Assignment
        {
            Id = _assignmentId,
            Code = code,
            ModuleId = _moduleId,
            CourseId = _courseId,
            Title = "Existing Assignment",
            Description = "Desc",
            AssignmentType = type,
            MaxPoints = 10,
            PassScore = 5,
            MaxAttempts = 2,
            QuestionBankId = questionBankId,
            EasyPercent = type == AssignmentType.Quiz ? 100 : 0,
            MediumPercent = 0,
            HardPercent = 0,
            IsRequiredForModulePass = true,
            AllowShuffle = true,
            ShuffleOptions = true,
            IsDeleted = isDeleted,
            CreatedAt = DateTime.UtcNow.AddDays(-1)
        };

        _db.Assignments.Seed(assignment);
        return assignment;
    }

    private CreateAssignmentRequestDto BuildCreateRequest(
        AssignmentType type = AssignmentType.FileUpload,
        string code = "ASN-NEW",
        Guid? courseId = null,
        Guid? questionBankId = null,
        int? questionCount = null,
        int easy = 0,
        int medium = 0,
        int hard = 0)
        => new()
        {
            Code = code,
            ModuleId = _moduleId,
            CourseId = courseId,
            Title = "New Assignment",
            Description = "  Details  ",
            AssignmentType = type,
            MaxPoints = 10,
            PassScore = 6,
            MaxAttempts = 2,
            TimeLimitMinutes = type == AssignmentType.Quiz ? 30 : null,
            QuestionBankId = questionBankId,
            QuestionCount = questionCount,
            EasyPercent = easy,
            MediumPercent = medium,
            HardPercent = hard,
            AllowShuffle = true,
            ShuffleOptions = true,
            IsRequiredForModulePass = true
        };

    /// <summary>
    /// Seeds Program + Module with navigations wired for LINQ-to-objects GetAll queries.
    /// </summary>
    private Module SeedCatalogModule(
        Guid? moduleId = null,
        Guid? programId = null,
        string moduleName = "Module 1",
        string programName = "Program Alpha")
    {
        var resolvedProgramId = programId ?? _programId;
        var program = _db.Programs.Items.FirstOrDefault(p => p.Id == resolvedProgramId);
        if (program == null)
        {
            program = new Program
            {
                Id = resolvedProgramId,
                Code = "PRG-001",
                Name = programName,
                Category = ProgramCategory.Technology,
                Level = DifficultyLevel.Beginner,
                IsDeleted = false
            };
            _db.Programs.Seed(program);
        }

        var module = new Module
        {
            Id = moduleId ?? _moduleId,
            Code = "MOD-001",
            Name = moduleName,
            ProgramId = resolvedProgramId,
            Program = program,
            ModuleType = ModuleType.Theory,
            IsDeleted = false
        };
        _db.Modules.Seed(module);
        return module;
    }

    private Assignment SeedCatalogAssignment(
        Module module,
        Guid? id = null,
        string code = "ASN-001",
        string title = "Existing Assignment",
        AssignmentType type = AssignmentType.FileUpload,
        Guid? courseId = null,
        DateTime? createdAt = null,
        DateTime? dueDate = null,
        bool isDeleted = false)
    {
        var assignment = new Assignment
        {
            Id = id ?? Guid.NewGuid(),
            Code = code,
            ModuleId = module.Id,
            Module = module,
            CourseId = courseId,
            Title = title,
            AssignmentType = type,
            MaxPoints = 10,
            PassScore = 5,
            MaxAttempts = 2,
            DueDate = dueDate,
            IsRequiredForModulePass = true,
            AllowShuffle = true,
            ShuffleOptions = true,
            IsDeleted = isDeleted,
            CreatedAt = createdAt ?? DateTime.UtcNow.AddDays(-1)
        };
        _db.Assignments.Seed(assignment);
        return assignment;
    }

    // ── GetAllAssignments ─────────────────────────────────────────────────────

    [Fact]
    public async Task GetAllAssignments_ReturnsPaginatedItems_WithModuleProgramContext()
    {
        var module = SeedCatalogModule();
        SeedCatalogAssignment(module, id: _assignmentId, code: "ASN-001", title: "Catalog A");
        var sut = CreateSut();

        var result = await sut.GetAllAssignments(
            search: null,
            sortBy: null,
            isDescending: true,
            page: 1,
            pageSize: 10);

        Assert.Equal(1, result.TotalCount);
        Assert.Equal(1, result.CurrentPage);
        Assert.Equal(10, result.PageSize);
        Assert.Single(result.Items);
        var item = result.Items[0];
        Assert.Equal(_assignmentId, item.Id);
        Assert.Equal("ASN-001", item.Code);
        Assert.Equal("Catalog A", item.Title);
        Assert.Equal(_moduleId, item.ModuleId);
        Assert.Equal("Module 1", item.ModuleName);
        Assert.Equal(_programId, item.ProgramId);
        Assert.Equal("Program Alpha", item.ProgramName);
    }

    [Fact]
    public async Task GetAllAssignments_ExcludesSoftDeleted()
    {
        var module = SeedCatalogModule();
        SeedCatalogAssignment(module, code: "ASN-LIVE", title: "Live");
        SeedCatalogAssignment(module, code: "ASN-GONE", title: "Gone", isDeleted: true);
        var sut = CreateSut();

        var result = await sut.GetAllAssignments(null, null, true, 1, 10);

        Assert.Equal(1, result.TotalCount);
        Assert.Equal("ASN-LIVE", result.Items[0].Code);
    }

    [Fact]
    public async Task GetAllAssignments_FiltersByModuleProgramCourseAndType()
    {
        var moduleA = SeedCatalogModule();
        var otherProgramId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var moduleB = SeedCatalogModule(
            moduleId: Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
            programId: otherProgramId,
            moduleName: "Other Module",
            programName: "Other Program");

        SeedCatalogAssignment(
            moduleA,
            code: "ASN-QUIZ",
            type: AssignmentType.Quiz,
            courseId: _courseId);
        SeedCatalogAssignment(
            moduleA,
            code: "ASN-FILE",
            type: AssignmentType.FileUpload);
        SeedCatalogAssignment(
            moduleB,
            code: "ASN-OTHER",
            type: AssignmentType.FileUpload);
        var sut = CreateSut();

        var byModule = await sut.GetAllAssignments(null, null, true, 1, 10, moduleId: _moduleId);
        Assert.Equal(2, byModule.TotalCount);

        var byProgram = await sut.GetAllAssignments(null, null, true, 1, 10, programId: otherProgramId);
        Assert.Single(byProgram.Items);
        Assert.Equal("ASN-OTHER", byProgram.Items[0].Code);

        var byCourse = await sut.GetAllAssignments(null, null, true, 1, 10, courseId: _courseId);
        Assert.Single(byCourse.Items);
        Assert.Equal("ASN-QUIZ", byCourse.Items[0].Code);

        var byType = await sut.GetAllAssignments(
            null, null, true, 1, 10, assignmentType: AssignmentType.Quiz);
        Assert.Single(byType.Items);
        Assert.Equal("ASN-QUIZ", byType.Items[0].Code);
    }

    [Fact]
    public async Task GetAllAssignments_PaginatesResults()
    {
        var module = SeedCatalogModule();
        for (var i = 1; i <= 5; i++)
        {
            SeedCatalogAssignment(
                module,
                code: $"ASN-{i:D3}",
                title: $"Assignment {i}",
                createdAt: DateTime.UtcNow.AddDays(-i));
        }

        var sut = CreateSut();
        var page1 = await sut.GetAllAssignments(null, "createdAt", true, page: 1, pageSize: 2);
        var page2 = await sut.GetAllAssignments(null, "createdAt", true, page: 2, pageSize: 2);
        var page3 = await sut.GetAllAssignments(null, "createdAt", true, page: 3, pageSize: 2);

        Assert.Equal(5, page1.TotalCount);
        Assert.Equal(3, page1.TotalPages);
        Assert.Equal(2, page1.Items.Count);
        Assert.Equal(2, page2.Items.Count);
        Assert.Single(page3.Items);
        Assert.Equal("ASN-001", page1.Items[0].Code);
        Assert.Equal("ASN-003", page2.Items[0].Code);
        Assert.Equal("ASN-005", page3.Items[0].Code);
    }

    // ── CreateAssignment happy ────────────────────────────────────────────────

    [Fact]
    public async Task CreateAssignment_CreatesFileUploadAssignment()
    {
        SeedModule();
        var sut = CreateSut();

        var result = await sut.CreateAssignment(BuildCreateRequest());

        Assert.Equal("ASN-NEW", result.Code);
        Assert.Equal("New Assignment", result.Title);
        Assert.Equal("Details", result.Description);
        Assert.Equal(AssignmentType.FileUpload, result.AssignmentType);
        Assert.Equal(10, result.MaxPoints);
        Assert.Equal(6m, result.PassScore);
        Assert.Single(_db.Assignments.Items);
        Assert.Equal(1, _db.SaveChangesCallCount);
        _notificationPublisher.Verify(
            n => n.PublishManyAsync(
                It.IsAny<IReadOnlyList<NotificationCommand>>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task CreateAssignment_PublishesNotification_WhenOpenClassExists()
    {
        SeedModule();
        _db.Classes.Seed(new Class
        {
            Id = _classId,
            Code = "CLS-OPEN",
            Name = "Open Class",
            ProgramId = _programId,
            Status = ClassStatus.Open,
            StartDate = DateTime.UtcNow,
            EndDate = DateTime.UtcNow.AddMonths(1),
            MaxCapacity = 20,
            IsDeleted = false
        });
        var sut = CreateSut();

        await sut.CreateAssignment(BuildCreateRequest());

        _notificationPublisher.Verify(
            n => n.PublishManyAsync(
                It.Is<IReadOnlyList<NotificationCommand>>(cmds => cmds.Count == 1),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CreateAssignment_CreatesQuiz_WithValidQuestionBank()
    {
        SeedModule();
        SeedCourse();
        _db.QuestionBanks.Seed(new QuestionBank
        {
            Id = _questionBankId,
            CourseId = _courseId,
            IsDeleted = false
        });
        _db.BankQuestions.Seed(
            new BankQuestion
            {
                Id = Guid.NewGuid(),
                QuestionBankId = _questionBankId,
                QuestionText = "Q1",
                QuestionType = "SingleChoice",
                IsDeleted = false
            },
            new BankQuestion
            {
                Id = Guid.NewGuid(),
                QuestionBankId = _questionBankId,
                QuestionText = "Q2",
                QuestionType = "SingleChoice",
                IsDeleted = false
            });

        var sut = CreateSut();
        var result = await sut.CreateAssignment(BuildCreateRequest(
            type: AssignmentType.Quiz,
            courseId: _courseId,
            questionBankId: _questionBankId,
            questionCount: 1,
            easy: 100,
            medium: 0,
            hard: 0));

        Assert.Equal(AssignmentType.Quiz, result.AssignmentType);
        Assert.Equal(_questionBankId, result.QuestionBankId);
        Assert.Equal(1, result.QuestionCount);
        Assert.Equal(30, result.TimeLimitMinutes);
    }

    // ── CreateAssignment unhappy ──────────────────────────────────────────────

    [Fact]
    public async Task CreateAssignment_ThrowsBadRequest_WhenCodeMissing()
    {
        SeedModule();
        var sut = CreateSut();
        var request = BuildCreateRequest();
        request.Code = "  ";

        var ex = await Assert.ThrowsAsync<BadRequestException>(() => sut.CreateAssignment(request));
        Assert.Equal("Code is required.", ex.Message);
    }

    [Fact]
    public async Task CreateAssignment_ThrowsBadRequest_WhenTitleMissing()
    {
        SeedModule();
        var sut = CreateSut();
        var request = BuildCreateRequest();
        request.Title = "";

        await Assert.ThrowsAsync<BadRequestException>(() => sut.CreateAssignment(request));
    }

    [Fact]
    public async Task CreateAssignment_ThrowsBadRequest_WhenMaxPointsInvalid()
    {
        SeedModule();
        var sut = CreateSut();
        var request = BuildCreateRequest();
        request.MaxPoints = 0;

        var ex = await Assert.ThrowsAsync<BadRequestException>(() => sut.CreateAssignment(request));
        Assert.Equal("MaxPoints must be greater than 0.", ex.Message);
    }

    [Fact]
    public async Task CreateAssignment_ThrowsBadRequest_WhenPassScoreExceedsMaxPoints()
    {
        SeedModule();
        var sut = CreateSut();
        var request = BuildCreateRequest();
        request.PassScore = 20;

        var ex = await Assert.ThrowsAsync<BadRequestException>(() => sut.CreateAssignment(request));
        Assert.Equal("PassScore cannot exceed MaxPoints.", ex.Message);
    }

    [Fact]
    public async Task CreateAssignment_ThrowsBadRequest_WhenMaxAttemptsInvalid()
    {
        SeedModule();
        var sut = CreateSut();
        var request = BuildCreateRequest();
        request.MaxAttempts = 0;

        await Assert.ThrowsAsync<BadRequestException>(() => sut.CreateAssignment(request));
    }

    [Fact]
    public async Task CreateAssignment_ThrowsBadRequest_WhenTimeLimitInvalid()
    {
        SeedModule();
        var sut = CreateSut();
        var request = BuildCreateRequest();
        request.TimeLimitMinutes = 0;

        await Assert.ThrowsAsync<BadRequestException>(() => sut.CreateAssignment(request));
    }

    [Fact]
    public async Task CreateAssignment_ThrowsNotFound_WhenModuleMissing()
    {
        var sut = CreateSut();

        await Assert.ThrowsAsync<NotFoundException>(() => sut.CreateAssignment(BuildCreateRequest()));
    }

    [Fact]
    public async Task CreateAssignment_ThrowsNotFound_WhenCourseMissing()
    {
        SeedModule();
        var sut = CreateSut();

        await Assert.ThrowsAsync<NotFoundException>(() =>
            sut.CreateAssignment(BuildCreateRequest(courseId: _courseId)));
    }

    [Fact]
    public async Task CreateAssignment_ThrowsBadRequest_WhenCourseBelongsToOtherModule()
    {
        SeedModule();
        var otherModuleId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        _db.Modules.Seed(new Module
        {
            Id = otherModuleId,
            Code = "MOD-OTHER",
            Name = "Other",
            ProgramId = _programId,
            ModuleType = ModuleType.Theory,
            IsDeleted = false
        });
        SeedCourse(moduleId: otherModuleId);
        var sut = CreateSut();

        var ex = await Assert.ThrowsAsync<BadRequestException>(() =>
            sut.CreateAssignment(BuildCreateRequest(courseId: _courseId)));
        Assert.Equal("Course does not belong to the specified module.", ex.Message);
    }

    [Fact]
    public async Task CreateAssignment_ThrowsConflict_WhenCodeDuplicate()
    {
        SeedModule();
        SeedAssignment(code: "ASN-DUP");
        var sut = CreateSut();

        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            sut.CreateAssignment(BuildCreateRequest(code: "asn-dup")));
        Assert.Contains("already exists", ex.Message);
    }

    [Fact]
    public async Task CreateAssignment_ThrowsBadRequest_WhenNonQuizHasQuestionBank()
    {
        SeedModule();
        var sut = CreateSut();

        var ex = await Assert.ThrowsAsync<BadRequestException>(() =>
            sut.CreateAssignment(BuildCreateRequest(questionBankId: _questionBankId)));
        Assert.Equal("Question bank can only be linked to quiz assignments.", ex.Message);
    }

    [Fact]
    public async Task CreateAssignment_ThrowsNotFound_WhenQuestionBankMissing()
    {
        SeedModule();
        SeedCourse();
        var sut = CreateSut();

        await Assert.ThrowsAsync<NotFoundException>(() =>
            sut.CreateAssignment(BuildCreateRequest(
                type: AssignmentType.Quiz,
                courseId: _courseId,
                questionBankId: _questionBankId,
                easy: 100)));
    }

    [Fact]
    public async Task CreateAssignment_ThrowsBadRequest_WhenDifficultyPercentsInvalid()
    {
        SeedModule();
        SeedCourse();
        _db.QuestionBanks.Seed(new QuestionBank
        {
            Id = _questionBankId,
            CourseId = _courseId,
            IsDeleted = false
        });
        var sut = CreateSut();

        var ex = await Assert.ThrowsAsync<BadRequestException>(() =>
            sut.CreateAssignment(BuildCreateRequest(
                type: AssignmentType.Quiz,
                courseId: _courseId,
                questionBankId: _questionBankId,
                easy: 50,
                medium: 50,
                hard: 50)));

        Assert.Contains("must sum to 100", ex.Message);
    }

    [Fact]
    public async Task CreateAssignment_ThrowsBadRequest_WhenQuestionCountExceedsBank()
    {
        SeedModule();
        SeedCourse();
        _db.QuestionBanks.Seed(new QuestionBank
        {
            Id = _questionBankId,
            CourseId = _courseId,
            IsDeleted = false
        });
        _db.BankQuestions.Seed(new BankQuestion
        {
            Id = Guid.NewGuid(),
            QuestionBankId = _questionBankId,
            QuestionText = "Only one",
            QuestionType = "SingleChoice",
            IsDeleted = false
        });
        var sut = CreateSut();

        var ex = await Assert.ThrowsAsync<BadRequestException>(() =>
            sut.CreateAssignment(BuildCreateRequest(
                type: AssignmentType.Quiz,
                courseId: _courseId,
                questionBankId: _questionBankId,
                questionCount: 5,
                easy: 100)));

        Assert.Contains("exceeds the number of questions", ex.Message);
    }

    // ── GetAssignmentById ─────────────────────────────────────────────────────

    [Fact]
    public async Task GetAssignmentById_ReturnsDto()
    {
        SeedModule();
        SeedAssignment();
        var sut = CreateSut();

        var result = await sut.GetAssignmentById(_assignmentId);

        Assert.NotNull(result);
        Assert.Equal(_assignmentId, result!.Id);
        Assert.Equal("ASN-001", result.Code);
        Assert.Equal("Existing Assignment", result.Title);
    }

    [Fact]
    public async Task GetAssignmentById_ReturnsNull_WhenMissing()
    {
        var sut = CreateSut();

        var result = await sut.GetAssignmentById(Guid.NewGuid());

        Assert.Null(result);
    }

    [Fact]
    public async Task GetAssignmentById_ReturnsNull_WhenDeleted()
    {
        SeedModule();
        SeedAssignment(isDeleted: true);
        var sut = CreateSut();

        var result = await sut.GetAssignmentById(_assignmentId);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetAssignmentById_ThrowsForbidden_WhenStudentNotEnrolled()
    {
        SeedModule();
        SeedAssignment();
        _db.Users.Seed(new User
        {
            Id = _studentId,
            Code = "STD-001",
            Email = "student@test.com",
            Role = RoleType.Student,
            IsDeleted = false
        });
        var sut = CreateSut(currentUserId: _studentId);

        await Assert.ThrowsAsync<ForbiddenException>(() => sut.GetAssignmentById(_assignmentId));
    }

    [Fact]
    public async Task GetAssignmentById_AllowsStudent_WhenActivelyEnrolled()
    {
        SeedModule();
        SeedAssignment();
        _db.Users.Seed(new User
        {
            Id = _studentId,
            Code = "STD-001",
            Email = "student@test.com",
            Role = RoleType.Student,
            IsDeleted = false
        });
        _db.ModuleEnrollments.Seed(new ModuleEnrollment
        {
            Id = Guid.NewGuid(),
            StudentId = _studentId,
            ModuleId = _moduleId,
            Status = EnrollmentStatus.Active,
            IsDeleted = false
        });
        var sut = CreateSut(currentUserId: _studentId);

        var result = await sut.GetAssignmentById(_assignmentId);

        Assert.NotNull(result);
        Assert.Equal(_assignmentId, result!.Id);
    }

    // ── UpdateAssignment ──────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateAssignment_UpdatesFields_AsManager()
    {
        SeedManager();
        SeedModule();
        SeedAssignment();
        var sut = CreateSut();

        var result = await sut.UpdateAssignment(_assignmentId, new UpdateAssignmentRequestDto
        {
            Title = "  Updated Title  ",
            Description = "  ",
            MaxPoints = 20,
            PassScore = 10,
            MaxAttempts = 3
        });

        Assert.NotNull(result);
        Assert.Equal("Updated Title", result!.Title);
        Assert.Null(result.Description);
        Assert.Equal(20, result.MaxPoints);
        Assert.Equal(10m, result.PassScore);
        Assert.Equal(3, result.MaxAttempts);
        Assert.Equal(1, _db.SaveChangesCallCount);
        _notificationPublisher.Verify(
            n => n.PublishAsync(It.IsAny<NotificationCommand>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task UpdateAssignment_UpdatesAllNullableFields()
    {
        SeedManager();
        SeedModule();
        SeedAssignment();
        var sut = CreateSut();

        var result = await sut.UpdateAssignment(_assignmentId, new UpdateAssignmentRequestDto
        {
            Code = "ASN-UPDATED",
            Title = "Full Update",
            Description = "Updated desc",
            AssignmentType = AssignmentType.FileUpload,
            MaxPoints = 50,
            PassScore = 25,
            MaxAttempts = 5,
            IsRequiredForModulePass = true,
            DueDate = DateTime.UtcNow.AddDays(30),
            AvailableFrom = DateTime.UtcNow,
            AvailableUntil = DateTime.UtcNow.AddDays(60),
            AllowShuffle = true,
            ShuffleOptions = true,
            TimeLimitMinutes = 90,
            EasyPercent = 30,
            MediumPercent = 40,
            HardPercent = 30,
        });

        Assert.NotNull(result);
        Assert.Equal("ASN-UPDATED", result!.Code);
        Assert.Equal("Full Update", result.Title);
        Assert.Equal(50, result.MaxPoints);
        Assert.True(result.IsRequiredForModulePass);
    }

    [Fact]
    public async Task UpdateAssignment_ReturnsNull_WhenMissing()
    {
        SeedManager();
        var sut = CreateSut();

        var result = await sut.UpdateAssignment(Guid.NewGuid(), new UpdateAssignmentRequestDto
        {
            Title = "Nope"
        });

        Assert.Null(result);
    }

    [Fact]
    public async Task UpdateAssignment_ThrowsUnauthorized_WhenCallerMissing()
    {
        SeedModule();
        SeedAssignment();
        var sut = CreateSut();

        await Assert.ThrowsAsync<UnauthorizedException>(() =>
            sut.UpdateAssignment(_assignmentId, new UpdateAssignmentRequestDto { Title = "X" }));
    }

    [Fact]
    public async Task UpdateAssignment_ThrowsConflict_WhenCodeDuplicate()
    {
        SeedManager();
        SeedModule();
        SeedAssignment(code: "ASN-A");
        _db.Assignments.Seed(new Assignment
        {
            Id = Guid.NewGuid(),
            Code = "ASN-B",
            ModuleId = _moduleId,
            Title = "Other",
            AssignmentType = AssignmentType.FileUpload,
            MaxPoints = 10,
            PassScore = 5,
            MaxAttempts = 1,
            IsDeleted = false
        });
        var sut = CreateSut();

        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            sut.UpdateAssignment(_assignmentId, new UpdateAssignmentRequestDto { Code = "asn-b" }));
        Assert.Contains("already exists", ex.Message);
    }

    [Fact]
    public async Task UpdateAssignment_ThrowsNotFound_WhenNewModuleMissing()
    {
        SeedManager();
        SeedModule();
        SeedAssignment();
        var sut = CreateSut();

        await Assert.ThrowsAsync<NotFoundException>(() =>
            sut.UpdateAssignment(_assignmentId, new UpdateAssignmentRequestDto
            {
                ModuleId = Guid.NewGuid()
            }));
    }

    [Fact]
    public async Task UpdateAssignment_MentorCanUpdateAllowedFields_AndPublishes()
    {
        SeedModule();
        SeedAssignment();
        SeedMentorWithClass();
        var sut = CreateSut(currentUserId: _mentorId);
        var due = DateTime.UtcNow.AddDays(7);

        var result = await sut.UpdateAssignment(_assignmentId, new UpdateAssignmentRequestDto
        {
            Title = "Mentor Title",
            Description = "Mentor desc",
            DueDate = due,
            AvailableFrom = DateTime.UtcNow,
            AvailableUntil = due.AddDays(1)
        });

        Assert.NotNull(result);
        Assert.Equal("Mentor Title", result!.Title);
        Assert.Equal("Mentor desc", result.Description);
        _notificationPublisher.Verify(
            n => n.PublishAsync(It.IsAny<NotificationCommand>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task UpdateAssignment_ThrowsForbidden_WhenMentorUpdatesRestrictedField()
    {
        SeedModule();
        SeedAssignment();
        SeedMentorWithClass();
        var sut = CreateSut(currentUserId: _mentorId);

        var ex = await Assert.ThrowsAsync<ForbiddenException>(() =>
            sut.UpdateAssignment(_assignmentId, new UpdateAssignmentRequestDto
            {
                Title = "Ok title",
                MaxPoints = 99
            }));

        Assert.Contains("Mentors may only update", ex.Message);
    }

    [Fact]
    public async Task UpdateAssignment_ThrowsForbidden_WhenMentorDoesNotOwnProgram()
    {
        SeedModule();
        SeedAssignment();
        _db.Users.Seed(new User
        {
            Id = _mentorId,
            Code = "MNT-001",
            Email = "mentor@test.com",
            Role = RoleType.Mentor,
            IsDeleted = false
        });
        var sut = CreateSut(currentUserId: _mentorId);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            sut.UpdateAssignment(_assignmentId, new UpdateAssignmentRequestDto
            {
                Title = "No ownership"
            }));
    }

    [Fact]
    public async Task UpdateAssignment_ThrowsBadRequest_WhenChangingToQuizWithInvalidPercents()
    {
        SeedManager();
        SeedModule();
        SeedCourse();
        SeedAssignment();
        _db.QuestionBanks.Seed(new QuestionBank
        {
            Id = _questionBankId,
            CourseId = _courseId,
            IsDeleted = false
        });
        var sut = CreateSut();

        await Assert.ThrowsAsync<BadRequestException>(() =>
            sut.UpdateAssignment(_assignmentId, new UpdateAssignmentRequestDto
            {
                AssignmentType = AssignmentType.Quiz,
                QuestionBankId = _questionBankId,
                EasyPercent = 40,
                MediumPercent = 40,
                HardPercent = 40
            }));
    }

    // ── DeleteAssignment ──────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteAssignment_SoftDeletes_WhenNoSubmissions()
    {
        SeedModule();
        var assignment = SeedAssignment();
        var sut = CreateSut();

        var result = await sut.DeleteAssignment(_assignmentId);

        Assert.True(result);
        Assert.True(assignment.IsDeleted);
        Assert.Equal(1, _db.SaveChangesCallCount);
    }

    [Fact]
    public async Task DeleteAssignment_ReturnsFalse_WhenMissing()
    {
        var sut = CreateSut();

        var result = await sut.DeleteAssignment(Guid.NewGuid());

        Assert.False(result);
    }

    [Fact]
    public async Task DeleteAssignment_ReturnsFalse_WhenAlreadyDeleted()
    {
        SeedModule();
        SeedAssignment(isDeleted: true);
        var sut = CreateSut();

        var result = await sut.DeleteAssignment(_assignmentId);

        Assert.False(result);
    }

    [Fact]
    public async Task DeleteAssignment_ThrowsConflict_WhenHasSubmissions()
    {
        SeedModule();
        SeedAssignment();
        _db.Submissions.Seed(new Submission
        {
            Id = Guid.NewGuid(),
            Code = "SUB-001",
            AssignmentId = _assignmentId,
            StudentId = _studentId,
            Status = SubmissionStatus.Pending,
            AttemptNumber = 1,
            IsDeleted = false
        });
        var sut = CreateSut();

        var ex = await Assert.ThrowsAsync<ConflictException>(() => sut.DeleteAssignment(_assignmentId));
        Assert.Equal("Cannot delete an assignment that has existing submissions.", ex.Message);
    }

    [Fact]
    public async Task GetAllAssignments_SortsAndSearchesByConfiguredColumns()
    {
        var module = SeedCatalogModule();
        SeedCatalogAssignment(module, code: "ZZZ-999", title: "Zulu Task", dueDate: DateTime.UtcNow.AddDays(10));
        SeedCatalogAssignment(module, code: "AAA-001", title: "Alpha Task", dueDate: DateTime.UtcNow.AddDays(1));
        var sut = CreateSut();

        var bySearch = await sut.GetAllAssignments("Zulu", null, false, 1, 10);
        Assert.Single(bySearch.Items);

        var byTitleAsc = await sut.GetAllAssignments(null, "title", false, 1, 10);
        Assert.Equal("Alpha Task", byTitleAsc.Items[0].Title);

        var byCodeDesc = await sut.GetAllAssignments(null, "code", true, 1, 10);
        Assert.Equal("ZZZ-999", byCodeDesc.Items[0].Code);

        var byDueDate = await sut.GetAllAssignments(null, "duedate", false, 1, 10);
        Assert.Equal("Alpha Task", byDueDate.Items[0].Title);

        var byModuleName = await sut.GetAllAssignments(null, "modulename", false, 1, 10);
        Assert.NotEmpty(byModuleName.Items);

        var byProgramName = await sut.GetAllAssignments(null, "programname", true, 1, 10);
        Assert.NotEmpty(byProgramName.Items);

        var byType = await sut.GetAllAssignments(null, "assignmenttype", false, 1, 10);
        Assert.NotEmpty(byType.Items);

        var defaultSort = await sut.GetAllAssignments(null, "unknown", false, 1, 10);
        Assert.Equal(2, defaultSort.Items.Count);
    }
}
