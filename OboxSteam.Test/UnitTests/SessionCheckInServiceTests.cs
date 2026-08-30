using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OboxSteam.Application.DTOs.ClassSessionDTO;
using OboxSteam.Application.Exceptions;
using OboxSteam.Application.Interfaces;
using OboxSteam.Application.Notifications;
using OboxSteam.Application.Services;
using OboxSteam.Application.Validation;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;
using OboxSteam.Test.Helpers;

namespace OboxSteam.Test.UnitTests;

public sealed class SessionCheckInServiceTests
{
    private readonly Guid _studentId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private readonly Guid _mentorId = Guid.Parse("14141414-1414-1414-1414-141414141414");
    private readonly Guid _otherMentorId = Guid.Parse("15151515-1515-1515-1515-151515151515");
    private readonly Guid _programId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private readonly Guid _moduleId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private readonly Guid _classId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private readonly Guid _sessionId = Guid.Parse("55555555-5555-5555-5555-555555555555");
    private readonly Guid _programEnrollmentId = Guid.Parse("66666666-6666-6666-6666-666666666666");
    private readonly Guid _moduleEnrollmentId = Guid.Parse("77777777-7777-7777-7777-777777777777");

    private readonly DateTime _now = new(2026, 8, 22, 9, 0, 0, DateTimeKind.Utc);

    private readonly InMemoryUnitOfWork _db = new();
    private readonly Mock<IClaimsService> _claimsService = new();
    private readonly Mock<ICurrentTime> _currentTime = new();
    private readonly Mock<INotificationPublisher> _notificationPublisher = new();

    private SessionAttendanceService CreateSut(Guid? currentUserId = null)
    {
        _claimsService.Setup(c => c.GetCurrentUserId).Returns(currentUserId ?? _mentorId);
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
        ClassSessionStatus status = ClassSessionStatus.InProgress,
        bool withActiveToken = true)
    {
        var session = new ClassSession
        {
            Id = _sessionId,
            ClassId = _classId,
            ModuleId = _moduleId,
            Title = "Field trip",
            SessionKind = SessionKind.Offline,
            StartTime = _now.AddHours(-1),
            EndTime = _now.AddHours(1),
            Status = status,
            IsDeleted = false,
        };

        if (withActiveToken)
        {
            session.CheckInToken = Guid.Parse("99999999-9999-9999-9999-999999999999");
            session.CheckInCode = "123456";
            session.CheckInTokenExpiresAt = _now.AddSeconds(30);
        }

        _db.ClassSessions.Seed(session);
        return session;
    }

    private void SeedStudentRoster()
    {
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
        _db.ModuleEnrollments.Seed(new ModuleEnrollment
        {
            Id = _moduleEnrollmentId,
            StudentId = _studentId,
            ModuleId = _moduleId,
            ProgramEnrollmentId = _programEnrollmentId,
            Status = EnrollmentStatus.Active,
            AttemptNumber = 1,
            IsDeleted = false,
        });
    }

    // ── GenerateCheckInTokenAsync ─────────────────────────────────────────────

    [Fact]
    public async Task GenerateToken_RotatesTokenAndCode_WithTtl()
    {
        SeedUser(_mentorId, RoleType.Mentor, "MNT-001");
        SeedClass(mentorId: _mentorId);
        var session = SeedSession(withActiveToken: false);
        var sut = CreateSut(_mentorId);

        var result = await sut.GenerateCheckInTokenAsync(_sessionId);

        Assert.Equal(_sessionId, result.ClassSessionId);
        Assert.NotEqual(Guid.Empty, result.Token);
        Assert.Equal(6, result.Code.Length);
        Assert.Equal(
            _now.AddSeconds(ClassSessionCheckInValidator.TokenTtlSeconds),
            result.ExpiresAt);
        Assert.Equal(result.Token, session.CheckInToken);
        Assert.Equal(result.Code, session.CheckInCode);
        Assert.Equal(result.ExpiresAt, session.CheckInTokenExpiresAt);
    }

    [Fact]
    public async Task GenerateToken_ThrowsForbidden_WhenMentorDoesNotOwnClass()
    {
        SeedUser(_otherMentorId, RoleType.Mentor, "MNT-002");
        SeedClass(mentorId: _mentorId);
        SeedSession();
        var sut = CreateSut(_otherMentorId);

        await Assert.ThrowsAsync<ForbiddenException>(() => sut.GenerateCheckInTokenAsync(_sessionId));
    }

    [Fact]
    public async Task GenerateToken_ThrowsBadRequest_WhenSessionCompleted()
    {
        SeedUser(_mentorId, RoleType.Mentor, "MNT-001");
        SeedClass(mentorId: _mentorId);
        SeedSession(status: ClassSessionStatus.Completed);
        var sut = CreateSut(_mentorId);

        await Assert.ThrowsAsync<BadRequestException>(() => sut.GenerateCheckInTokenAsync(_sessionId));
    }

    // ── CheckInAsync ──────────────────────────────────────────────────────────

    [Fact]
    public async Task CheckIn_CreatesPresentAttendance_WithValidCode()
    {
        SeedUser(_studentId, RoleType.Student, "STD-001");
        SeedClass(mentorId: _mentorId);
        SeedSession();
        SeedStudentRoster();
        var sut = CreateSut(_studentId);

        var result = await sut.CheckInAsync(
            _sessionId,
            new ClassSessionCheckInRequestDto { Code = "123456" });

        Assert.Equal(AttendanceStatus.Present, result.Status);
        Assert.Equal(_studentId, result.StudentId);
        Assert.Equal(_studentId, result.RecordedBy);
        Assert.Equal(_now, result.CheckedInAt);
        Assert.Equal(_moduleEnrollmentId, result.ModuleEnrollmentId);
        Assert.Single(_db.SessionAttendances.Items);
        _notificationPublisher.Verify(
            n => n.PublishAsync(
                It.Is<NotificationCommand>(c =>
                    c.Type == NotificationType.AttendanceMarkedPresent
                    && c.Audience.Kind == NotificationAudienceKind.ParentsOfStudent
                    && c.Audience.StudentId == _studentId
                    && c.Tokens[NotificationTokenKeys.CheckedInAt] == "16:00"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CheckIn_CreatesPresentAttendance_WithValidToken()
    {
        SeedUser(_studentId, RoleType.Student, "STD-001");
        SeedClass(mentorId: _mentorId);
        SeedSession();
        SeedStudentRoster();
        var sut = CreateSut(_studentId);

        var result = await sut.CheckInAsync(
            _sessionId,
            new ClassSessionCheckInRequestDto
            {
                Token = Guid.Parse("99999999-9999-9999-9999-999999999999"),
            });

        Assert.Equal(AttendanceStatus.Present, result.Status);
    }

    [Fact]
    public async Task CheckIn_UpdatesExistingAttendance_OnSecondCheckIn()
    {
        SeedUser(_studentId, RoleType.Student, "STD-001");
        SeedClass(mentorId: _mentorId);
        SeedSession();
        SeedStudentRoster();
        _db.SessionAttendances.Seed(new SessionAttendance
        {
            Id = Guid.NewGuid(),
            ClassSessionId = _sessionId,
            StudentId = _studentId,
            ModuleEnrollmentId = _moduleEnrollmentId,
            Status = AttendanceStatus.Expected,
            IsDeleted = false,
        });
        var sut = CreateSut(_studentId);

        var result = await sut.CheckInAsync(
            _sessionId,
            new ClassSessionCheckInRequestDto { Code = "123456" });

        Assert.Equal(AttendanceStatus.Present, result.Status);
        Assert.Single(_db.SessionAttendances.Items);
        _notificationPublisher.Verify(
            n => n.PublishAsync(
                It.Is<NotificationCommand>(c => c.Type == NotificationType.AttendanceMarkedPresent),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CheckIn_DoesNotNotify_OnSecondSelfCheckIn()
    {
        SeedUser(_studentId, RoleType.Student, "STD-001");
        SeedClass(mentorId: _mentorId);
        SeedSession();
        SeedStudentRoster();
        _db.SessionAttendances.Seed(new SessionAttendance
        {
            Id = Guid.NewGuid(),
            ClassSessionId = _sessionId,
            StudentId = _studentId,
            ModuleEnrollmentId = _moduleEnrollmentId,
            Status = AttendanceStatus.Present,
            CheckedInAt = _now.AddMinutes(-10),
            RecordedBy = _studentId,
            IsDeleted = false,
        });
        var sut = CreateSut(_studentId);

        await sut.CheckInAsync(
            _sessionId,
            new ClassSessionCheckInRequestDto { Code = "123456" });

        _notificationPublisher.Verify(
            n => n.PublishAsync(It.IsAny<NotificationCommand>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task CheckIn_ThrowsBadRequest_WhenTokenExpired()
    {
        SeedUser(_studentId, RoleType.Student, "STD-001");
        SeedClass(mentorId: _mentorId);
        var session = SeedSession();
        session.CheckInTokenExpiresAt = _now.AddSeconds(-1);
        SeedStudentRoster();
        var sut = CreateSut(_studentId);

        var ex = await Assert.ThrowsAsync<BadRequestException>(() =>
            sut.CheckInAsync(_sessionId, new ClassSessionCheckInRequestDto { Code = "123456" }));

        Assert.Equal(ClassSessionCheckInValidator.TokenExpiredMessage, ex.Message);
    }

    [Fact]
    public async Task CheckIn_ThrowsBadRequest_WhenCodeWrong()
    {
        SeedUser(_studentId, RoleType.Student, "STD-001");
        SeedClass(mentorId: _mentorId);
        SeedSession();
        SeedStudentRoster();
        var sut = CreateSut(_studentId);

        await Assert.ThrowsAsync<BadRequestException>(() =>
            sut.CheckInAsync(_sessionId, new ClassSessionCheckInRequestDto { Code = "000000" }));
    }

    [Fact]
    public async Task CheckIn_ThrowsBadRequest_WhenStudentNotEnrolled()
    {
        SeedUser(_studentId, RoleType.Student, "STD-001");
        SeedClass(mentorId: _mentorId);
        SeedSession();
        var sut = CreateSut(_studentId);

        await Assert.ThrowsAsync<BadRequestException>(() =>
            sut.CheckInAsync(_sessionId, new ClassSessionCheckInRequestDto { Code = "123456" }));
    }

    [Fact]
    public async Task CheckIn_ThrowsForbidden_WhenCallerIsNotStudent()
    {
        SeedUser(_mentorId, RoleType.Mentor, "MNT-001");
        SeedClass(mentorId: _mentorId);
        SeedSession();
        var sut = CreateSut(_mentorId);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            sut.CheckInAsync(_sessionId, new ClassSessionCheckInRequestDto { Code = "123456" }));
    }
}
