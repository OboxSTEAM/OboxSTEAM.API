using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OboxSteam.Application.DTOs.ClassDTO;
using OboxSteam.Application.Exceptions;
using OboxSteam.Application.Interfaces;
using OboxSteam.Application.Notifications;
using OboxSteam.Application.Services;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;
using OboxSteam.Test.Helpers;

namespace OboxSteam.Test.UnitTests;

public sealed class ClassServiceTests
{
    private readonly Guid _studentId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private readonly Guid _otherStudentId = Guid.Parse("12121212-1212-1212-1212-121212121212");
    private readonly Guid _managerId = Guid.Parse("13131313-1313-1313-1313-131313131313");
    private readonly Guid _mentorId = Guid.Parse("14141414-1414-1414-1414-141414141414");
    private readonly Guid _otherMentorId = Guid.Parse("15151515-1515-1515-1515-151515151515");
    private readonly Guid _parentId = Guid.Parse("16161616-1616-1616-1616-161616161616");
    private readonly Guid _programId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private readonly Guid _otherProgramId = Guid.Parse("23232323-2323-2323-2323-232323232323");
    private readonly Guid _classId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private readonly Guid _skillId = Guid.Parse("55555555-5555-5555-5555-555555555555");
    private readonly Guid _sessionId = Guid.Parse("66666666-6666-6666-6666-666666666666");

    private readonly DateTime _now = DateTime.UtcNow;

    private readonly InMemoryUnitOfWork _db = new();
    private readonly Mock<IClaimsService> _claimsService = new();
    private readonly Mock<INotificationPublisher> _notificationPublisher = new();

    private ClassService CreateSut(Guid? currentUserId = null)
    {
        _claimsService.Setup(c => c.GetCurrentUserId).Returns(currentUserId ?? _managerId);
        _notificationPublisher
            .Setup(n => n.PublishAsync(It.IsAny<NotificationCommand>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _notificationPublisher
            .Setup(n => n.PublishManyAsync(It.IsAny<IReadOnlyList<NotificationCommand>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        return new ClassService(
            _db,
            _claimsService.Object,
            NullLogger<ClassService>.Instance,
            _notificationPublisher.Object);
    }

    private void SeedUser(Guid id, RoleType role, string code, string? fullName = null, int? maxConcurrent = null)
    {
        _db.Users.Seed(new User
        {
            Id = id,
            Code = code,
            Email = $"{code.ToLower()}@test.com",
            FullName = fullName ?? code,
            Role = role,
            MaxConcurrentClasses = maxConcurrent,
            IsDeleted = false,
        });
    }

    private void SeedProgram(Guid? id = null)
    {
        _db.Programs.Seed(new Program
        {
            Id = id ?? _programId,
            Code = id == _otherProgramId ? "PRG-002" : "PRG-001",
            Name = "Robotics",
            Category = ProgramCategory.Technology,
            Level = DifficultyLevel.Beginner,
            IsDeleted = false,
        });
    }

    private void SeedSkill()
    {
        _db.Skills.Seed(new Skill
        {
            Id = _skillId,
            Code = "SK-ROB",
            Name = "Robotics",
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
        int maxCapacity = 2,
        DateTime? startDate = null,
        DateTime? endDate = null,
        bool isDeleted = false,
        DateTime? createdAt = null)
    {
        var entity = new Class
        {
            Id = id ?? _classId,
            Code = code,
            Name = name,
            ProgramId = programId ?? _programId,
            MentorId = mentorId,
            Status = status,
            MaxCapacity = maxCapacity,
            StartDate = startDate ?? _now.AddDays(1),
            EndDate = endDate ?? _now.AddDays(30),
            MinHoursBeforeAssignmentJoin = 48,
            ScheduleSummary = "Sat 9-12",
            CreatedAt = createdAt ?? _now.AddHours(-2),
            IsDeleted = isDeleted,
        };
        _db.Classes.Seed(entity);
        return entity;
    }

    private void SeedSchedulableCurriculum(int liveActivityCount = 1, int assignmentCount = 0)
    {
        var moduleId = Guid.NewGuid();
        var courseId = Guid.NewGuid();

        _db.Modules.Seed(new Module
        {
            Id = moduleId,
            Code = "MOD-SCHED",
            ProgramId = _programId,
            Name = "Schedulable module",
            ModuleType = ModuleType.Theory,
            ModuleOrder = 1,
            IsDeleted = false,
        });
        _db.Courses.Seed(new Course
        {
            Id = courseId,
            Code = "CRS-SCHED",
            ModuleId = moduleId,
            Name = "Schedulable course",
            CourseOrder = 1,
            IsDeleted = false,
        });

        for (var i = 0; i < liveActivityCount; i++)
        {
            _db.Activities.Seed(new Activity
            {
                Id = Guid.NewGuid(),
                Code = $"ACT-SCHED-{i}",
                CourseId = courseId,
                Name = $"Live activity {i}",
                ActivityType = ActivityType.LiveOnline,
                ActivityOrder = i + 1,
                DurationMinutes = 60,
                IsDeleted = false,
            });
        }

        for (var i = 0; i < assignmentCount; i++)
        {
            _db.Assignments.Seed(new Assignment
            {
                Id = Guid.NewGuid(),
                Code = $"ASM-SCHED-{i}",
                ModuleId = moduleId,
                Title = $"Assignment {i}",
                IsDeleted = false,
            });
        }
    }

    private void SeedCoveringSessions(int count)
    {
        for (var i = 0; i < count; i++)
        {
            _db.ClassSessions.Seed(new ClassSession
            {
                Id = Guid.NewGuid(),
                ClassId = _classId,
                Title = $"Session {i}",
                SessionKind = SessionKind.Lesson,
                StartTime = _now.AddDays(1 + i),
                EndTime = _now.AddDays(1 + i).AddHours(2),
                Status = ClassSessionStatus.Scheduled,
                IsDeleted = false,
            });
        }
    }

    private void SeedEnrollment(Guid studentId, Guid? classId = null, DateTime? enrolledAt = null)
    {
        _db.ClassEnrollments.Seed(new ClassEnrollment
        {
            Id = Guid.NewGuid(),
            ClassId = classId ?? _classId,
            StudentId = studentId,
            ProgramEnrollmentId = Guid.NewGuid(),
            Status = ClassEnrollmentStatus.Active,
            EnrolledAt = enrolledAt ?? _now.AddDays(-1),
            CreatedAt = enrolledAt ?? _now.AddDays(-1),
            IsDeleted = false,
        });
    }

    private CreateClassRequestDto BuildCreateRequest(
        string code = "CLS-NEW",
        Guid? mentorId = null,
        List<Guid>? skillIds = null)
    {
        return new CreateClassRequestDto
        {
            Code = code,
            Name = "  New Cohort  ",
            ProgramId = _programId,
            MentorId = mentorId,
            StartDate = _now.AddDays(21),
            EndDate = _now.AddDays(60),
            MaxCapacity = 20,
            MinHoursBeforeAssignmentJoin = 24,
            ScheduleSummary = "  Weekends  ",
            RequiredSkillIds = skillIds,
        };
    }

    // ── GetAllClassesAsync ────────────────────────────────────────────────────

    [Fact]
    public async Task GetAll_ReturnsFilteredSortedPage()
    {
        SeedProgram();
        SeedClass(code: "CLS-A", name: "Alpha", status: ClassStatus.Open, mentorId: _mentorId, createdAt: _now.AddHours(-3));
        SeedClass(
            id: Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            code: "CLS-B",
            name: "Beta",
            status: ClassStatus.Draft,
            createdAt: _now.AddHours(-1));
        SeedClass(
            id: Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
            code: "CLS-X",
            name: "Deleted",
            isDeleted: true);
        var sut = CreateSut();

        var result = await sut.GetAllClassesAsync(
            "alp", "name", false, 1, 10,
            programId: _programId,
            status: ClassStatus.Open,
            mentorId: _mentorId);

        Assert.Equal(1, result.TotalCount);
        Assert.Equal("Alpha", result.Items[0].Name);
    }

    [Fact]
    public async Task GetAll_Throws_WhenPaginationInvalid()
    {
        var sut = CreateSut();

        await Assert.ThrowsAsync<BadRequestException>(() =>
            sut.GetAllClassesAsync(null, null, false, 0, 10));
    }

    [Fact]
    public async Task GetAll_ReturnsEmpty_WhenNoMatches()
    {
        var sut = CreateSut();

        var result = await sut.GetAllClassesAsync(null, null, true, 1, 10);

        Assert.Equal(0, result.TotalCount);
        Assert.Empty(result.Items);
    }

    [Theory]
    [InlineData("name", false)]
    [InlineData("code", true)]
    [InlineData("startdate", false)]
    [InlineData("enddate", true)]
    [InlineData("status", false)]
    [InlineData("maxcapacity", true)]
    [InlineData("createdat", false)]
    [InlineData("unknown", false)]
    public async Task GetAll_SortByColumns_ReturnsResults(string sortBy, bool desc)
    {
        SeedProgram();
        SeedClass();
        var sut = CreateSut();

        var result = await sut.GetAllClassesAsync(null, sortBy, desc, 1, 10);

        Assert.True(result.TotalCount >= 1);
    }

    // ── GetClassByIdAsync ─────────────────────────────────────────────────────

    [Fact]
    public async Task GetById_ReturnsClass_WithSeatsAndMentor()
    {
        SeedProgram();
        SeedUser(_mentorId, RoleType.Mentor, "MNT-001", "Mentor One");
        _db.MentorProfiles.Seed(new MentorProfile
        {
            Id = Guid.NewGuid(),
            MentorId = _mentorId,
            Title = "Lead Mentor",
            IsDeleted = false,
        });
        SeedSkill();
        SeedClass(mentorId: _mentorId, status: ClassStatus.Open);
        SeedEnrollment(_studentId);
        _db.ClassSkills.Seed(new ClassSkill
        {
            Id = Guid.NewGuid(),
            ClassId = _classId,
            SkillId = _skillId,
            IsDeleted = false,
        });
        _db.ClassMentorRequests.Seed(new ClassMentorRequest
        {
            Id = Guid.NewGuid(),
            ClassId = _classId,
            MentorId = _otherMentorId,
            Status = ClassMentorRequestStatus.Pending,
            IsDeleted = false,
        });
        var sut = CreateSut();

        var result = await sut.GetClassByIdAsync(_classId);

        Assert.Equal(_classId, result.Id);
        Assert.Equal(1, result.SeatsTaken);
        Assert.NotNull(result.Mentor);
        Assert.Equal("Mentor One", result.Mentor!.FullName);
        Assert.Single(result.RequiredSkills);
        Assert.Equal(1, result.PendingMentorRequestCount);
    }

    [Fact]
    public async Task GetById_Throws_WhenMissing()
    {
        var sut = CreateSut();

        await Assert.ThrowsAsync<NotFoundException>(() =>
            sut.GetClassByIdAsync(_classId));
    }

    // ── GetClassWithStudentsAsync ─────────────────────────────────────────────

    [Fact]
    public async Task GetWithStudents_ReturnsRoster_ForManager()
    {
        SeedUser(_managerId, RoleType.Manager, "MGR-001");
        SeedUser(_studentId, RoleType.Student, "STD-001", "Alice");
        SeedUser(_otherStudentId, RoleType.Student, "STD-002", "Bob");
        SeedProgram();
        SeedClass(status: ClassStatus.Open);
        SeedEnrollment(_studentId, enrolledAt: _now.AddDays(-2));
        SeedEnrollment(_otherStudentId, enrolledAt: _now.AddDays(-1));
        var sut = CreateSut(_managerId);

        var result = await sut.GetClassWithStudentsAsync(_classId);

        Assert.Equal(2, result.Students!.Count);
        Assert.Equal("Alice", result.Students[0].StudentName);
        Assert.Equal(2, result.SeatsTaken);
    }

    [Fact]
    public async Task GetWithStudents_AllowsEnrolledStudent()
    {
        SeedUser(_studentId, RoleType.Student, "STD-001");
        SeedProgram();
        SeedClass();
        SeedEnrollment(_studentId);
        var sut = CreateSut(_studentId);

        var result = await sut.GetClassWithStudentsAsync(_classId);

        Assert.Single(result.Students!);
    }

    [Fact]
    public async Task GetWithStudents_ForbidsUnenrolledStudent()
    {
        SeedUser(_studentId, RoleType.Student, "STD-001");
        SeedProgram();
        SeedClass();
        var sut = CreateSut(_studentId);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            sut.GetClassWithStudentsAsync(_classId));
    }

    [Fact]
    public async Task GetWithStudents_AllowsOwningMentor_ForbidsParent()
    {
        SeedUser(_mentorId, RoleType.Mentor, "MNT-001");
        SeedUser(_parentId, RoleType.Parent, "PAR-001");
        SeedProgram();
        SeedClass(mentorId: _mentorId);
        SeedEnrollment(_studentId);
        SeedUser(_studentId, RoleType.Student, "STD-001");

        var mentorSut = CreateSut(_mentorId);
        var roster = await mentorSut.GetClassWithStudentsAsync(_classId);
        Assert.Single(roster.Students!);

        var parentSut = CreateSut(_parentId);
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            parentSut.GetClassWithStudentsAsync(_classId));
    }

    // ── GetClassWithSessionsAsync ─────────────────────────────────────────────

    [Fact]
    public async Task GetWithSessions_ReturnsOrderedSessions()
    {
        SeedProgram();
        SeedClass(status: ClassStatus.Open);
        SeedEnrollment(_studentId);
        _db.ClassSessions.Seed(
            new ClassSession
            {
                Id = _sessionId,
                ClassId = _classId,
                ModuleId = Guid.NewGuid(),
                Title = "Later",
                SessionKind = SessionKind.Lesson,
                StartTime = _now.AddDays(3),
                EndTime = _now.AddDays(3).AddHours(2),
                Status = ClassSessionStatus.Scheduled,
                CreatedAt = _now,
                IsDeleted = false,
            },
            new ClassSession
            {
                Id = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"),
                ClassId = _classId,
                ModuleId = Guid.NewGuid(),
                Title = "Earlier",
                SessionKind = SessionKind.Lesson,
                StartTime = _now.AddDays(1),
                EndTime = _now.AddDays(1).AddHours(2),
                Status = ClassSessionStatus.Scheduled,
                CreatedAt = _now,
                IsDeleted = false,
            },
            new ClassSession
            {
                Id = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd"),
                ClassId = _classId,
                ModuleId = Guid.NewGuid(),
                Title = "Deleted",
                SessionKind = SessionKind.Lesson,
                StartTime = _now.AddDays(2),
                EndTime = _now.AddDays(2).AddHours(1),
                Status = ClassSessionStatus.Scheduled,
                IsDeleted = true,
            });
        var sut = CreateSut();

        var result = await sut.GetClassWithSessionsAsync(_classId);

        Assert.Equal(2, result.Sessions.Count);
        Assert.Equal("Earlier", result.Sessions[0].Title);
        Assert.Equal(1, result.SeatsTaken);
    }

    [Fact]
    public async Task GetWithSessions_Throws_WhenClassMissing()
    {
        var sut = CreateSut();

        await Assert.ThrowsAsync<NotFoundException>(() =>
            sut.GetClassWithSessionsAsync(_classId));
    }

    // ── CreateClassAsync ──────────────────────────────────────────────────────

    [Fact]
    public async Task Create_PersistsClass_WithSkills_AndPublishes()
    {
        SeedProgram();
        SeedUser(_mentorId, RoleType.Mentor, "MNT-001");
        SeedSkill();
        var sut = CreateSut();

        var result = await sut.CreateClassAsync(BuildCreateRequest(
            mentorId: _mentorId,
            skillIds: [_skillId]));

        Assert.Equal("CLS-NEW", result.Code);
        Assert.Equal("New Cohort", result.Name);
        Assert.Equal(ClassStatus.Draft, result.Status);
        Assert.Equal(_mentorId, result.MentorId);
        Assert.Equal("Weekends", result.ScheduleSummary);
        Assert.Single(result.RequiredSkills);
        Assert.Single(_db.Classes.Items);
        Assert.Single(_db.ClassSkills.Items);
        _notificationPublisher.Verify(
            n => n.PublishAsync(It.IsAny<NotificationCommand>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Create_Throws_WhenDuplicateCode()
    {
        SeedProgram();
        SeedClass(code: "CLS-NEW");
        var sut = CreateSut();

        await Assert.ThrowsAsync<ConflictException>(() =>
            sut.CreateClassAsync(BuildCreateRequest()));
    }

    [Fact]
    public async Task Create_Throws_WhenProgramMissing()
    {
        var sut = CreateSut();

        await Assert.ThrowsAsync<NotFoundException>(() =>
            sut.CreateClassAsync(BuildCreateRequest()));
    }

    [Fact]
    public async Task Create_Throws_WhenCodeMissing()
    {
        SeedProgram();
        var sut = CreateSut();
        var request = BuildCreateRequest(code: "  ");

        await Assert.ThrowsAsync<BadRequestException>(() =>
            sut.CreateClassAsync(request));
    }

    [Fact]
    public async Task Create_Throws_WhenSkillMissing()
    {
        SeedProgram();
        var sut = CreateSut();

        await Assert.ThrowsAsync<NotFoundException>(() =>
            sut.CreateClassAsync(BuildCreateRequest(skillIds: [Guid.NewGuid()])));
    }

    [Fact]
    public async Task Create_Throws_WhenMentorNotEligible()
    {
        SeedProgram();
        SeedUser(_studentId, RoleType.Student, "STD-001");
        var sut = CreateSut();

        await Assert.ThrowsAsync<BadRequestException>(() =>
            sut.CreateClassAsync(BuildCreateRequest(mentorId: _studentId)));
    }

    // ── UpdateClassAsync ──────────────────────────────────────────────────────

    [Fact]
    public async Task Update_ChangesFields_AndPublishes()
    {
        SeedProgram();
        SeedProgram(_otherProgramId);
        SeedClass(status: ClassStatus.Draft);
        SeedSkill();
        var sut = CreateSut();

        var result = await sut.UpdateClassAsync(_classId, new UpdateClassRequestDto
        {
            Code = "CLS-UPD",
            Name = "  Updated  ",
            ProgramId = _otherProgramId,
            StartDate = _now.AddDays(5),
            EndDate = _now.AddDays(50),
            MaxCapacity = 10,
            MinHoursBeforeAssignmentJoin = 12,
            ScheduleSummary = "  Evenings  ",
            RequiredSkillIds = [_skillId],
        });

        Assert.Equal("CLS-UPD", result.Code);
        Assert.Equal("Updated", result.Name);
        Assert.Equal(_otherProgramId, result.ProgramId);
        Assert.Equal(10, result.MaxCapacity);
        Assert.Equal(12, result.MinHoursBeforeAssignmentJoin);
        Assert.Equal("Evenings", result.ScheduleSummary);
        Assert.Single(result.RequiredSkills);
        _notificationPublisher.Verify(
            n => n.PublishAsync(It.IsAny<NotificationCommand>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Update_NoChanges_SkipsPublish()
    {
        SeedProgram();
        SeedClass();
        var sut = CreateSut();

        var result = await sut.UpdateClassAsync(_classId, new UpdateClassRequestDto());

        Assert.Equal(_classId, result.Id);
        _notificationPublisher.Verify(
            n => n.PublishAsync(It.IsAny<NotificationCommand>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Update_AssignsMentor_ReconcilesPendingRequests()
    {
        SeedProgram();
        SeedUser(_managerId, RoleType.Manager, "MGR-001");
        SeedUser(_mentorId, RoleType.Mentor, "MNT-001");
        SeedUser(_otherMentorId, RoleType.Mentor, "MNT-002");
        SeedClass(status: ClassStatus.Draft, mentorId: null);
        var winnerRequestId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa01");
        var loserRequestId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa02");
        _db.ClassMentorRequests.Seed(
            new ClassMentorRequest
            {
                Id = winnerRequestId,
                ClassId = _classId,
                MentorId = _mentorId,
                Status = ClassMentorRequestStatus.Pending,
                IsDeleted = false,
            },
            new ClassMentorRequest
            {
                Id = loserRequestId,
                ClassId = _classId,
                MentorId = _otherMentorId,
                Status = ClassMentorRequestStatus.Pending,
                IsDeleted = false,
            });
        var sut = CreateSut(_managerId);

        var result = await sut.UpdateClassAsync(_classId, new UpdateClassRequestDto
        {
            MentorId = _mentorId,
        });

        Assert.Equal(_mentorId, result.MentorId);
        Assert.Equal(ClassMentorRequestStatus.Approved, _db.ClassMentorRequests.Items.Single(r => r.Id == winnerRequestId).Status);
        Assert.Equal(ClassMentorRequestStatus.Rejected, _db.ClassMentorRequests.Items.Single(r => r.Id == loserRequestId).Status);
        _notificationPublisher.Verify(
            n => n.PublishManyAsync(It.IsAny<IReadOnlyList<NotificationCommand>>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Update_Throws_WhenStatusViaPatch()
    {
        SeedProgram();
        SeedClass();
        var sut = CreateSut();

        await Assert.ThrowsAsync<BadRequestException>(() =>
            sut.UpdateClassAsync(_classId, new UpdateClassRequestDto { Status = ClassStatus.Open }));
    }

    [Fact]
    public async Task Update_Throws_WhenDuplicateCode()
    {
        SeedProgram();
        SeedClass(code: "CLS-001");
        SeedClass(
            id: Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee"),
            code: "CLS-002");
        var sut = CreateSut();

        await Assert.ThrowsAsync<ConflictException>(() =>
            sut.UpdateClassAsync(_classId, new UpdateClassRequestDto { Code = "CLS-002" }));
    }

    [Fact]
    public async Task Update_Throws_WhenCapacityBelowEnrollment()
    {
        SeedProgram();
        SeedClass(maxCapacity: 5);
        SeedEnrollment(_studentId);
        SeedEnrollment(_otherStudentId);
        var sut = CreateSut();

        await Assert.ThrowsAsync<BadRequestException>(() =>
            sut.UpdateClassAsync(_classId, new UpdateClassRequestDto { MaxCapacity = 1 }));
    }

    // ── Status transitions ────────────────────────────────────────────────────

    [Fact]
    public async Task MarkReadyForMentor_TransitionsDraftWhenScheduleCoversCurriculum()
    {
        SeedProgram();
        SeedClass(status: ClassStatus.Draft, mentorId: null);
        SeedSchedulableCurriculum(liveActivityCount: 1);
        SeedCoveringSessions(1);
        var sut = CreateSut();

        var result = await sut.MarkReadyForMentorAsync(_classId);

        Assert.Equal(ClassStatus.ReadyForMentor, result.Status);
    }

    [Fact]
    public async Task MarkReadyForMentor_Throws_WhenNoScheduleGenerated()
    {
        SeedProgram();
        SeedClass(status: ClassStatus.Draft);
        SeedSchedulableCurriculum(liveActivityCount: 1);
        var sut = CreateSut();

        var ex = await Assert.ThrowsAsync<BadRequestException>(() =>
            sut.MarkReadyForMentorAsync(_classId));
        Assert.Contains("schedule", ex.Message);
    }

    [Fact]
    public async Task Open_TransitionsReadyForMentorToOpen()
    {
        SeedProgram();
        SeedClass(status: ClassStatus.ReadyForMentor, mentorId: _mentorId);
        SeedSchedulableCurriculum(liveActivityCount: 1);
        SeedCoveringSessions(1);
        var sut = CreateSut();

        var result = await sut.OpenClassAsync(_classId);

        Assert.Equal(ClassStatus.Open, result.Status);
        _notificationPublisher.Verify(
            n => n.PublishAsync(It.IsAny<NotificationCommand>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Open_Throws_WhenStillDraft()
    {
        SeedProgram();
        SeedClass(status: ClassStatus.Draft, mentorId: _mentorId);
        SeedSchedulableCurriculum(liveActivityCount: 1);
        SeedCoveringSessions(1);
        var sut = CreateSut();

        var ex = await Assert.ThrowsAsync<BadRequestException>(() =>
            sut.OpenClassAsync(_classId));
        Assert.Contains("Draft", ex.Message);
    }

    [Fact]
    public async Task Open_Throws_WhenInvalidTransition()
    {
        SeedProgram();
        SeedClass(status: ClassStatus.InProgress);
        var sut = CreateSut();

        await Assert.ThrowsAsync<BadRequestException>(() =>
            sut.OpenClassAsync(_classId));
    }

    [Fact]
    public async Task Open_Throws_WhenNoMentorAssigned()
    {
        SeedProgram();
        SeedClass(status: ClassStatus.ReadyForMentor);
        SeedSchedulableCurriculum(liveActivityCount: 1);
        SeedCoveringSessions(1);
        var sut = CreateSut();

        var ex = await Assert.ThrowsAsync<BadRequestException>(() =>
            sut.OpenClassAsync(_classId));
        Assert.Contains("mentor", ex.Message);
    }

    [Fact]
    public async Task Open_Throws_WhenNoScheduleGenerated()
    {
        SeedProgram();
        SeedClass(status: ClassStatus.ReadyForMentor, mentorId: _mentorId);
        SeedSchedulableCurriculum(liveActivityCount: 1);
        var sut = CreateSut();

        var ex = await Assert.ThrowsAsync<BadRequestException>(() =>
            sut.OpenClassAsync(_classId));
        Assert.Contains("schedule", ex.Message);
    }

    [Fact]
    public async Task Open_Throws_WhenScheduleDoesNotCoverCurriculum()
    {
        SeedProgram();
        SeedClass(status: ClassStatus.ReadyForMentor, mentorId: _mentorId);
        SeedSchedulableCurriculum(liveActivityCount: 2);
        SeedCoveringSessions(1);
        var sut = CreateSut();

        var ex = await Assert.ThrowsAsync<BadRequestException>(() =>
            sut.OpenClassAsync(_classId));
        Assert.Contains("no longer matches the curriculum", ex.Message);
    }

    [Fact]
    public async Task Start_TransitionsOpenToInProgress()
    {
        SeedProgram();
        SeedClass(status: ClassStatus.Open, mentorId: _mentorId);
        SeedSchedulableCurriculum(liveActivityCount: 1);
        SeedCoveringSessions(1);
        var sut = CreateSut();

        var result = await sut.StartClassAsync(_classId);

        Assert.Equal(ClassStatus.InProgress, result.Status);
    }

    [Fact]
    public async Task Start_Throws_WhenScheduleNoLongerCoversCurriculum()
    {
        // Curriculum changed while the class was open but still empty — the stale
        // schedule must be fixed before the class can start.
        SeedProgram();
        SeedClass(status: ClassStatus.Open, mentorId: _mentorId);
        SeedSchedulableCurriculum(liveActivityCount: 2);
        SeedCoveringSessions(1);
        var sut = CreateSut();

        await Assert.ThrowsAsync<BadRequestException>(() =>
            sut.StartClassAsync(_classId));
    }

    [Fact]
    public async Task Complete_TransitionsInProgressToCompleted()
    {
        SeedProgram();
        SeedClass(status: ClassStatus.InProgress);
        var sut = CreateSut();

        var result = await sut.CompleteClassAsync(_classId);

        Assert.Equal(ClassStatus.Completed, result.Status);
    }

    // ── Auto-start ────────────────────────────────────────────────────────────

    [Fact]
    public async Task TryAutoStart_StartsWhenFullAndStartDateReached()
    {
        SeedProgram();
        SeedClass(
            status: ClassStatus.Open,
            maxCapacity: 2,
            startDate: _now.AddHours(-1),
            endDate: _now.AddDays(20));
        SeedSchedulableCurriculum(liveActivityCount: 1);
        SeedCoveringSessions(1);
        SeedEnrollment(_studentId);
        SeedEnrollment(_otherStudentId);
        var sut = CreateSut();

        await sut.TryAutoStartClassIfReadyAsync(_classId);

        Assert.Equal(ClassStatus.InProgress, _db.Classes.Items[0].Status);
        _notificationPublisher.Verify(
            n => n.PublishAsync(It.IsAny<NotificationCommand>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task TryAutoStart_NoOp_WhenNotReady()
    {
        SeedProgram();
        SeedClass(status: ClassStatus.Open, maxCapacity: 5, startDate: _now.AddDays(2));
        SeedEnrollment(_studentId);
        var sut = CreateSut();

        await sut.TryAutoStartClassIfReadyAsync(_classId);

        Assert.Equal(ClassStatus.Open, _db.Classes.Items[0].Status);
        _notificationPublisher.Verify(
            n => n.PublishAsync(It.IsAny<NotificationCommand>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task AutoStartEligible_StartsMatchingOpenClasses()
    {
        SeedProgram();
        SeedClass(
            status: ClassStatus.Open,
            maxCapacity: 1,
            startDate: _now.AddHours(-2),
            endDate: _now.AddDays(10));
        SeedSchedulableCurriculum(liveActivityCount: 1);
        SeedCoveringSessions(1);
        SeedEnrollment(_studentId);
        SeedClass(
            id: Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff"),
            code: "CLS-WAIT",
            status: ClassStatus.Open,
            maxCapacity: 1,
            startDate: _now.AddDays(3),
            endDate: _now.AddDays(20));
        SeedEnrollment(_otherStudentId, classId: Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff"));
        var sut = CreateSut();

        var started = await sut.AutoStartEligibleOpenClassesAsync();

        Assert.Equal(1, started);
        Assert.Equal(ClassStatus.InProgress, _db.Classes.Items.Single(c => c.Id == _classId).Status);
        _notificationPublisher.Verify(
            n => n.PublishManyAsync(It.IsAny<IReadOnlyList<NotificationCommand>>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Create_Throws_WhenStartDateWithinLeadTime()
    {
        SeedProgram();
        var sut = CreateSut();
        var request = BuildCreateRequest();
        request.StartDate = _now.AddDays(3);
        request.EndDate = _now.AddDays(40);

        var ex = await Assert.ThrowsAsync<BadRequestException>(() => sut.CreateClassAsync(request));
        Assert.Contains("14 days", ex.Message);
    }

    [Fact]
    public async Task Open_Throws_WhenStartDateAlreadyPassed()
    {
        SeedProgram();
        SeedClass(status: ClassStatus.ReadyForMentor, mentorId: _mentorId, startDate: _now.AddDays(-2));
        var sut = CreateSut();

        var ex = await Assert.ThrowsAsync<BadRequestException>(() => sut.OpenClassAsync(_classId));
        Assert.Contains("already passed", ex.Message);
    }

    [Fact]
    public async Task Update_Throws_WhenDateChangeOrphansSessions()
    {
        SeedProgram();
        SeedClass(status: ClassStatus.Draft);
        _db.ClassSessions.Seed(new ClassSession
        {
            Id = Guid.NewGuid(),
            ClassId = _classId,
            ModuleId = Guid.NewGuid(),
            Title = "Lab 1",
            StartTime = _now.AddDays(10),
            EndTime = _now.AddDays(10).AddHours(2),
            Status = ClassSessionStatus.Scheduled,
            IsDeleted = false,
        });
        var sut = CreateSut();

        var ex = await Assert.ThrowsAsync<BadRequestException>(() =>
            sut.UpdateClassAsync(_classId, new UpdateClassRequestDto { EndDate = _now.AddDays(5) }));
        Assert.Contains("outside the new class date range", ex.Message);
    }

    [Fact]
    public async Task Update_Succeeds_WhenDateChangeKeepsSessionsCovered()
    {
        SeedProgram();
        SeedClass(status: ClassStatus.Draft);
        _db.ClassSessions.Seed(new ClassSession
        {
            Id = Guid.NewGuid(),
            ClassId = _classId,
            ModuleId = Guid.NewGuid(),
            Title = "Lab 1",
            StartTime = _now.AddDays(10),
            EndTime = _now.AddDays(10).AddHours(2),
            Status = ClassSessionStatus.Scheduled,
            IsDeleted = false,
        });
        var sut = CreateSut();

        var result = await sut.UpdateClassAsync(_classId, new UpdateClassRequestDto { EndDate = _now.AddDays(20) });

        Assert.Equal(_now.AddDays(20), result.EndDate);
    }

    [Fact]
    public async Task AutoStartEligible_SkipsClass_WhenScheduleStale()
    {
        // Curriculum gained an activity while the class was open but empty; the class
        // then filled up and reached its start date. Auto-start must not put it
        // InProgress on a stale schedule — the manager has to regenerate first.
        SeedProgram();
        SeedClass(
            status: ClassStatus.Open,
            maxCapacity: 1,
            startDate: _now.AddHours(-2),
            endDate: _now.AddDays(10));
        SeedSchedulableCurriculum(liveActivityCount: 2);
        SeedCoveringSessions(1);
        SeedEnrollment(_studentId);
        var sut = CreateSut();

        var started = await sut.AutoStartEligibleOpenClassesAsync();

        Assert.Equal(0, started);
        Assert.Equal(ClassStatus.Open, _db.Classes.Items.Single(c => c.Id == _classId).Status);
    }

    [Fact]
    public async Task TryAutoStart_SkipsClass_WhenScheduleStale()
    {
        SeedProgram();
        SeedClass(
            status: ClassStatus.Open,
            maxCapacity: 2,
            startDate: _now.AddHours(-1),
            endDate: _now.AddDays(20));
        SeedSchedulableCurriculum(liveActivityCount: 2);
        SeedCoveringSessions(1);
        SeedEnrollment(_studentId);
        SeedEnrollment(_otherStudentId);
        var sut = CreateSut();

        await sut.TryAutoStartClassIfReadyAsync(_classId);

        Assert.Equal(ClassStatus.Open, _db.Classes.Items.Single(c => c.Id == _classId).Status);
    }

    [Fact]
    public async Task ResolveSchedule_ReturnsIdle_WhenNoOpenClasses()
    {
        SeedProgram();
        SeedClass(status: ClassStatus.Draft);
        var sut = CreateSut();

        var schedule = await sut.ResolveOpenClassAutoStartScheduleAsync();

        Assert.False(schedule.ShouldRunAutoStart);
        Assert.Equal("Idle", schedule.Reason);
    }

    [Fact]
    public async Task ResolveSchedule_ReturnsWaitingForCapacity()
    {
        SeedProgram();
        SeedClass(status: ClassStatus.Open, maxCapacity: 5, startDate: _now.AddDays(-1));
        SeedEnrollment(_studentId);
        var sut = CreateSut();

        var schedule = await sut.ResolveOpenClassAutoStartScheduleAsync();

        Assert.False(schedule.ShouldRunAutoStart);
        Assert.Equal("WaitingForCapacity", schedule.Reason);
    }

    [Fact]
    public async Task ResolveSchedule_ReturnsWaitingForStartDate()
    {
        SeedProgram();
        SeedClass(
            status: ClassStatus.Open,
            maxCapacity: 1,
            startDate: _now.AddHours(2),
            endDate: _now.AddDays(20));
        SeedEnrollment(_studentId);
        var sut = CreateSut();

        var schedule = await sut.ResolveOpenClassAutoStartScheduleAsync();

        Assert.False(schedule.ShouldRunAutoStart);
        Assert.Equal("WaitingForStartDate", schedule.Reason);
        Assert.True(schedule.NextDelay <= TimeSpan.FromHours(2).Add(TimeSpan.FromMinutes(1)));
    }

    [Fact]
    public async Task ResolveSchedule_ReturnsReadyToStart()
    {
        SeedProgram();
        SeedClass(
            status: ClassStatus.Open,
            maxCapacity: 1,
            startDate: _now.AddHours(-1),
            endDate: _now.AddDays(20));
        SeedEnrollment(_studentId);
        var sut = CreateSut();

        var schedule = await sut.ResolveOpenClassAutoStartScheduleAsync();

        Assert.True(schedule.ShouldRunAutoStart);
        Assert.Equal("ReadyToStart", schedule.Reason);
    }

    // ── DeleteClassAsync ──────────────────────────────────────────────────────

    [Fact]
    public async Task Delete_SoftDeletesClassAndSessions_AsManager()
    {
        SeedUser(_managerId, RoleType.Manager, "MGR-001");
        SeedProgram();
        SeedClass(status: ClassStatus.Draft);
        _db.ClassSessions.Seed(new ClassSession
        {
            Id = _sessionId,
            ClassId = _classId,
            ModuleId = Guid.NewGuid(),
            Title = "Session",
            SessionKind = SessionKind.Lesson,
            StartTime = _now.AddDays(1),
            EndTime = _now.AddDays(1).AddHours(2),
            Status = ClassSessionStatus.Scheduled,
            IsDeleted = false,
        });
        var sut = CreateSut(_managerId);

        await sut.DeleteClassAsync(_classId);

        Assert.True(_db.Classes.Items[0].IsDeleted);
        Assert.True(_db.ClassSessions.Items[0].IsDeleted);
    }

    [Fact]
    public async Task Delete_Throws_WhenNotManager()
    {
        SeedUser(_mentorId, RoleType.Mentor, "MNT-001");
        SeedProgram();
        SeedClass(status: ClassStatus.Draft);
        var sut = CreateSut(_mentorId);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            sut.DeleteClassAsync(_classId));
    }

    [Fact]
    public async Task Delete_Throws_WhenInProgress()
    {
        SeedUser(_managerId, RoleType.Manager, "MGR-001");
        SeedProgram();
        SeedClass(status: ClassStatus.InProgress);
        var sut = CreateSut(_managerId);

        await Assert.ThrowsAsync<BadRequestException>(() =>
            sut.DeleteClassAsync(_classId));
    }

    [Fact]
    public async Task Delete_Throws_WhenOpenHasStudents()
    {
        SeedUser(_managerId, RoleType.Manager, "MGR-001");
        SeedProgram();
        SeedClass(status: ClassStatus.Open);
        SeedEnrollment(_studentId);
        var sut = CreateSut(_managerId);

        await Assert.ThrowsAsync<ConflictException>(() =>
            sut.DeleteClassAsync(_classId));
    }
}
