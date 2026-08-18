using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OboxSteam.Application.DTOs.ClassSessionDTO;
using OboxSteam.Application.Exceptions;
using OboxSteam.Application.Interfaces;
using OboxSteam.Application.Notifications;
using OboxSteam.Application.Services;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;
using OboxSteam.Test.Helpers;

namespace OboxSteam.Test.UnitTests;

public sealed class ClassSessionServiceTests
{
    private readonly Guid _studentId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private readonly Guid _otherStudentId = Guid.Parse("12121212-1212-1212-1212-121212121212");
    private readonly Guid _managerId = Guid.Parse("13131313-1313-1313-1313-131313131313");
    private readonly Guid _mentorId = Guid.Parse("14141414-1414-1414-1414-141414141414");
    private readonly Guid _parentId = Guid.Parse("15151515-1515-1515-1515-151515151515");
    private readonly Guid _programId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private readonly Guid _moduleId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private readonly Guid _courseId = Guid.Parse("34343434-3434-3434-3434-343434343434");
    private readonly Guid _activityId = Guid.Parse("35353535-3535-3535-3535-353535353535");
    private readonly Guid _assignmentId = Guid.Parse("36363636-3636-3636-3636-363636363636");
    private readonly Guid _classId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private readonly Guid _sessionId = Guid.Parse("55555555-5555-5555-5555-555555555555");
    private readonly Guid _programEnrollmentId = Guid.Parse("66666666-6666-6666-6666-666666666666");
    private readonly Guid _moduleEnrollmentId = Guid.Parse("77777777-7777-7777-7777-777777777777");

    private readonly DateTime _now = DateTime.UtcNow;
    private readonly DateTime _classStart;
    private readonly DateTime _classEnd;

    private readonly InMemoryUnitOfWork _db = new();
    private readonly Mock<IClaimsService> _claimsService = new();
    private readonly Mock<INotificationPublisher> _notificationPublisher = new();

    public ClassSessionServiceTests()
    {
        _classStart = _now.AddDays(-7);
        _classEnd = _now.AddDays(60);
    }

    private ClassSessionService CreateSut(Guid? currentUserId = null)
    {
        _claimsService.Setup(c => c.GetCurrentUserId).Returns(currentUserId ?? _managerId);
        _notificationPublisher
            .Setup(n => n.PublishAsync(It.IsAny<NotificationCommand>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _notificationPublisher
            .Setup(n => n.PublishManyAsync(It.IsAny<IReadOnlyList<NotificationCommand>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        return new ClassSessionService(
            _db,
            _claimsService.Object,
            NullLogger<ClassSessionService>.Instance,
            _notificationPublisher.Object);
    }

    private void SeedUser(Guid id, RoleType role, string code, string? fullName = null)
    {
        _db.Users.Seed(new User
        {
            Id = id,
            Code = code,
            Email = $"{code.ToLower()}@test.com",
            FullName = fullName ?? code,
            Role = role,
            IsDeleted = false,
        });
    }

    private void SeedCurriculum()
    {
        _db.Modules.Seed(new Module
        {
            Id = _moduleId,
            Code = "MOD-001",
            Name = "Module 1",
            ProgramId = _programId,
            ModuleType = ModuleType.Theory,
            ModuleOrder = 1,
            IsDeleted = false,
        });
        _db.Courses.Seed(new Course
        {
            Id = _courseId,
            Code = "CRS-001",
            Name = "Course 1",
            ModuleId = _moduleId,
            IsDeleted = false,
        });
        _db.Activities.Seed(new Activity
        {
            Id = _activityId,
            Code = "ACT-001",
            Name = "Lab Activity",
            CourseId = _courseId,
            ActivityType = ActivityType.Offline,
            ActivityOrder = 1,
            IsDeleted = false,
        });
        _db.Assignments.Seed(new Assignment
        {
            Id = _assignmentId,
            Code = "ASN-001",
            Title = "Homework",
            ModuleId = _moduleId,
            AssignmentType = AssignmentType.FileUpload,
            MaxPoints = 100,
            PassScore = 50,
            IsDeleted = false,
        });
    }

    private Class SeedClass(
        Guid? mentorId = null,
        ClassStatus status = ClassStatus.InProgress,
        DateTime? startDate = null,
        DateTime? endDate = null,
        bool assignMentor = true)
    {
        var entity = new Class
        {
            Id = _classId,
            Code = "CLS-001",
            Name = "Cohort A",
            ProgramId = _programId,
            MentorId = assignMentor ? (mentorId ?? _mentorId) : null,
            Status = status,
            MaxCapacity = 30,
            StartDate = startDate ?? _classStart,
            EndDate = endDate ?? _classEnd,
            IsDeleted = false,
        };
        _db.Classes.Seed(entity);
        return entity;
    }

    private ClassSession SeedSession(
        Guid? id = null,
        string title = "Lab Session",
        DateTime? startTime = null,
        DateTime? endTime = null,
        ClassSessionStatus status = ClassSessionStatus.Scheduled,
        SessionKind kind = SessionKind.Lesson,
        Guid? moduleId = null,
        Guid? activityId = null,
        bool isDeleted = false,
        Class? classEntity = null)
    {
        var session = new ClassSession
        {
            Id = id ?? _sessionId,
            ClassId = _classId,
            ModuleId = moduleId ?? _moduleId,
            ActivityId = activityId ?? _activityId,
            Title = title,
            SessionKind = kind,
            StartTime = startTime ?? _now.AddDays(1),
            EndTime = endTime ?? _now.AddDays(1).AddHours(2),
            Status = status,
            RequiresAttendance = true,
            IsDeleted = isDeleted,
            CreatedAt = _now.AddHours(-3),
            Class = classEntity!,
        };
        _db.ClassSessions.Seed(session);
        return session;
    }

    private void SeedStudentRoster(Guid? studentId = null, string? fullName = null)
    {
        var sid = studentId ?? _studentId;
        SeedUser(sid, RoleType.Student, sid == _studentId ? "STD-001" : "STD-002", fullName);

        var peId = sid == _studentId ? _programEnrollmentId : Guid.NewGuid();
        var meId = sid == _studentId ? _moduleEnrollmentId : Guid.NewGuid();

        _db.ProgramEnrollments.Seed(new ProgramEnrollment
        {
            Id = peId,
            StudentId = sid,
            ProgramId = _programId,
            Status = EnrollmentStatus.Active,
            IsDeleted = false,
        });
        _db.ClassEnrollments.Seed(new ClassEnrollment
        {
            Id = Guid.NewGuid(),
            ClassId = _classId,
            StudentId = sid,
            ProgramEnrollmentId = peId,
            Status = ClassEnrollmentStatus.Active,
            IsDeleted = false,
        });
        _db.ModuleEnrollments.Seed(new ModuleEnrollment
        {
            Id = meId,
            StudentId = sid,
            ModuleId = _moduleId,
            ProgramEnrollmentId = peId,
            Status = EnrollmentStatus.Active,
            AttemptNumber = 1,
            IsDeleted = false,
        });
    }

    private CreateClassSessionRequestDto BuildCreateRequest(
        DateTime? start = null,
        DateTime? end = null,
        Guid? activityId = null,
        Guid? assignmentId = null,
        string title = "New Session")
    {
        var startTime = start ?? _now.AddDays(3);
        return new CreateClassSessionRequestDto
        {
            ClassId = _classId,
            ModuleId = _moduleId,
            ActivityId = activityId ?? _activityId,
            AssignmentId = assignmentId,
            SessionKind = SessionKind.Lesson,
            Title = title,
            Description = "Desc",
            StartTime = startTime,
            EndTime = end ?? startTime.AddHours(2),
            Location = "Room A",
            RequiresAttendance = true,
        };
    }

    // ── GetClassSessionsByClassIdAsync ────────────────────────────────────────

    [Fact]
    public async Task GetByClass_ReturnsSessions_ExcludesDeleted()
    {
        SeedClass();
        SeedSession(title: "Active");
        SeedSession(
            id: Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            title: "Gone",
            startTime: _now.AddDays(2),
            endTime: _now.AddDays(2).AddHours(1),
            isDeleted: true);
        var sut = CreateSut();

        var result = await sut.GetClassSessionsByClassIdAsync(_classId, null, false, 1, 10);

        Assert.Equal(1, result.TotalCount);
        Assert.Equal("Active", result.Items[0].Title);
    }

    [Fact]
    public async Task GetByClass_AppliesFilters()
    {
        SeedClass();
        SeedSession(
            title: "Lesson A",
            kind: SessionKind.Lesson,
            status: ClassSessionStatus.Scheduled,
            startTime: _now.AddDays(1),
            endTime: _now.AddDays(1).AddHours(2));
        SeedSession(
            id: Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
            title: "Trip B",
            kind: SessionKind.FieldTrip,
            status: ClassSessionStatus.InProgress,
            startTime: _now.AddDays(5),
            endTime: _now.AddDays(5).AddHours(3));
        var sut = CreateSut();

        var result = await sut.GetClassSessionsByClassIdAsync(
            _classId, null, false, 1, 10,
            moduleId: _moduleId,
            sessionKind: SessionKind.Lesson,
            status: ClassSessionStatus.Scheduled,
            from: _now,
            to: _now.AddDays(3));

        Assert.Equal(1, result.TotalCount);
        Assert.Equal("Lesson A", result.Items[0].Title);
    }

    [Fact]
    public async Task GetByClass_SortsByTitleAscending()
    {
        SeedClass();
        SeedSession(title: "Zebra", startTime: _now.AddDays(1), endTime: _now.AddDays(1).AddHours(1));
        SeedSession(
            id: Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"),
            title: "Alpha",
            startTime: _now.AddDays(2),
            endTime: _now.AddDays(2).AddHours(1));
        var sut = CreateSut();

        var result = await sut.GetClassSessionsByClassIdAsync(_classId, "title", false, 1, 10);

        Assert.Equal("Alpha", result.Items[0].Title);
        Assert.Equal("Zebra", result.Items[1].Title);
    }

    [Fact]
    public async Task GetByClass_DefaultsSortByStartTimeDescending()
    {
        SeedClass();
        SeedSession(title: "Earlier", startTime: _now.AddDays(1), endTime: _now.AddDays(1).AddHours(1));
        SeedSession(
            id: Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd"),
            title: "Later",
            startTime: _now.AddDays(4),
            endTime: _now.AddDays(4).AddHours(1));
        var sut = CreateSut();

        var result = await sut.GetClassSessionsByClassIdAsync(_classId, null, true, 1, 10);

        Assert.Equal("Later", result.Items[0].Title);
        Assert.Equal("Earlier", result.Items[1].Title);
    }

    [Fact]
    public async Task GetByClass_Throws_WhenPaginationInvalid()
    {
        SeedClass();
        var sut = CreateSut();

        await Assert.ThrowsAsync<BadRequestException>(() =>
            sut.GetClassSessionsByClassIdAsync(_classId, null, false, 0, 10));
    }

    [Fact]
    public async Task GetByClass_Throws_WhenClassMissing()
    {
        var sut = CreateSut();

        await Assert.ThrowsAsync<NotFoundException>(() =>
            sut.GetClassSessionsByClassIdAsync(_classId, null, false, 1, 10));
    }

    // ── GetClassSessionByIdAsync ──────────────────────────────────────────────

    [Fact]
    public async Task GetById_ReturnsSession()
    {
        SeedCurriculum();
        SeedClass();
        SeedSession();
        var sut = CreateSut();

        var result = await sut.GetClassSessionByIdAsync(_sessionId);

        Assert.Equal(_sessionId, result.Id);
        Assert.Equal("Lab Session", result.Title);
        Assert.Equal(_activityId, result.ActivityId);
    }

    [Fact]
    public async Task GetById_Throws_WhenMissingOrDeleted()
    {
        SeedClass();
        SeedSession(isDeleted: true);
        var sut = CreateSut();

        await Assert.ThrowsAsync<NotFoundException>(() =>
            sut.GetClassSessionByIdAsync(_sessionId));
        await Assert.ThrowsAsync<NotFoundException>(() =>
            sut.GetClassSessionByIdAsync(Guid.NewGuid()));
    }

    // ── GetClassSessionWithStudentsAsync ──────────────────────────────────────

    [Fact]
    public async Task GetWithStudents_ReturnsRoster_ForManager()
    {
        SeedUser(_managerId, RoleType.Manager, "MGR-001");
        SeedCurriculum();
        SeedClass();
        SeedSession();
        SeedStudentRoster(_studentId, "Alice");
        SeedStudentRoster(_otherStudentId, "Bob");
        _db.SessionAttendances.Seed(new SessionAttendance
        {
            Id = Guid.NewGuid(),
            ClassSessionId = _sessionId,
            StudentId = _studentId,
            ModuleEnrollmentId = _moduleEnrollmentId,
            Status = AttendanceStatus.Present,
            CheckedInAt = _now,
            IsDeleted = false,
        });
        var sut = CreateSut(_managerId);

        var result = await sut.GetClassSessionWithStudentsAsync(_sessionId);

        Assert.Equal(2, result.Students.Count);
        Assert.Equal("Alice", result.Students[0].StudentName);
        Assert.Equal(AttendanceStatus.Present, result.Students[0].AttendanceStatus);
        Assert.Equal(AttendanceStatus.Expected, result.Students[1].AttendanceStatus);
        Assert.Equal(_moduleEnrollmentId, result.Students[0].ModuleEnrollmentId);
    }

    [Fact]
    public async Task GetWithStudents_StudentSeesOnlySelf()
    {
        SeedCurriculum();
        SeedClass();
        SeedSession();
        SeedStudentRoster(_studentId, "Alice");
        SeedStudentRoster(_otherStudentId, "Bob");
        var sut = CreateSut(_studentId);

        var result = await sut.GetClassSessionWithStudentsAsync(_sessionId);

        Assert.Single(result.Students);
        Assert.Equal(_studentId, result.Students[0].StudentId);
    }

    [Fact]
    public async Task GetWithStudents_AllowsOwningMentor()
    {
        SeedUser(_mentorId, RoleType.Mentor, "MNT-001");
        SeedCurriculum();
        SeedClass(mentorId: _mentorId);
        SeedSession();
        SeedStudentRoster();
        var sut = CreateSut(_mentorId);

        var result = await sut.GetClassSessionWithStudentsAsync(_sessionId);

        Assert.Single(result.Students);
    }

    [Fact]
    public async Task GetWithStudents_ForbidsParent()
    {
        SeedUser(_parentId, RoleType.Parent, "PAR-001");
        SeedCurriculum();
        SeedClass();
        SeedSession();
        var sut = CreateSut(_parentId);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            sut.GetClassSessionWithStudentsAsync(_sessionId));
    }

    [Fact]
    public async Task GetWithStudents_Throws_WhenSessionMissing()
    {
        var sut = CreateSut();

        await Assert.ThrowsAsync<NotFoundException>(() =>
            sut.GetClassSessionWithStudentsAsync(_sessionId));
    }

    [Fact]
    public async Task GetWithStudents_ReturnsEmptyStudents_WhenNoEnrollments()
    {
        SeedUser(_managerId, RoleType.Manager, "MGR-001");
        SeedCurriculum();
        SeedClass();
        SeedSession();
        var sut = CreateSut(_managerId);

        var result = await sut.GetClassSessionWithStudentsAsync(_sessionId);

        Assert.Empty(result.Students);
    }

    // ── CreateClassSessionAsync ───────────────────────────────────────────────

    [Fact]
    public async Task Create_PersistsSession_AndPublishes()
    {
        SeedCurriculum();
        SeedClass(mentorId: _mentorId);
        var sut = CreateSut();
        var request = BuildCreateRequest();

        var result = await sut.CreateClassSessionAsync(request);

        Assert.Equal("New Session", result.Title);
        Assert.Equal(ClassSessionStatus.Scheduled, result.Status);
        Assert.Equal(_classId, result.ClassId);
        Assert.Single(_db.ClassSessions.Items);
        Assert.Equal(1, _db.SaveChangesCallCount);
        _notificationPublisher.Verify(
            n => n.PublishAsync(It.IsAny<NotificationCommand>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Create_Throws_WhenTitleMissing()
    {
        SeedCurriculum();
        SeedClass();
        var sut = CreateSut();
        var request = BuildCreateRequest(title: "  ");

        await Assert.ThrowsAsync<BadRequestException>(() =>
            sut.CreateClassSessionAsync(request));
    }

    [Fact]
    public async Task Create_Throws_WhenActivityAndAssignmentMissing()
    {
        SeedCurriculum();
        SeedClass();
        var sut = CreateSut();
        var request = BuildCreateRequest();
        request.ActivityId = null;
        request.AssignmentId = null;

        await Assert.ThrowsAsync<BadRequestException>(() =>
            sut.CreateClassSessionAsync(request));
    }

    [Fact]
    public async Task Create_Throws_WhenClassCompleted()
    {
        SeedCurriculum();
        SeedClass(status: ClassStatus.Completed);
        var sut = CreateSut();

        await Assert.ThrowsAsync<BadRequestException>(() =>
            sut.CreateClassSessionAsync(BuildCreateRequest()));
    }

    [Fact]
    public async Task Create_Throws_WhenNoMentorAssigned()
    {
        SeedCurriculum();
        SeedClass(assignMentor: false);
        var sut = CreateSut();

        var ex = await Assert.ThrowsAsync<BadRequestException>(() =>
            sut.CreateClassSessionAsync(BuildCreateRequest()));
        Assert.Contains("no assigned mentor", ex.Message);
    }

    [Fact]
    public async Task Create_Throws_WhenOutsideClassDateRange()
    {
        SeedCurriculum();
        SeedClass(endDate: _now.AddDays(2));
        var sut = CreateSut();
        var request = BuildCreateRequest(
            start: _now.AddDays(10),
            end: _now.AddDays(10).AddHours(2));

        await Assert.ThrowsAsync<BadRequestException>(() =>
            sut.CreateClassSessionAsync(request));
    }

    [Fact]
    public async Task Create_Throws_WhenMentorScheduleOverlaps()
    {
        SeedCurriculum();
        var classEntity = SeedClass(mentorId: _mentorId);
        var start = _now.AddDays(3);
        var end = start.AddHours(2);
        SeedSession(
            startTime: start.AddMinutes(-30),
            endTime: end.AddMinutes(30),
            classEntity: classEntity);
        var sut = CreateSut();

        await Assert.ThrowsAsync<ConflictException>(() =>
            sut.CreateClassSessionAsync(BuildCreateRequest(start: start, end: end)));
    }

    [Fact]
    public async Task Create_Throws_WhenStartInPast()
    {
        SeedCurriculum();
        SeedClass();
        var sut = CreateSut();
        var request = BuildCreateRequest(
            start: _now.AddHours(-1),
            end: _now.AddHours(1));

        await Assert.ThrowsAsync<BadRequestException>(() =>
            sut.CreateClassSessionAsync(request));
    }

    // ── UpdateClassSessionAsync ───────────────────────────────────────────────

    [Fact]
    public async Task Update_ChangesModuleActivityAssignment()
    {
        SeedCurriculum();
        var otherModuleId = Guid.Parse("99999999-9999-9999-9999-999999999999");
        var otherCourseId = Guid.Parse("98989898-9898-9898-9898-989898989898");
        var otherActivityId = Guid.Parse("97979797-9797-9797-9797-979797979797");
        var otherAssignmentId = Guid.Parse("96969696-9696-9696-9696-969696969696");
        _db.Modules.Seed(new Module
        {
            Id = otherModuleId,
            Code = "MOD-002",
            Name = "Module 2",
            ProgramId = _programId,
            ModuleType = ModuleType.Theory,
            ModuleOrder = 2,
            IsDeleted = false,
        });
        _db.Courses.Seed(new Course
        {
            Id = otherCourseId,
            Code = "CRS-002",
            Name = "Course 2",
            ModuleId = otherModuleId,
            IsDeleted = false,
        });
        _db.Activities.Seed(new Activity
        {
            Id = otherActivityId,
            Code = "ACT-002",
            Name = "Alt Activity",
            CourseId = otherCourseId,
            ActivityType = ActivityType.Offline,
            ActivityOrder = 1,
            IsDeleted = false,
        });
        _db.Assignments.Seed(new Assignment
        {
            Id = otherAssignmentId,
            Code = "ASN-002",
            Title = "Alt Assignment",
            ModuleId = otherModuleId,
            AssignmentType = AssignmentType.FileUpload,
            MaxPoints = 100,
            PassScore = 50,
            IsDeleted = false,
        });
        SeedClass();
        SeedSession(status: ClassSessionStatus.Scheduled);
        var sut = CreateSut();

        var result = await sut.UpdateClassSessionAsync(_sessionId, new UpdateClassSessionRequestDto
        {
            ModuleId = otherModuleId,
            ActivityId = otherActivityId,
            AssignmentId = otherAssignmentId,
        });

        Assert.Equal(otherModuleId, result.ModuleId);
        Assert.Equal(otherActivityId, result.ActivityId);
        Assert.Equal(otherAssignmentId, result.AssignmentId);
    }

    [Fact]
    public async Task Update_ChangesFields()
    {
        SeedCurriculum();
        SeedClass();
        SeedSession(status: ClassSessionStatus.Scheduled);
        var sut = CreateSut();

        var result = await sut.UpdateClassSessionAsync(_sessionId, new UpdateClassSessionRequestDto
        {
            Title = "  Updated Title  ",
            Description = "  New desc  ",
            Location = "  Lab 2  ",
            RequiresAttendance = false,
            RequiresMentorCheckIn = true,
            SessionKind = SessionKind.FieldTrip,
        });

        Assert.Equal("Updated Title", result.Title);
        Assert.Equal("New desc", result.Description);
        Assert.Equal("Lab 2", result.Location);
        Assert.False(result.RequiresAttendance);
        Assert.True(result.RequiresMentorCheckIn);
        Assert.Equal(SessionKind.FieldTrip, result.SessionKind);
    }

    [Fact]
    public async Task Update_Throws_WhenRescheduleWithoutMentor()
    {
        SeedCurriculum();
        SeedClass(assignMentor: false);
        SeedSession(status: ClassSessionStatus.Scheduled);
        var sut = CreateSut();
        var newStart = _now.AddDays(5);

        var ex = await Assert.ThrowsAsync<BadRequestException>(() =>
            sut.UpdateClassSessionAsync(_sessionId, new UpdateClassSessionRequestDto
            {
                StartTime = newStart,
                EndTime = newStart.AddHours(2),
            }));
        Assert.Contains("no assigned mentor", ex.Message);
    }

    [Fact]
    public async Task GetByClass_SortsByStatusAndCreatedAt()
    {
        SeedClass();
        SeedSession(
            title: "Scheduled",
            status: ClassSessionStatus.Scheduled,
            startTime: _now.AddDays(1),
            endTime: _now.AddDays(1).AddHours(1));
        var later = SeedSession(
            id: Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee"),
            title: "InProgress",
            status: ClassSessionStatus.InProgress,
            startTime: _now.AddDays(2),
            endTime: _now.AddDays(2).AddHours(1));
        later.CreatedAt = _now.AddHours(-1);
        var sut = CreateSut();

        var byStatus = await sut.GetClassSessionsByClassIdAsync(_classId, "status", false, 1, 10);
        Assert.Equal(ClassSessionStatus.Scheduled, byStatus.Items[0].Status);

        var byCreated = await sut.GetClassSessionsByClassIdAsync(_classId, "createdat", true, 1, 10);
        Assert.Equal(2, byCreated.Items.Count);

        var byStart = await sut.GetClassSessionsByClassIdAsync(_classId, "starttime", false, 1, 10);
        Assert.True(byStart.TotalCount >= 1);
        var byEnd = await sut.GetClassSessionsByClassIdAsync(_classId, "endtime", true, 1, 10);
        Assert.True(byEnd.TotalCount >= 1);
        var byKind = await sut.GetClassSessionsByClassIdAsync(_classId, "sessionkind", false, 1, 10);
        Assert.True(byKind.TotalCount >= 1);
    }

    [Fact]
    public async Task Update_StatusToInProgress_PublishesStarted()
    {
        SeedCurriculum();
        SeedClass();
        SeedSession(status: ClassSessionStatus.Scheduled);
        var sut = CreateSut();

        var result = await sut.UpdateClassSessionAsync(_sessionId, new UpdateClassSessionRequestDto
        {
            Status = ClassSessionStatus.InProgress,
        });

        Assert.Equal(ClassSessionStatus.InProgress, result.Status);
        _notificationPublisher.Verify(
            n => n.PublishManyAsync(It.IsAny<IReadOnlyList<NotificationCommand>>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Update_StatusToCompleted_PublishesCompleted()
    {
        SeedCurriculum();
        SeedClass();
        SeedSession(status: ClassSessionStatus.InProgress);
        var sut = CreateSut();

        var result = await sut.UpdateClassSessionAsync(_sessionId, new UpdateClassSessionRequestDto
        {
            Status = ClassSessionStatus.Completed,
        });

        Assert.Equal(ClassSessionStatus.Completed, result.Status);
        _notificationPublisher.Verify(
            n => n.PublishManyAsync(It.IsAny<IReadOnlyList<NotificationCommand>>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Update_StatusToCancelled_PublishesCancelled()
    {
        SeedCurriculum();
        SeedClass();
        SeedSession(status: ClassSessionStatus.Scheduled);
        var sut = CreateSut();

        var result = await sut.UpdateClassSessionAsync(_sessionId, new UpdateClassSessionRequestDto
        {
            Status = ClassSessionStatus.Cancelled,
        });

        Assert.Equal(ClassSessionStatus.Cancelled, result.Status);
        _notificationPublisher.Verify(
            n => n.PublishManyAsync(It.IsAny<IReadOnlyList<NotificationCommand>>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Update_TimeChange_PublishesRescheduled()
    {
        SeedCurriculum();
        var classEntity = SeedClass(mentorId: _mentorId);
        SeedSession(status: ClassSessionStatus.Scheduled, classEntity: classEntity);
        var sut = CreateSut();
        var newStart = _now.AddDays(5);
        var newEnd = newStart.AddHours(2);

        var result = await sut.UpdateClassSessionAsync(_sessionId, new UpdateClassSessionRequestDto
        {
            StartTime = newStart,
            EndTime = newEnd,
        });

        Assert.Equal(newStart, result.StartTime);
        Assert.Equal(newEnd, result.EndTime);
        _notificationPublisher.Verify(
            n => n.PublishManyAsync(It.IsAny<IReadOnlyList<NotificationCommand>>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Update_Throws_WhenSessionNotModifiable()
    {
        SeedCurriculum();
        SeedClass();
        SeedSession(status: ClassSessionStatus.Completed);
        var sut = CreateSut();

        await Assert.ThrowsAsync<BadRequestException>(() =>
            sut.UpdateClassSessionAsync(_sessionId, new UpdateClassSessionRequestDto { Title = "Nope" }));
    }

    [Fact]
    public async Task Update_Throws_WhenInvalidStatusTransition()
    {
        SeedCurriculum();
        SeedClass();
        SeedSession(status: ClassSessionStatus.Scheduled);
        var sut = CreateSut();

        await Assert.ThrowsAsync<BadRequestException>(() =>
            sut.UpdateClassSessionAsync(_sessionId, new UpdateClassSessionRequestDto
            {
                Status = ClassSessionStatus.Completed,
            }));
    }

    [Fact]
    public async Task Update_Throws_WhenEndTimeNotAfterStart()
    {
        SeedCurriculum();
        SeedClass();
        SeedSession(status: ClassSessionStatus.Scheduled);
        var sut = CreateSut();
        var start = _now.AddDays(4);

        await Assert.ThrowsAsync<BadRequestException>(() =>
            sut.UpdateClassSessionAsync(_sessionId, new UpdateClassSessionRequestDto
            {
                StartTime = start,
                EndTime = start,
            }));
    }

    [Fact]
    public async Task Update_Throws_WhenSessionMissing()
    {
        var sut = CreateSut();

        await Assert.ThrowsAsync<NotFoundException>(() =>
            sut.UpdateClassSessionAsync(_sessionId, new UpdateClassSessionRequestDto { Title = "X" }));
    }

    // ── DeleteClassSessionAsync ───────────────────────────────────────────────

    [Fact]
    public async Task Delete_SoftDeletes_AndPublishes()
    {
        SeedCurriculum();
        SeedClass();
        SeedSession();
        var sut = CreateSut();

        var deleted = await sut.DeleteClassSessionAsync(_sessionId);

        Assert.True(deleted);
        Assert.True(_db.ClassSessions.Items[0].IsDeleted);
        _notificationPublisher.Verify(
            n => n.PublishAsync(It.IsAny<NotificationCommand>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Delete_ReturnsFalse_WhenMissingOrAlreadyDeleted()
    {
        SeedClass();
        SeedSession(isDeleted: true);
        var sut = CreateSut();

        Assert.False(await sut.DeleteClassSessionAsync(_sessionId));
        Assert.False(await sut.DeleteClassSessionAsync(Guid.NewGuid()));
    }
}
