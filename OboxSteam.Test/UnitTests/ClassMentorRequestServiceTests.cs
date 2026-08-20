using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OboxSteam.Application.DTOs.ClassMentorRequestDTO;
using OboxSteam.Application.Exceptions;
using OboxSteam.Application.Interfaces;
using OboxSteam.Application.Notifications;
using OboxSteam.Application.Services;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;
using OboxSteam.Test.Helpers;

namespace OboxSteam.Test.UnitTests;

public sealed class ClassMentorRequestServiceTests
{
    private readonly Guid _mentorId = Guid.Parse("14141414-1414-1414-1414-141414141414");
    private readonly Guid _otherMentorId = Guid.Parse("15151515-1515-1515-1515-151515151515");
    private readonly Guid _managerId = Guid.Parse("13131313-1313-1313-1313-131313131313");
    private readonly Guid _studentId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private readonly Guid _skillId = Guid.Parse("55555555-5555-5555-5555-555555555555");
    private readonly Guid _otherSkillId = Guid.Parse("56565656-5656-5656-5656-565656565656");
    private readonly Guid _programId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private readonly Guid _otherProgramId = Guid.Parse("23232323-2323-2323-2323-232323232323");
    private readonly Guid _classId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private readonly Guid _otherClassId = Guid.Parse("45454545-4545-4545-4545-454545454545");
    private readonly Guid _requestId = Guid.Parse("77777777-7777-7777-7777-777777777777");
    private readonly Guid _otherRequestId = Guid.Parse("78787878-7878-7878-7878-787878787878");
    private readonly Guid _moduleId = Guid.Parse("33333333-3333-3333-3333-333333333333");

    private readonly DateTime _now = DateTime.UtcNow;

    private readonly InMemoryUnitOfWork _db = new();
    private readonly Mock<IClaimsService> _claimsService = new();
    private readonly Mock<INotificationPublisher> _notificationPublisher = new();

    private ClassMentorRequestService CreateSut(Guid? currentUserId = null)
    {
        _claimsService.Setup(c => c.GetCurrentUserId).Returns(currentUserId ?? _mentorId);
        _notificationPublisher
            .Setup(n => n.PublishAsync(It.IsAny<NotificationCommand>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _notificationPublisher
            .Setup(n => n.PublishManyAsync(It.IsAny<IReadOnlyList<NotificationCommand>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        return new ClassMentorRequestService(
            _db,
            _claimsService.Object,
            NullLogger<ClassMentorRequestService>.Instance,
            _notificationPublisher.Object);
    }

    private void SeedUser(
        Guid id,
        RoleType role,
        string code,
        string? fullName = null,
        int? maxConcurrent = null)
    {
        _db.Users.Seed(new User
        {
            Id = id,
            Code = code,
            Email = $"{code.ToLower()}@test.com",
            FullName = fullName ?? code,
            Role = role,
            Status = AccountStatus.Active,
            MaxConcurrentClasses = maxConcurrent,
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

    private void SeedSkill(Guid? id = null, string code = "SKL-001", string name = "Robotics")
    {
        _db.Skills.Seed(new Skill
        {
            Id = id ?? _skillId,
            Code = code,
            Name = name,
            Category = SkillCategory.Technology,
            IsDeleted = false,
        });
    }

    private Class SeedClass(
        Guid? id = null,
        string code = "CLS-001",
        string name = "Cohort A",
        ClassStatus status = ClassStatus.Draft,
        Guid? mentorId = null,
        Guid? programId = null,
        DateTime? createdAt = null,
        bool withSchedule = true)
    {
        var entity = new Class
        {
            Id = id ?? _classId,
            Code = code,
            Name = name,
            ProgramId = programId ?? _programId,
            MentorId = mentorId,
            Status = status,
            MaxCapacity = 20,
            StartDate = _now.AddDays(1),
            EndDate = _now.AddDays(30),
            ScheduleSummary = "Weekends",
            CreatedAt = createdAt ?? _now.AddHours(-2),
            IsDeleted = false,
        };
        _db.Classes.Seed(entity);

        if (withSchedule)
        {
            _db.ClassSessions.Seed(new ClassSession
            {
                Id = Guid.NewGuid(),
                ClassId = entity.Id,
                Title = "Seeded session",
                SessionKind = SessionKind.Lesson,
                StartTime = _now.AddDays(5),
                EndTime = _now.AddDays(5).AddHours(2),
                Status = ClassSessionStatus.Scheduled,
                IsDeleted = false,
            });
        }

        return entity;
    }

    private void SeedClassSkill(Guid classId, Guid skillId)
    {
        _db.ClassSkills.Seed(new ClassSkill
        {
            Id = Guid.NewGuid(),
            ClassId = classId,
            SkillId = skillId,
            IsDeleted = false,
        });
    }

    private void SeedMentorSkill(Guid mentorId, Guid skillId)
    {
        _db.MentorSkills.Seed(new MentorSkill
        {
            Id = Guid.NewGuid(),
            MentorId = mentorId,
            SkillId = skillId,
            ProficiencyLevel = SkillProficiencyLevel.Intermediate,
            IsDeleted = false,
        });
    }

    private ClassMentorRequest SeedRequest(
        Guid? id = null,
        Guid? classId = null,
        Guid? mentorId = null,
        ClassMentorRequestStatus status = ClassMentorRequestStatus.Pending,
        string? message = null)
    {
        var entity = new ClassMentorRequest
        {
            Id = id ?? _requestId,
            ClassId = classId ?? _classId,
            MentorId = mentorId ?? _mentorId,
            Status = status,
            Message = message,
            IsDeleted = false,
            CreatedAt = _now.AddHours(-1),
        };
        _db.ClassMentorRequests.Seed(entity);
        return entity;
    }

    // ── GetMentorBoardAsync ───────────────────────────────────────────────────

    [Fact]
    public async Task GetMentorBoard_ReturnsScheduledDraftClassesOnly()
    {
        SeedUser(_mentorId, RoleType.Mentor, "MNT-001");
        SeedProgram();
        // Open classes always have a mentor requirement to open — an unassigned open
        // class is legacy data and must not be requestable.
        SeedClass(status: ClassStatus.Open, mentorId: null);
        SeedClass(
            id: _otherClassId,
            code: "CLS-002",
            name: "Cohort B",
            status: ClassStatus.Draft,
            mentorId: null);
        SeedClass(
            id: Guid.Parse("46464646-4646-4646-4646-464646464646"),
            code: "CLS-ASSIGNED",
            status: ClassStatus.Draft,
            mentorId: _otherMentorId);
        SeedClass(
            id: Guid.Parse("47474747-4747-4747-4747-474747474747"),
            code: "CLS-DONE",
            status: ClassStatus.Completed,
            mentorId: null);
        SeedClass(
            id: Guid.Parse("48484848-4848-4848-4848-484848484848"),
            code: "CLS-NOSCHED",
            status: ClassStatus.Draft,
            mentorId: null,
            withSchedule: false);
        var sut = CreateSut();

        var result = await sut.GetMentorBoardAsync(null, null, false, 1, 10);

        Assert.Equal(1, result.TotalCount);
        Assert.Contains(result.Items, i => i.Code == "CLS-002");
    }

    [Fact]
    public async Task GetMentorBoard_FiltersByProgram_Search_SortAndFlags()
    {
        SeedUser(_mentorId, RoleType.Mentor, "MNT-001");
        SeedProgram();
        SeedProgram(_otherProgramId);
        SeedSkill();
        SeedSkill(_otherSkillId, "SKL-002", "Coding");
        SeedMentorSkill(_mentorId, _skillId);
        SeedClass(code: "CLS-ZEBRA", name: "Zebra", createdAt: _now.AddHours(-3));
        SeedClass(
            id: _otherClassId,
            code: "CLS-ALPHA",
            name: "Alpha",
            programId: _otherProgramId,
            createdAt: _now.AddHours(-1));
        SeedClassSkill(_classId, _skillId);
        SeedClassSkill(_otherClassId, _otherSkillId);
        SeedRequest(classId: _classId);
        _db.ClassMentorRequests.Seed(new ClassMentorRequest
        {
            Id = Guid.NewGuid(),
            ClassId = _classId,
            MentorId = _otherMentorId,
            Status = ClassMentorRequestStatus.Pending,
            IsDeleted = false,
        });
        var sut = CreateSut();

        var filtered = await sut.GetMentorBoardAsync("zeb", "name", false, 1, 10, _programId);
        var board = filtered.Items.Single();

        Assert.Equal(1, filtered.TotalCount);
        Assert.True(board.MatchesMySkills);
        Assert.True(board.HasPendingRequestFromMe);
        Assert.Equal(2, board.PendingRequestCount);
        Assert.Single(board.RequiredSkills);
        Assert.Equal("Robotics", board.RequiredSkills[0].Name);

        var sorted = await sut.GetMentorBoardAsync(null, "code", false, 1, 10);
        Assert.Equal("CLS-ALPHA", sorted.Items[0].Code);
        Assert.Equal("CLS-ZEBRA", sorted.Items[1].Code);

        var byStart = await sut.GetMentorBoardAsync(null, "startdate", true, 1, 10);
        Assert.True(byStart.TotalCount >= 1);
    }

    [Fact]
    public async Task GetMentorBoard_MatchMySkills_ReturnsEmpty_WhenMentorHasNoSkills()
    {
        SeedUser(_mentorId, RoleType.Mentor, "MNT-001");
        SeedProgram();
        SeedClass();
        var sut = CreateSut();

        var result = await sut.GetMentorBoardAsync(null, null, false, 1, 10, matchMySkills: true);

        Assert.Equal(0, result.TotalCount);
        Assert.Empty(result.Items);
    }

    [Fact]
    public async Task GetMentorBoard_Throws_WhenNotMentorOrInvalidPagination()
    {
        SeedUser(_managerId, RoleType.Manager, "MGR-001");
        var sut = CreateSut(_managerId);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            sut.GetMentorBoardAsync(null, null, false, 1, 10));
        await Assert.ThrowsAsync<BadRequestException>(() =>
            sut.GetMentorBoardAsync(null, null, false, 0, 10));
    }

    // ── CreateRequestAsync ────────────────────────────────────────────────────

    [Fact]
    public async Task CreateRequest_PersistsAndNotifies()
    {
        SeedUser(_mentorId, RoleType.Mentor, "MNT-001", "Mentor One");
        SeedProgram();
        SeedClass(status: ClassStatus.Draft, mentorId: null);
        var sut = CreateSut();

        var result = await sut.CreateRequestAsync(new CreateClassMentorRequestDto
        {
            ClassId = _classId,
            Message = "  Available weekends  ",
        });

        Assert.Equal(ClassMentorRequestStatus.Pending, result.Status);
        Assert.Equal("Available weekends", result.Message);
        Assert.Equal("CLS-001", result.ClassCode);
        Assert.Equal("Mentor One", result.MentorName);
        Assert.Single(_db.ClassMentorRequests.Items);
        _notificationPublisher.Verify(
            n => n.PublishAsync(It.IsAny<NotificationCommand>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CreateRequest_Throws_WhenClassUnavailableOrDuplicate()
    {
        SeedUser(_mentorId, RoleType.Mentor, "MNT-001");
        SeedUser(_otherMentorId, RoleType.Mentor, "MNT-002");
        SeedProgram();
        SeedClass(status: ClassStatus.Open, mentorId: _otherMentorId);
        var sut = CreateSut();

        await Assert.ThrowsAsync<ConflictException>(() =>
            sut.CreateRequestAsync(new CreateClassMentorRequestDto { ClassId = _classId }));

        SeedClass(id: _otherClassId, code: "CLS-002", status: ClassStatus.Draft, mentorId: null);
        SeedRequest(classId: _otherClassId);

        await Assert.ThrowsAsync<ConflictException>(() =>
            sut.CreateRequestAsync(new CreateClassMentorRequestDto { ClassId = _otherClassId }));
    }

    [Fact]
    public async Task CreateRequest_Throws_WhenClassHasNoSchedule()
    {
        SeedUser(_mentorId, RoleType.Mentor, "MNT-001");
        SeedProgram();
        SeedClass(status: ClassStatus.Draft, mentorId: null, withSchedule: false);
        var sut = CreateSut();

        var ex = await Assert.ThrowsAsync<BadRequestException>(() =>
            sut.CreateRequestAsync(new CreateClassMentorRequestDto { ClassId = _classId }));
        Assert.Contains("no schedule", ex.Message);
    }

    [Fact]
    public async Task CreateRequest_Throws_WhenClassNotDraft()
    {
        SeedUser(_mentorId, RoleType.Mentor, "MNT-001");
        SeedProgram();
        SeedClass(status: ClassStatus.Open, mentorId: null);
        var sut = CreateSut();

        await Assert.ThrowsAsync<BadRequestException>(() =>
            sut.CreateRequestAsync(new CreateClassMentorRequestDto { ClassId = _classId }));
    }

    [Fact]
    public async Task CreateRequest_Throws_WhenConcurrentLimitReached()
    {
        SeedUser(_mentorId, RoleType.Mentor, "MNT-001", maxConcurrent: 1);
        SeedProgram();
        _db.Classes.Seed(new Class
        {
            Id = Guid.Parse("48484848-4848-4848-4848-484848484848"),
            Code = "CLS-BUSY",
            Name = "Busy",
            ProgramId = _programId,
            MentorId = _mentorId,
            Status = ClassStatus.Open,
            MaxCapacity = 10,
            StartDate = _now.AddDays(1),
            EndDate = _now.AddDays(20),
            IsDeleted = false,
        });
        SeedClass(status: ClassStatus.Draft, mentorId: null);
        var sut = CreateSut();

        await Assert.ThrowsAsync<ConflictException>(() =>
            sut.CreateRequestAsync(new CreateClassMentorRequestDto { ClassId = _classId }));
    }

    [Fact]
    public async Task CreateRequest_Throws_WhenClassMissing()
    {
        SeedUser(_mentorId, RoleType.Mentor, "MNT-001");
        var sut = CreateSut();

        await Assert.ThrowsAsync<NotFoundException>(() =>
            sut.CreateRequestAsync(new CreateClassMentorRequestDto { ClassId = _classId }));
    }

    [Fact]
    public async Task CreateRequest_Throws_WhenMentorScheduleOverlapsClassSessions()
    {
        SeedUser(_mentorId, RoleType.Mentor, "MNT-001");
        SeedProgram();
        var busyClass = SeedClass(
            id: _otherClassId,
            code: "CLS-BUSY",
            status: ClassStatus.InProgress,
            mentorId: _mentorId,
            withSchedule: false);
        var targetClass = SeedClass(status: ClassStatus.Draft, mentorId: null, withSchedule: false);
        var start = _now.AddDays(2);
        _db.ClassSessions.Seed(
            new ClassSession
            {
                Id = Guid.Parse("55555555-5555-5555-5555-555555555555"),
                ClassId = _otherClassId,
                ModuleId = _moduleId,
                Title = "Busy Session",
                SessionKind = SessionKind.Lesson,
                StartTime = start,
                EndTime = start.AddHours(2),
                Status = ClassSessionStatus.Scheduled,
                Class = busyClass,
                IsDeleted = false,
            },
            new ClassSession
            {
                Id = Guid.Parse("66666666-6666-6666-6666-666666666666"),
                ClassId = _classId,
                ModuleId = _moduleId,
                Title = "Target Session",
                SessionKind = SessionKind.Lesson,
                StartTime = start.AddHours(1),
                EndTime = start.AddHours(3),
                Status = ClassSessionStatus.Scheduled,
                Class = targetClass,
                IsDeleted = false,
            });
        var sut = CreateSut();

        await Assert.ThrowsAsync<ConflictException>(() =>
            sut.CreateRequestAsync(new CreateClassMentorRequestDto { ClassId = _classId }));

        // Fail fast: no request is persisted when the mentor cannot cover the schedule.
        Assert.Empty(_db.ClassMentorRequests.Items);
    }

    [Fact]
    public async Task CreateRequest_Persists_WhenClassSessionsDoNotOverlapMentorSchedule()
    {
        SeedUser(_mentorId, RoleType.Mentor, "MNT-001", "Mentor One");
        SeedProgram();
        var targetClass = SeedClass(status: ClassStatus.Draft, mentorId: null, withSchedule: false);
        _db.ClassSessions.Seed(new ClassSession
        {
            Id = Guid.Parse("66666666-6666-6666-6666-666666666666"),
            ClassId = _classId,
            ModuleId = _moduleId,
            Title = "Target Session",
            SessionKind = SessionKind.Lesson,
            StartTime = _now.AddDays(2),
            EndTime = _now.AddDays(2).AddHours(2),
            Status = ClassSessionStatus.Scheduled,
            Class = targetClass,
            IsDeleted = false,
        });
        var sut = CreateSut();

        var result = await sut.CreateRequestAsync(new CreateClassMentorRequestDto
        {
            ClassId = _classId,
        });

        Assert.Equal(ClassMentorRequestStatus.Pending, result.Status);
        Assert.Single(_db.ClassMentorRequests.Items);
    }

    // ── WithdrawRequestAsync ──────────────────────────────────────────────────

    [Fact]
    public async Task WithdrawRequest_WithdrawsPending()
    {
        SeedUser(_mentorId, RoleType.Mentor, "MNT-001");
        SeedProgram();
        SeedClass();
        SeedRequest();
        var sut = CreateSut();

        await sut.WithdrawRequestAsync(_requestId);

        Assert.Equal(ClassMentorRequestStatus.Withdrawn, _db.ClassMentorRequests.Items.Single().Status);
    }

    [Fact]
    public async Task WithdrawRequest_Throws_WhenNotOwnerOrNotPending()
    {
        SeedUser(_mentorId, RoleType.Mentor, "MNT-001");
        SeedUser(_otherMentorId, RoleType.Mentor, "MNT-002");
        SeedProgram();
        SeedClass();
        SeedRequest(mentorId: _otherMentorId);
        var sut = CreateSut();

        await Assert.ThrowsAsync<ForbiddenException>(() => sut.WithdrawRequestAsync(_requestId));

        SeedRequest(id: _otherRequestId, mentorId: _mentorId, status: ClassMentorRequestStatus.Approved);
        await Assert.ThrowsAsync<BadRequestException>(() => sut.WithdrawRequestAsync(_otherRequestId));
    }

    // ── GetMyRequestsAsync ────────────────────────────────────────────────────

    [Fact]
    public async Task GetMyRequests_ReturnsFilteredPage()
    {
        SeedUser(_mentorId, RoleType.Mentor, "MNT-001", "Mentor One");
        SeedProgram();
        SeedClass();
        SeedRequest();
        SeedRequest(
            id: _otherRequestId,
            classId: _otherClassId,
            status: ClassMentorRequestStatus.Withdrawn);
        SeedClass(id: _otherClassId, code: "CLS-002");
        var sut = CreateSut();

        var all = await sut.GetMyRequestsAsync(null, 1, 10);
        var pending = await sut.GetMyRequestsAsync(ClassMentorRequestStatus.Pending, 1, 10);

        Assert.Equal(2, all.TotalCount);
        Assert.Equal(1, pending.TotalCount);
        Assert.Equal("CLS-001", pending.Items[0].ClassCode);
        Assert.Equal("Mentor One", pending.Items[0].MentorName);
    }

    [Fact]
    public async Task GetMyRequests_Throws_WhenNotMentor()
    {
        SeedUser(_managerId, RoleType.Manager, "MGR-001");
        var sut = CreateSut(_managerId);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            sut.GetMyRequestsAsync(null, 1, 10));
    }

    // ── GetRequestsForManagerAsync ────────────────────────────────────────────

    [Fact]
    public async Task GetRequestsForManager_ReturnsFiltered_ForManager()
    {
        SeedUser(_managerId, RoleType.Manager, "MGR-001");
        SeedUser(_mentorId, RoleType.Mentor, "MNT-001", "Mentor One");
        SeedProgram();
        SeedClass();
        SeedClass(id: _otherClassId, code: "CLS-002");
        SeedRequest();
        SeedRequest(id: _otherRequestId, classId: _otherClassId, mentorId: _otherMentorId);
        var sut = CreateSut(_managerId);

        var result = await sut.GetRequestsForManagerAsync(_classId, _mentorId, ClassMentorRequestStatus.Pending, 1, 10);

        Assert.Equal(1, result.TotalCount);
        Assert.Equal(_requestId, result.Items[0].Id);
    }

    [Fact]
    public async Task GetRequestsForManager_ForbidsMentor()
    {
        SeedUser(_mentorId, RoleType.Mentor, "MNT-001");
        var sut = CreateSut();

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            sut.GetRequestsForManagerAsync(null, null, null, 1, 10));
    }

    // ── ApproveRequestAsync ───────────────────────────────────────────────────

    [Fact]
    public async Task ApproveRequest_AssignsMentor_RejectsSiblings()
    {
        SeedUser(_managerId, RoleType.Manager, "MGR-001");
        SeedUser(_mentorId, RoleType.Mentor, "MNT-001", "Winner");
        SeedUser(_otherMentorId, RoleType.Mentor, "MNT-002", "Loser");
        SeedProgram();
        SeedClass(status: ClassStatus.Draft, mentorId: null);
        SeedRequest();
        SeedRequest(id: _otherRequestId, mentorId: _otherMentorId);
        var sut = CreateSut(_managerId);

        var result = await sut.ApproveRequestAsync(_requestId, new DecideClassMentorRequestDto
        {
            DecisionNote = "  Great fit  ",
        });

        Assert.Equal(ClassMentorRequestStatus.Approved, result.Status);
        Assert.Equal("Great fit", result.DecisionNote);
        Assert.Equal(_mentorId, _db.Classes.Items.Single(c => c.Id == _classId).MentorId);
        Assert.Equal(ClassMentorRequestStatus.Rejected, _db.ClassMentorRequests.Items.Single(r => r.Id == _otherRequestId).Status);
        _notificationPublisher.Verify(
            n => n.PublishManyAsync(It.IsAny<IReadOnlyList<NotificationCommand>>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ApproveRequest_Throws_WhenSessionOverlap()
    {
        SeedUser(_managerId, RoleType.Manager, "MGR-001");
        SeedUser(_mentorId, RoleType.Mentor, "MNT-001");
        SeedProgram();
        var assignedClass = SeedClass(
            id: _otherClassId,
            code: "CLS-BUSY",
            status: ClassStatus.Open,
            mentorId: _mentorId,
            withSchedule: false);
        SeedClass(status: ClassStatus.Draft, mentorId: null, withSchedule: false);
        SeedRequest();
        var start = _now.AddDays(2);
        var end = start.AddHours(2);
        _db.ClassSessions.Seed(
            new ClassSession
            {
                Id = Guid.Parse("55555555-5555-5555-5555-555555555555"),
                ClassId = _otherClassId,
                ModuleId = _moduleId,
                Title = "Busy Session",
                SessionKind = SessionKind.Lesson,
                StartTime = start,
                EndTime = end,
                Status = ClassSessionStatus.Scheduled,
                Class = assignedClass,
                IsDeleted = false,
            },
            new ClassSession
            {
                Id = Guid.Parse("66666666-6666-6666-6666-666666666666"),
                ClassId = _classId,
                ModuleId = _moduleId,
                Title = "Target Session",
                SessionKind = SessionKind.Lesson,
                StartTime = start.AddHours(1),
                EndTime = end.AddHours(1),
                Status = ClassSessionStatus.Scheduled,
                Class = _db.Classes.Items.Single(c => c.Id == _classId),
                IsDeleted = false,
            });
        var sut = CreateSut(_managerId);

        await Assert.ThrowsAsync<ConflictException>(() =>
            sut.ApproveRequestAsync(_requestId, null));
    }

    [Fact]
    public async Task ApproveRequest_Throws_WhenClassScheduleWasDeleted()
    {
        // The mentor requested when a schedule existed; the manager deleted the sessions
        // afterwards. Approving into a timetable-less class is pointless — block it.
        SeedUser(_managerId, RoleType.Manager, "MGR-001");
        SeedUser(_mentorId, RoleType.Mentor, "MNT-001");
        SeedProgram();
        SeedClass(status: ClassStatus.Draft, mentorId: null, withSchedule: false);
        SeedRequest();
        var sut = CreateSut(_managerId);

        var ex = await Assert.ThrowsAsync<BadRequestException>(() =>
            sut.ApproveRequestAsync(_requestId, null));
        Assert.Contains("no schedule", ex.Message);
    }

    [Fact]
    public async Task ApproveRequest_Throws_WhenNotPendingOrForbidden()
    {
        SeedUser(_mentorId, RoleType.Mentor, "MNT-001");
        SeedProgram();
        SeedClass();
        SeedRequest(status: ClassMentorRequestStatus.Approved);
        var mentorSut = CreateSut();

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            mentorSut.ApproveRequestAsync(_requestId, null));

        SeedUser(_managerId, RoleType.Manager, "MGR-001");
        var managerSut = CreateSut(_managerId);
        await Assert.ThrowsAsync<BadRequestException>(() =>
            managerSut.ApproveRequestAsync(_requestId, null));
    }

    // ── RejectRequestAsync ────────────────────────────────────────────────────

    [Fact]
    public async Task RejectRequest_RejectsAndNotifies()
    {
        SeedUser(_managerId, RoleType.Manager, "MGR-001");
        SeedUser(_mentorId, RoleType.Mentor, "MNT-001");
        SeedProgram();
        SeedClass();
        SeedRequest();
        var sut = CreateSut(_managerId);

        var result = await sut.RejectRequestAsync(_requestId, new DecideClassMentorRequestDto
        {
            DecisionNote = "  Not a fit  ",
        });

        Assert.Equal(ClassMentorRequestStatus.Rejected, result.Status);
        Assert.Equal("Not a fit", result.DecisionNote);
        Assert.Null(_db.Classes.Items.Single().MentorId);
        _notificationPublisher.Verify(
            n => n.PublishAsync(It.IsAny<NotificationCommand>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RejectRequest_Throws_WhenNotPending()
    {
        SeedUser(_managerId, RoleType.Manager, "MGR-001");
        SeedProgram();
        SeedClass();
        SeedRequest(status: ClassMentorRequestStatus.Withdrawn);
        var sut = CreateSut(_managerId);

        await Assert.ThrowsAsync<BadRequestException>(() =>
            sut.RejectRequestAsync(_requestId, null));
    }
}
