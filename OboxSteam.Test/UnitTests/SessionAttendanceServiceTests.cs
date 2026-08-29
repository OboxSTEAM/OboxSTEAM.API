using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OboxSteam.Application.DTOs.SessionAttendanceDTO;
using OboxSteam.Application.Exceptions;
using OboxSteam.Application.Interfaces;
using OboxSteam.Application.Notifications;
using OboxSteam.Application.Services;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;
using OboxSteam.Test.Helpers;

namespace OboxSteam.Test.UnitTests;

public sealed class SessionAttendanceServiceTests
{
    private readonly Guid _studentId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private readonly Guid _otherStudentId = Guid.Parse("12121212-1212-1212-1212-121212121212");
    private readonly Guid _managerId = Guid.Parse("13131313-1313-1313-1313-131313131313");
    private readonly Guid _mentorId = Guid.Parse("14141414-1414-1414-1414-141414141414");
    private readonly Guid _parentId = Guid.Parse("15151515-1515-1515-1515-151515151515");
    private readonly Guid _programId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private readonly Guid _moduleId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private readonly Guid _classId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private readonly Guid _sessionId = Guid.Parse("55555555-5555-5555-5555-555555555555");
    private readonly Guid _programEnrollmentId = Guid.Parse("66666666-6666-6666-6666-666666666666");
    private readonly Guid _moduleEnrollmentId = Guid.Parse("77777777-7777-7777-7777-777777777777");
    private readonly Guid _attendanceId = Guid.Parse("88888888-8888-8888-8888-888888888888");

    private readonly DateTime _now = new(2026, 7, 27, 10, 0, 0, DateTimeKind.Utc);

    private readonly InMemoryUnitOfWork _db = new();
    private readonly Mock<IClaimsService> _claimsService = new();
    private readonly Mock<ICurrentTime> _currentTime = new();
    private readonly Mock<INotificationPublisher> _notificationPublisher = new();

    private SessionAttendanceService CreateSut(Guid? currentUserId = null)
    {
        _claimsService.Setup(c => c.GetCurrentUserId).Returns(currentUserId ?? _managerId);
        _currentTime.Setup(t => t.GetCurrentTime()).Returns(_now);
        _notificationPublisher
            .Setup(n => n.PublishAsync(It.IsAny<NotificationCommand>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var lifecycle = new ProgramPurchaseLifecycle(
            _db,
            _currentTime.Object,
            _notificationPublisher.Object,
            NullLogger<ProgramPurchaseLifecycle>.Instance);

        return new SessionAttendanceService(
            _db,
            _claimsService.Object,
            _currentTime.Object,
            NullLogger<SessionAttendanceService>.Instance,
            _notificationPublisher.Object,
            lifecycle);
    }

    private void SeedUser(Guid id, RoleType role, string code)
    {
        _db.Users.Seed(new User
        {
            Id = id,
            Code = code,
            Email = $"{code.ToLower()}@test.com",
            Role = role,
            IsDeleted = false,
        });
    }

    private void SeedClass(Guid? mentorId = null)
    {
        _db.Classes.Seed(new Class
        {
            Id = _classId,
            Code = "CLS-001",
            Name = "Cohort A",
            ProgramId = _programId,
            MentorId = mentorId,
            Status = ClassStatus.InProgress,
            MaxCapacity = 30,
            StartDate = _now.AddDays(-7),
            EndDate = _now.AddDays(60),
            IsDeleted = false,
        });
    }

    private ClassSession SeedSession(
        bool requiresAttendance = true,
        DateTime? endTime = null,
        Guid? classId = null,
        bool isDeleted = false)
    {
        var session = new ClassSession
        {
            Id = _sessionId,
            ClassId = classId ?? _classId,
            ModuleId = _moduleId,
            Title = "Lab Session",
            SessionKind = SessionKind.LiveOnline,
            StartTime = _now.AddHours(-1),
            EndTime = endTime ?? _now.AddHours(2),
            RequiresAttendance = requiresAttendance,
            Status = ClassSessionStatus.InProgress,
            IsDeleted = isDeleted,
        };
        _db.ClassSessions.Seed(session);
        return session;
    }

    private void SeedStudentRoster(Guid? studentId = null)
    {
        var sid = studentId ?? _studentId;
        _db.ProgramEnrollments.Seed(new ProgramEnrollment
        {
            Id = _programEnrollmentId,
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
            ProgramEnrollmentId = _programEnrollmentId,
            Status = ClassEnrollmentStatus.Active,
            IsDeleted = false,
        });
        _db.ModuleEnrollments.Seed(new ModuleEnrollment
        {
            Id = _moduleEnrollmentId,
            StudentId = sid,
            ModuleId = _moduleId,
            ProgramEnrollmentId = _programEnrollmentId,
            Status = EnrollmentStatus.Active,
            AttemptNumber = 1,
            IsDeleted = false,
        });
    }

    private SessionAttendance SeedAttendance(
        Guid? studentId = null,
        AttendanceStatus status = AttendanceStatus.Expected,
        DateTime? createdAt = null,
        bool isDeleted = false)
    {
        var attendance = new SessionAttendance
        {
            Id = _attendanceId,
            ClassSessionId = _sessionId,
            StudentId = studentId ?? _studentId,
            ModuleEnrollmentId = _moduleEnrollmentId,
            Status = status,
            CheckedInAt = null,
            CreatedAt = createdAt ?? _now.AddDays(-1),
            IsDeleted = isDeleted,
        };
        _db.SessionAttendances.Seed(attendance);
        return attendance;
    }

    // ── GetSessionAttendancesByClassSessionIdAsync ────────────────────────────

    [Fact]
    public async Task GetBySession_ReturnsRoster_ForManager()
    {
        SeedUser(_managerId, RoleType.Manager, "MGR-001");
        SeedClass();
        SeedSession();
        _db.SessionAttendances.Seed(
            new SessionAttendance
            {
                Id = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
                ClassSessionId = _sessionId,
                StudentId = _studentId,
                ModuleEnrollmentId = _moduleEnrollmentId,
                Status = AttendanceStatus.Present,
                CreatedAt = _now.AddHours(-2),
                IsDeleted = false,
            },
            new SessionAttendance
            {
                Id = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
                ClassSessionId = _sessionId,
                StudentId = _otherStudentId,
                ModuleEnrollmentId = Guid.NewGuid(),
                Status = AttendanceStatus.Absent,
                CreatedAt = _now.AddHours(-1),
                IsDeleted = false,
            });
        var sut = CreateSut(_managerId);

        var result = await sut.GetSessionAttendancesByClassSessionIdAsync(
            _sessionId, null, true, 1, 10);

        Assert.Equal(2, result.TotalCount);
        Assert.Equal(2, result.Items.Count);
    }

    [Theory]
    [InlineData("status", false)]
    [InlineData("checkedinat", true)]
    [InlineData("studentid", false)]
    [InlineData("createdat", true)]
    [InlineData("xxx", false)]
    public async Task GetBySession_SortByColumns_ReturnsResults(string sortBy, bool desc)
    {
        SeedUser(_managerId, RoleType.Manager, "MGR-001");
        SeedClass();
        SeedSession();
        _db.SessionAttendances.Seed(new SessionAttendance
        {
            Id = _attendanceId,
            ClassSessionId = _sessionId,
            StudentId = _studentId,
            ModuleEnrollmentId = _moduleEnrollmentId,
            Status = AttendanceStatus.Present,
            CreatedAt = _now,
            IsDeleted = false,
        });
        var sut = CreateSut(_managerId);

        var r = await sut.GetSessionAttendancesByClassSessionIdAsync(
            _sessionId, sortBy, desc, 1, 10);

        Assert.True(r.TotalCount >= 1);
    }

    [Fact]
    public async Task GetBySession_StudentSeesOnlyOwnRecord()
    {
        SeedUser(_studentId, RoleType.Student, "STD-001");
        SeedClass();
        SeedSession();
        _db.SessionAttendances.Seed(
            new SessionAttendance
            {
                Id = Guid.NewGuid(),
                ClassSessionId = _sessionId,
                StudentId = _studentId,
                ModuleEnrollmentId = _moduleEnrollmentId,
                Status = AttendanceStatus.Present,
                CreatedAt = _now,
                IsDeleted = false,
            },
            new SessionAttendance
            {
                Id = Guid.NewGuid(),
                ClassSessionId = _sessionId,
                StudentId = _otherStudentId,
                ModuleEnrollmentId = Guid.NewGuid(),
                Status = AttendanceStatus.Absent,
                CreatedAt = _now,
                IsDeleted = false,
            });
        var sut = CreateSut(_studentId);

        var result = await sut.GetSessionAttendancesByClassSessionIdAsync(
            _sessionId, null, true, 1, 10);

        Assert.Equal(1, result.TotalCount);
        Assert.Equal(_studentId, result.Items[0].StudentId);
    }

    [Fact]
    public async Task GetBySession_FiltersByStatusAndStudentId_ForStaff()
    {
        SeedUser(_managerId, RoleType.Manager, "MGR-001");
        SeedClass();
        SeedSession();
        _db.SessionAttendances.Seed(
            new SessionAttendance
            {
                Id = Guid.NewGuid(),
                ClassSessionId = _sessionId,
                StudentId = _studentId,
                ModuleEnrollmentId = _moduleEnrollmentId,
                Status = AttendanceStatus.Present,
                CreatedAt = _now,
                IsDeleted = false,
            },
            new SessionAttendance
            {
                Id = Guid.NewGuid(),
                ClassSessionId = _sessionId,
                StudentId = _otherStudentId,
                ModuleEnrollmentId = Guid.NewGuid(),
                Status = AttendanceStatus.Absent,
                CreatedAt = _now,
                IsDeleted = false,
            });
        var sut = CreateSut(_managerId);

        var result = await sut.GetSessionAttendancesByClassSessionIdAsync(
            _sessionId, null, true, 1, 10,
            status: AttendanceStatus.Present,
            studentId: _studentId);

        Assert.Equal(1, result.TotalCount);
        Assert.Equal(AttendanceStatus.Present, result.Items[0].Status);
    }

    [Fact]
    public async Task GetBySession_SortsByStatusAscending()
    {
        SeedUser(_managerId, RoleType.Manager, "MGR-001");
        SeedClass();
        SeedSession();
        _db.SessionAttendances.Seed(
            new SessionAttendance
            {
                Id = Guid.NewGuid(),
                ClassSessionId = _sessionId,
                StudentId = _studentId,
                ModuleEnrollmentId = _moduleEnrollmentId,
                Status = AttendanceStatus.Present,
                CreatedAt = _now,
                IsDeleted = false,
            },
            new SessionAttendance
            {
                Id = Guid.NewGuid(),
                ClassSessionId = _sessionId,
                StudentId = _otherStudentId,
                ModuleEnrollmentId = Guid.NewGuid(),
                Status = AttendanceStatus.Absent,
                CreatedAt = _now,
                IsDeleted = false,
            });
        var sut = CreateSut(_managerId);

        var result = await sut.GetSessionAttendancesByClassSessionIdAsync(
            _sessionId, "status", false, 1, 10);

        // AttendanceStatus: Present=1, Absent=2 — ascending puts Present first
        Assert.Equal(AttendanceStatus.Present, result.Items[0].Status);
        Assert.Equal(AttendanceStatus.Absent, result.Items[1].Status);
    }

    [Fact]
    public async Task GetBySession_AllowsMentorWhoOwnsClass()
    {
        SeedUser(_mentorId, RoleType.Mentor, "MNT-001");
        SeedClass(mentorId: _mentorId);
        SeedSession();
        SeedAttendance();
        var sut = CreateSut(_mentorId);

        var result = await sut.GetSessionAttendancesByClassSessionIdAsync(
            _sessionId, null, true, 1, 10);

        Assert.Equal(1, result.TotalCount);
    }

    [Fact]
    public async Task GetBySession_ThrowsForbidden_WhenMentorDoesNotOwnClass()
    {
        SeedUser(_mentorId, RoleType.Mentor, "MNT-001");
        SeedClass(mentorId: Guid.NewGuid());
        SeedSession();
        var sut = CreateSut(_mentorId);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            sut.GetSessionAttendancesByClassSessionIdAsync(_sessionId, null, true, 1, 10));
    }

    [Fact]
    public async Task GetBySession_ThrowsForbidden_WhenParent()
    {
        SeedUser(_parentId, RoleType.Parent, "PAR-001");
        SeedClass();
        SeedSession();
        var sut = CreateSut(_parentId);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            sut.GetSessionAttendancesByClassSessionIdAsync(_sessionId, null, true, 1, 10));
    }

    [Fact]
    public async Task GetBySession_ThrowsNotFound_WhenSessionMissing()
    {
        SeedUser(_managerId, RoleType.Manager, "MGR-001");
        var sut = CreateSut(_managerId);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            sut.GetSessionAttendancesByClassSessionIdAsync(_sessionId, null, true, 1, 10));
    }

    [Fact]
    public async Task GetBySession_ThrowsBadRequest_WhenPaginationInvalid()
    {
        SeedUser(_managerId, RoleType.Manager, "MGR-001");
        var sut = CreateSut(_managerId);

        await Assert.ThrowsAsync<BadRequestException>(() =>
            sut.GetSessionAttendancesByClassSessionIdAsync(_sessionId, null, true, 0, 10));
    }

    [Fact]
    public async Task GetBySession_ThrowsUnauthorized_WhenUserIdEmpty()
    {
        SeedClass();
        SeedSession();
        var sut = CreateSut(Guid.Empty);

        await Assert.ThrowsAsync<UnauthorizedException>(() =>
            sut.GetSessionAttendancesByClassSessionIdAsync(_sessionId, null, true, 1, 10));
    }

    // ── UpdateSessionAttendanceAsync ──────────────────────────────────────────

    [Fact]
    public async Task Update_CreatesAttendance_WhenNoneExists()
    {
        SeedUser(_managerId, RoleType.Manager, "MGR-001");
        SeedUser(_studentId, RoleType.Student, "STD-001");
        SeedClass();
        SeedSession();
        SeedStudentRoster();
        var sut = CreateSut(_managerId);

        var result = await sut.UpdateSessionAttendanceAsync(
            _classId,
            _sessionId,
            _studentId,
            new UpdateSessionAttendanceRequestDto { Status = AttendanceStatus.Present });

        Assert.Equal(AttendanceStatus.Present, result.Status);
        Assert.Equal(_studentId, result.StudentId);
        Assert.Equal(_moduleEnrollmentId, result.ModuleEnrollmentId);
        Assert.Equal(_managerId, result.RecordedBy);
        Assert.Equal(_now, result.CheckedInAt);
        Assert.Single(_db.SessionAttendances.Items);
        Assert.Equal(1, _db.SaveChangesCallCount);
        _notificationPublisher.Verify(
            n => n.PublishAsync(It.IsAny<NotificationCommand>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Update_UpdatesExistingAttendance()
    {
        SeedUser(_managerId, RoleType.Manager, "MGR-001");
        SeedUser(_studentId, RoleType.Student, "STD-001");
        SeedClass();
        SeedSession();
        SeedStudentRoster();
        SeedAttendance(status: AttendanceStatus.Expected);
        var sut = CreateSut(_managerId);

        var result = await sut.UpdateSessionAttendanceAsync(
            _classId,
            _sessionId,
            _studentId,
            new UpdateSessionAttendanceRequestDto { Status = AttendanceStatus.Late });

        Assert.Equal(AttendanceStatus.Late, result.Status);
        Assert.Equal(_attendanceId, result.Id);
        Assert.Single(_db.SessionAttendances.Items);
        Assert.Equal(AttendanceStatus.Late, _db.SessionAttendances.Items[0].Status);
    }

    [Fact]
    public async Task Update_AllowsMentor_WhileSessionOngoing()
    {
        SeedUser(_mentorId, RoleType.Mentor, "MNT-001");
        SeedUser(_studentId, RoleType.Student, "STD-001");
        SeedClass(mentorId: _mentorId);
        SeedSession(endTime: _now.AddHours(1));
        SeedStudentRoster();
        var sut = CreateSut(_mentorId);

        var result = await sut.UpdateSessionAttendanceAsync(
            _classId,
            _sessionId,
            _studentId,
            new UpdateSessionAttendanceRequestDto { Status = AttendanceStatus.Present });

        Assert.Equal(AttendanceStatus.Present, result.Status);
        Assert.Equal(_mentorId, result.RecordedBy);
    }

    [Fact]
    public async Task Update_ThrowsForbidden_WhenMentorAfterSessionEnded()
    {
        SeedUser(_mentorId, RoleType.Mentor, "MNT-001");
        SeedUser(_studentId, RoleType.Student, "STD-001");
        SeedClass(mentorId: _mentorId);
        SeedSession(endTime: _now.AddHours(-1));
        SeedStudentRoster();
        var sut = CreateSut(_mentorId);

        var ex = await Assert.ThrowsAsync<ForbiddenException>(() =>
            sut.UpdateSessionAttendanceAsync(
                _classId,
                _sessionId,
                _studentId,
                new UpdateSessionAttendanceRequestDto { Status = AttendanceStatus.Present }));

        Assert.Contains("while the class session is ongoing", ex.Message);
    }

    [Fact]
    public async Task Update_ManagerCanUpdate_AfterSessionEnded()
    {
        SeedUser(_managerId, RoleType.Manager, "MGR-001");
        SeedUser(_studentId, RoleType.Student, "STD-001");
        SeedClass();
        SeedSession(endTime: _now.AddHours(-1));
        SeedStudentRoster();
        var sut = CreateSut(_managerId);

        var result = await sut.UpdateSessionAttendanceAsync(
            _classId,
            _sessionId,
            _studentId,
            new UpdateSessionAttendanceRequestDto { Status = AttendanceStatus.Absent });

        Assert.Equal(AttendanceStatus.Absent, result.Status);
    }

    [Fact]
    public async Task Update_ThrowsForbidden_WhenStudentUpdates()
    {
        SeedUser(_studentId, RoleType.Student, "STD-001");
        SeedClass();
        SeedSession();
        SeedStudentRoster();
        var sut = CreateSut(_studentId);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            sut.UpdateSessionAttendanceAsync(
                _classId,
                _sessionId,
                _studentId,
                new UpdateSessionAttendanceRequestDto { Status = AttendanceStatus.Present }));
    }

    [Fact]
    public async Task Update_ThrowsNotFound_WhenSessionMissing()
    {
        SeedUser(_managerId, RoleType.Manager, "MGR-001");
        var sut = CreateSut(_managerId);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            sut.UpdateSessionAttendanceAsync(
                _classId,
                _sessionId,
                _studentId,
                new UpdateSessionAttendanceRequestDto { Status = AttendanceStatus.Present }));
    }

    [Fact]
    public async Task Update_ThrowsNotFound_WhenSessionBelongsToOtherClass()
    {
        SeedUser(_managerId, RoleType.Manager, "MGR-001");
        SeedClass();
        SeedSession();
        var sut = CreateSut(_managerId);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            sut.UpdateSessionAttendanceAsync(
                Guid.NewGuid(),
                _sessionId,
                _studentId,
                new UpdateSessionAttendanceRequestDto { Status = AttendanceStatus.Present }));
    }

    [Fact]
    public async Task Update_ThrowsBadRequest_WhenSessionDoesNotRequireAttendance()
    {
        SeedUser(_managerId, RoleType.Manager, "MGR-001");
        SeedClass();
        SeedSession(requiresAttendance: false);
        var sut = CreateSut(_managerId);

        await Assert.ThrowsAsync<BadRequestException>(() =>
            sut.UpdateSessionAttendanceAsync(
                _classId,
                _sessionId,
                _studentId,
                new UpdateSessionAttendanceRequestDto { Status = AttendanceStatus.Present }));
    }

    [Fact]
    public async Task Update_ThrowsBadRequest_WhenStudentNotInClass()
    {
        SeedUser(_managerId, RoleType.Manager, "MGR-001");
        SeedUser(_studentId, RoleType.Student, "STD-001");
        SeedClass();
        SeedSession();
        var sut = CreateSut(_managerId);

        await Assert.ThrowsAsync<BadRequestException>(() =>
            sut.UpdateSessionAttendanceAsync(
                _classId,
                _sessionId,
                _studentId,
                new UpdateSessionAttendanceRequestDto { Status = AttendanceStatus.Present }));
    }

    [Fact]
    public async Task Update_ThrowsBadRequest_WhenNoActiveModuleEnrollment()
    {
        SeedUser(_managerId, RoleType.Manager, "MGR-001");
        SeedUser(_studentId, RoleType.Student, "STD-001");
        SeedClass();
        SeedSession();
        _db.ProgramEnrollments.Seed(new ProgramEnrollment
        {
            Id = _programEnrollmentId,
            StudentId = _studentId,
            ProgramId = _programId,
            Status = EnrollmentStatus.Active,
            IsDeleted = false,
        });
        _db.ClassEnrollments.Seed(new ClassEnrollment
        {
            Id = Guid.NewGuid(),
            ClassId = _classId,
            StudentId = _studentId,
            ProgramEnrollmentId = _programEnrollmentId,
            Status = ClassEnrollmentStatus.Active,
            IsDeleted = false,
        });
        var sut = CreateSut(_managerId);

        var ex = await Assert.ThrowsAsync<BadRequestException>(() =>
            sut.UpdateSessionAttendanceAsync(
                _classId,
                _sessionId,
                _studentId,
                new UpdateSessionAttendanceRequestDto { Status = AttendanceStatus.Present }));

        Assert.Contains("active module enrollment", ex.Message);
    }

    [Fact]
    public async Task Update_ThrowsBadRequest_WhenStatusInvalid()
    {
        SeedUser(_managerId, RoleType.Manager, "MGR-001");
        SeedClass();
        SeedSession();
        SeedStudentRoster();
        var sut = CreateSut(_managerId);

        await Assert.ThrowsAsync<BadRequestException>(() =>
            sut.UpdateSessionAttendanceAsync(
                _classId,
                _sessionId,
                _studentId,
                new UpdateSessionAttendanceRequestDto { Status = (AttendanceStatus)99 }));
    }

    [Fact]
    public async Task Update_ThrowsForbidden_WhenMentorDoesNotOwnClass()
    {
        SeedUser(_mentorId, RoleType.Mentor, "MNT-001");
        SeedUser(_studentId, RoleType.Student, "STD-001");
        SeedClass(mentorId: Guid.NewGuid());
        SeedSession();
        SeedStudentRoster();
        var sut = CreateSut(_mentorId);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            sut.UpdateSessionAttendanceAsync(
                _classId,
                _sessionId,
                _studentId,
                new UpdateSessionAttendanceRequestDto { Status = AttendanceStatus.Present }));
    }

    // ── Absence fail rule ─────────────────────────────────────────────────────

    private void SeedModuleEntity()
    {
        _db.Modules.Seed(new Module
        {
            Id = _moduleId,
            Code = "MOD-001",
            Name = "Module 1",
            ProgramId = _programId,
            ModuleType = ModuleType.Experiential,
            IsDeleted = false,
        });
    }

    private void SeedActivityLinkedSessions(int count)
    {
        for (var i = 0; i < count; i++)
        {
            var activityId = Guid.Parse($"aaaaaaaa-aaaa-aaaa-aaaa-{i + 1:D12}");
            _db.ClassSessions.Seed(new ClassSession
            {
                Id = i == 0 ? _sessionId : Guid.Parse($"bbbbbbbb-bbbb-bbbb-bbbb-{i + 1:D12}"),
                ClassId = _classId,
                ModuleId = _moduleId,
                ActivityId = activityId,
                Title = $"Session {i + 1}",
                SessionKind = SessionKind.LiveOnline,
                StartTime = _now.AddDays(i).AddHours(-1),
                EndTime = _now.AddDays(i).AddHours(2),
                RequiresAttendance = true,
                Status = ClassSessionStatus.InProgress,
                IsDeleted = false,
            });
        }
    }

    [Fact]
    public async Task Update_Absent_BelowThreshold_KeepsModuleActive()
    {
        SeedUser(_managerId, RoleType.Manager, "MGR-001");
        SeedUser(_studentId, RoleType.Student, "STD-001");
        SeedModuleEntity();
        SeedClass();
        SeedActivityLinkedSessions(count: 6);
        SeedStudentRoster();
        var sut = CreateSut(_managerId);

        await sut.UpdateSessionAttendanceAsync(
            _classId,
            _sessionId,
            _studentId,
            new UpdateSessionAttendanceRequestDto { Status = AttendanceStatus.Absent });

        var enrollment = _db.ModuleEnrollments.Items.Single(me => me.Id == _moduleEnrollmentId);
        Assert.Equal(EnrollmentStatus.Active, enrollment.Status);
        _notificationPublisher.Verify(
            n => n.PublishAsync(
                It.Is<NotificationCommand>(c => c.Type == NotificationType.ModuleFailed),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Update_Absent_AtThreshold_FailsModule_AndNotifies()
    {
        SeedUser(_managerId, RoleType.Manager, "MGR-001");
        SeedUser(_studentId, RoleType.Student, "STD-001");
        SeedModuleEntity();
        SeedClass();
        SeedActivityLinkedSessions(count: 5);
        SeedStudentRoster();
        var sut = CreateSut(_managerId);

        await sut.UpdateSessionAttendanceAsync(
            _classId,
            _sessionId,
            _studentId,
            new UpdateSessionAttendanceRequestDto { Status = AttendanceStatus.Absent });

        var enrollment = _db.ModuleEnrollments.Items.Single(me => me.Id == _moduleEnrollmentId);
        Assert.Equal(EnrollmentStatus.Failed, enrollment.Status);

        var programEnrollment = _db.ProgramEnrollments.Items.Single(pe => pe.Id == _programEnrollmentId);
        Assert.Equal(EnrollmentStatus.Failed, programEnrollment.Status);
        Assert.Equal(ProgramPurchaseEndReason.Attendance, programEnrollment.EndReason);
        Assert.Equal(_moduleId, programEnrollment.EndedModuleId);
        Assert.Equal(_now, programEnrollment.EndedAt);

        var seat = _db.ClassEnrollments.Items.Single(ce => ce.ProgramEnrollmentId == _programEnrollmentId);
        Assert.Equal(ClassEnrollmentStatus.Withdrawn, seat.Status);

        _notificationPublisher.Verify(
            n => n.PublishAsync(
                It.Is<NotificationCommand>(c => c.Type == NotificationType.ModuleFailed),
                It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    [Fact]
    public async Task Update_Present_NeverTriggersModuleFail()
    {
        SeedUser(_managerId, RoleType.Manager, "MGR-001");
        SeedUser(_studentId, RoleType.Student, "STD-001");
        SeedModuleEntity();
        SeedClass();
        SeedActivityLinkedSessions(count: 5);
        SeedStudentRoster();
        var sut = CreateSut(_managerId);

        await sut.UpdateSessionAttendanceAsync(
            _classId,
            _sessionId,
            _studentId,
            new UpdateSessionAttendanceRequestDto { Status = AttendanceStatus.Present });

        Assert.Equal(
            EnrollmentStatus.Active,
            _db.ModuleEnrollments.Items.Single(me => me.Id == _moduleEnrollmentId).Status);
        _notificationPublisher.Verify(
            n => n.PublishAsync(
                It.Is<NotificationCommand>(c => c.Type == NotificationType.ModuleFailed),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Update_Absent_AfterModuleFailed_ManagerCorrectionStillAllowed()
    {
        SeedUser(_managerId, RoleType.Manager, "MGR-001");
        SeedUser(_studentId, RoleType.Student, "STD-001");
        SeedModuleEntity();
        SeedClass();
        SeedActivityLinkedSessions(count: 5);
        SeedStudentRoster();
        var sut = CreateSut(_managerId);

        await sut.UpdateSessionAttendanceAsync(
            _classId,
            _sessionId,
            _studentId,
            new UpdateSessionAttendanceRequestDto { Status = AttendanceStatus.Absent });

        Assert.Equal(
            EnrollmentStatus.Failed,
            _db.ModuleEnrollments.Items.Single(me => me.Id == _moduleEnrollmentId).Status);

        // Manager backup path: attendance stays editable after the module failed.
        var otherSessionId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-000000000002");
        var result = await sut.UpdateSessionAttendanceAsync(
            _classId,
            otherSessionId,
            _studentId,
            new UpdateSessionAttendanceRequestDto { Status = AttendanceStatus.Absent });

        Assert.Equal(AttendanceStatus.Absent, result.Status);
        Assert.Equal(
            EnrollmentStatus.Failed,
            _db.ModuleEnrollments.Items.Single(me => me.Id == _moduleEnrollmentId).Status);
        Assert.Equal(
            EnrollmentStatus.Failed,
            _db.ProgramEnrollments.Items.Single(pe => pe.Id == _programEnrollmentId).Status);

        _notificationPublisher.Verify(
            n => n.PublishAsync(
                It.Is<NotificationCommand>(c => c.Type == NotificationType.ModuleFailed),
                It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    [Fact]
    public async Task Update_CorrectionBelowThreshold_ReopensEnrollmentAndRestoresSeat()
    {
        SeedUser(_managerId, RoleType.Manager, "MGR-001");
        SeedUser(_studentId, RoleType.Student, "STD-001");
        SeedModuleEntity();
        SeedClass();
        SeedActivityLinkedSessions(count: 5);
        SeedStudentRoster();
        var sut = CreateSut(_managerId);

        await sut.UpdateSessionAttendanceAsync(
            _classId,
            _sessionId,
            _studentId,
            new UpdateSessionAttendanceRequestDto { Status = AttendanceStatus.Absent });

        var programEnrollment = _db.ProgramEnrollments.Items.Single(pe => pe.Id == _programEnrollmentId);
        Assert.Equal(EnrollmentStatus.Failed, programEnrollment.Status);
        Assert.Equal(
            ClassEnrollmentStatus.Withdrawn,
            _db.ClassEnrollments.Items.Single(ce => ce.ProgramEnrollmentId == _programEnrollmentId).Status);

        // Manager corrects the wrongful absence -> missed ratio drops below the fail threshold.
        await sut.UpdateSessionAttendanceAsync(
            _classId,
            _sessionId,
            _studentId,
            new UpdateSessionAttendanceRequestDto { Status = AttendanceStatus.Present });

        Assert.Equal(EnrollmentStatus.Active, programEnrollment.Status);
        Assert.Null(programEnrollment.EndReason);
        Assert.Null(programEnrollment.EndedModuleId);
        Assert.Null(programEnrollment.EndedAt);
        Assert.Equal(
            EnrollmentStatus.Active,
            _db.ModuleEnrollments.Items.Single(me => me.Id == _moduleEnrollmentId).Status);
        Assert.Equal(
            ClassEnrollmentStatus.Active,
            _db.ClassEnrollments.Items.Single(ce => ce.ProgramEnrollmentId == _programEnrollmentId).Status);
    }

    [Fact]
    public async Task Update_CorrectionStillAboveThreshold_KeepsEnrollmentFailed()
    {
        SeedUser(_managerId, RoleType.Manager, "MGR-001");
        SeedUser(_studentId, RoleType.Student, "STD-001");
        SeedModuleEntity();
        SeedClass();
        SeedActivityLinkedSessions(count: 5);
        SeedStudentRoster();
        var sut = CreateSut(_managerId);

        // 1/5 = 20% -> fail. Then a second absence is recorded (2/5 = 40%).
        await sut.UpdateSessionAttendanceAsync(
            _classId,
            _sessionId,
            _studentId,
            new UpdateSessionAttendanceRequestDto { Status = AttendanceStatus.Absent });
        await sut.UpdateSessionAttendanceAsync(
            _classId,
            Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-000000000002"),
            _studentId,
            new UpdateSessionAttendanceRequestDto { Status = AttendanceStatus.Absent });

        // Correcting only one absence leaves 1/5 = 20% -> still at the threshold, no reopen.
        await sut.UpdateSessionAttendanceAsync(
            _classId,
            _sessionId,
            _studentId,
            new UpdateSessionAttendanceRequestDto { Status = AttendanceStatus.Present });

        var programEnrollment = _db.ProgramEnrollments.Items.Single(pe => pe.Id == _programEnrollmentId);
        Assert.Equal(EnrollmentStatus.Failed, programEnrollment.Status);
        Assert.Equal(
            EnrollmentStatus.Failed,
            _db.ModuleEnrollments.Items.Single(me => me.Id == _moduleEnrollmentId).Status);
    }
}
