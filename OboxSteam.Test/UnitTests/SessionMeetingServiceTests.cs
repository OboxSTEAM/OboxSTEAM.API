using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OboxSteam.Application.Exceptions;
using OboxSteam.Application.Interfaces;
using OboxSteam.Application.Notifications;
using OboxSteam.Application.Services;
using OboxSteam.Application.Validation;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;
using OboxSteam.Test.Helpers;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;

namespace OboxSteam.Test.UnitTests;

public sealed class SessionMeetingServiceTests
{
    private readonly Guid _studentId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private readonly Guid _mentorId = Guid.Parse("14141414-1414-1414-1414-141414141414");
    private readonly Guid _programId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private readonly Guid _moduleId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private readonly Guid _classId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private readonly Guid _sessionId = Guid.Parse("55555555-5555-5555-5555-555555555555");
    private readonly Guid _programEnrollmentId = Guid.Parse("66666666-6666-6666-6666-666666666666");
    private readonly Guid _moduleEnrollmentId = Guid.Parse("77777777-7777-7777-7777-777777777777");

    private readonly DateTime _now = new(2026, 8, 30, 9, 0, 0, DateTimeKind.Utc);

    private readonly InMemoryUnitOfWork _db = new();
    private readonly Mock<IClaimsService> _claimsService = new();
    private readonly Mock<ICurrentTime> _currentTime = new();
    private readonly Mock<INotificationPublisher> _notificationPublisher = new();
    private readonly IJaasJwtService _jaas;

    public SessionMeetingServiceTests()
    {
        using var rsa = RSA.Create(2048);
        var pem = rsa.ExportPkcs8PrivateKeyPem();
        _jaas = new JaasJwtService(
            "vpaas-magic-cookie-testapp",
            "test-key-id",
            pem,
            "8x8.vc",
            NullLogger<JaasJwtService>.Instance);
    }

    private SessionMeetingService CreateSut(Guid currentUserId)
    {
        _claimsService.Setup(c => c.GetCurrentUserId).Returns(currentUserId);
        _currentTime.Setup(t => t.GetCurrentTime()).Returns(_now);
        _notificationPublisher
            .Setup(n => n.PublishAsync(It.IsAny<NotificationCommand>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        return new SessionMeetingService(
            _db,
            _claimsService.Object,
            _currentTime.Object,
            _jaas,
            _notificationPublisher.Object,
            NullLogger<SessionMeetingService>.Instance);
    }

    private void SeedUsers()
    {
        _db.Users.Seed(new User
        {
            Id = _studentId,
            Code = "STU",
            Email = "student@test.com",
            FullName = "Student One",
            Role = RoleType.Student,
            IsDeleted = false,
        });
        _db.Users.Seed(new User
        {
            Id = _mentorId,
            Code = "MEN",
            Email = "mentor@test.com",
            FullName = "Mentor One",
            Role = RoleType.Mentor,
            IsDeleted = false,
        });
    }

    private void SeedClassAndEnrollments()
    {
        _db.Classes.Seed(new Class
        {
            Id = _classId,
            Code = "CLS-001",
            Name = "Cohort A",
            ProgramId = _programId,
            MentorId = _mentorId,
            Status = ClassStatus.InProgress,
            MaxCapacity = 30,
            StartDate = _now.AddDays(-7),
            EndDate = _now.AddDays(60),
            IsDeleted = false,
        });
        _db.ProgramEnrollments.Seed(new ProgramEnrollment
        {
            Id = _programEnrollmentId,
            StudentId = _studentId,
            ProgramId = _programId,
            Status = EnrollmentStatus.Active,
            IsDeleted = false,
        });
        _db.ModuleEnrollments.Seed(new ModuleEnrollment
        {
            Id = _moduleEnrollmentId,
            StudentId = _studentId,
            ModuleId = _moduleId,
            ProgramEnrollmentId = _programEnrollmentId,
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
    }

    private ClassSession SeedLiveOnline(DateTime start, DateTime end)
    {
        var session = new ClassSession
        {
            Id = _sessionId,
            ClassId = _classId,
            ModuleId = _moduleId,
            Title = "Live lesson",
            SessionKind = SessionKind.LiveOnline,
            StartTime = start,
            EndTime = end,
            Status = ClassSessionStatus.Scheduled,
            IsDeleted = false,
        };
        _db.ClassSessions.Seed(session);
        return session;
    }

    [Fact]
    public async Task Join_Student_OnTime_RecordsPresent_AndReturnsJwt()
    {
        SeedUsers();
        SeedClassAndEnrollments();
        SeedLiveOnline(_now.AddMinutes(-5), _now.AddHours(1));

        var sut = CreateSut(_studentId);
        var result = await sut.JoinAsync(_sessionId);

        Assert.Equal(_sessionId.ToString(), result.RoomName);
        Assert.Equal("vpaas-magic-cookie-testapp", result.AppId);
        Assert.Equal("8x8.vc", result.Domain);
        Assert.False(result.IsModerator);
        Assert.Equal(nameof(AttendanceStatus.Present), result.AttendanceStatus);
        Assert.False(string.IsNullOrWhiteSpace(result.Jwt));

        var attendance = _db.SessionAttendances.GetQueryable().Single();
        Assert.Equal(AttendanceStatus.Present, attendance.Status);
        Assert.Equal(_studentId, attendance.RecordedBy);
        Assert.Equal(_now, attendance.CheckedInAt);

        var token = new JwtSecurityTokenHandler().ReadJwtToken(result.Jwt);
        Assert.Equal("test-key-id", token.Header.Kid);
        Assert.Equal("vpaas-magic-cookie-testapp", token.Payload["sub"]?.ToString());
    }

    [Fact]
    public async Task Join_Student_AfterGrace_RecordsLate()
    {
        SeedUsers();
        SeedClassAndEnrollments();
        SeedLiveOnline(_now.AddMinutes(-15), _now.AddHours(1));

        var sut = CreateSut(_studentId);
        var result = await sut.JoinAsync(_sessionId);

        Assert.Equal(nameof(AttendanceStatus.Late), result.AttendanceStatus);
        Assert.Equal(AttendanceStatus.Late, _db.SessionAttendances.GetQueryable().Single().Status);
    }

    [Fact]
    public async Task Join_Mentor_IsModerator_WithoutAttendanceRow()
    {
        SeedUsers();
        SeedClassAndEnrollments();
        SeedLiveOnline(_now.AddMinutes(-5), _now.AddHours(1));

        var sut = CreateSut(_mentorId);
        var result = await sut.JoinAsync(_sessionId);

        Assert.True(result.IsModerator);
        Assert.Null(result.AttendanceStatus);
        Assert.Empty(_db.SessionAttendances.GetQueryable());

        var token = new JwtSecurityTokenHandler().ReadJwtToken(result.Jwt);
        Assert.True(token.Payload.ContainsKey("context"));
        var contextJson = System.Text.Json.JsonSerializer.Serialize(token.Payload["context"]);
        Assert.Contains("\"moderator\":\"true\"", contextJson.Replace(" ", string.Empty));
    }

    [Fact]
    public async Task Join_IsIdempotent_KeepsOriginalCheckedInAt()
    {
        SeedUsers();
        SeedClassAndEnrollments();
        SeedLiveOnline(_now.AddMinutes(-5), _now.AddHours(1));

        var sut = CreateSut(_studentId);
        await sut.JoinAsync(_sessionId);

        _currentTime.Setup(t => t.GetCurrentTime()).Returns(_now.AddMinutes(20));
        var second = await sut.JoinAsync(_sessionId);

        var attendance = _db.SessionAttendances.GetQueryable().Single();
        Assert.Equal(_now, attendance.CheckedInAt);
        Assert.Equal(AttendanceStatus.Present, attendance.Status);
        Assert.Equal(nameof(AttendanceStatus.Present), second.AttendanceStatus);
    }

    [Fact]
    public async Task Leave_SetsParticipationMinutes()
    {
        SeedUsers();
        SeedClassAndEnrollments();
        SeedLiveOnline(_now.AddMinutes(-5), _now.AddHours(1));

        var sut = CreateSut(_studentId);
        await sut.JoinAsync(_sessionId);

        _currentTime.Setup(t => t.GetCurrentTime()).Returns(_now.AddMinutes(37));
        var leave = await sut.LeaveAsync(_sessionId);

        Assert.Equal(37, leave.ParticipationMinutes);
        Assert.Equal(_now.AddMinutes(37), leave.LeftAt);

        var attendance = _db.SessionAttendances.GetQueryable().Single();
        Assert.Equal(37, attendance.ParticipationMinutes);
    }

    [Fact]
    public async Task Join_Throws_WhenOutsideWindow()
    {
        SeedUsers();
        SeedClassAndEnrollments();
        SeedLiveOnline(_now.AddMinutes(20), _now.AddHours(2));

        var sut = CreateSut(_studentId);
        var ex = await Assert.ThrowsAsync<BadRequestException>(() => sut.JoinAsync(_sessionId));
        Assert.Equal(ClassSessionJoinValidator.JoinWindowClosedMessage, ex.Message);
    }

    [Fact]
    public void ResolveJoinAttendanceStatus_PresentWithinGrace()
    {
        var session = new ClassSession { StartTime = _now };
        Assert.Equal(
            AttendanceStatus.Present,
            ClassSessionJoinValidator.ResolveJoinAttendanceStatus(session, _now.AddMinutes(10)));
        Assert.Equal(
            AttendanceStatus.Late,
            ClassSessionJoinValidator.ResolveJoinAttendanceStatus(session, _now.AddMinutes(11)));
    }

    [Fact]
    public void JaasJwt_NormalizePem_ReplacesEscapedNewlines()
    {
        var normalized = JaasJwtService.NormalizePem("line1\\nline2\\r\\nline3");
        Assert.Equal("line1\nline2\nline3", normalized);
    }
}
