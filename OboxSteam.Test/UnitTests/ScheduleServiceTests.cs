using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OboxSteam.Application.Exceptions;
using OboxSteam.Application.Interfaces;
using OboxSteam.Application.Services;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;
using OboxSteam.Test.Helpers;

namespace OboxSteam.Test.UnitTests;

public sealed class ScheduleServiceTests
{
    private readonly Guid _studentId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private readonly Guid _otherStudentId = Guid.Parse("12121212-1212-1212-1212-121212121212");
    private readonly Guid _managerId = Guid.Parse("13131313-1313-1313-1313-131313131313");
    private readonly Guid _parentId = Guid.Parse("15151515-1515-1515-1515-151515151515");
    private readonly Guid _mentorId = Guid.Parse("14141414-1414-1414-1414-141414141414");
    private readonly Guid _programId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private readonly Guid _classId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private readonly Guid _otherClassId = Guid.Parse("45454545-4545-4545-4545-454545454545");
    private readonly Guid _moduleId = Guid.Parse("66666666-6666-6666-6666-666666666666");
    private readonly Guid _activityId = Guid.Parse("77777777-7777-7777-7777-777777777777");
    private readonly Guid _programEnrollmentId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private readonly Guid _moduleEnrollmentId = Guid.Parse("99999999-9999-9999-9999-999999999999");

    /// <summary>Wednesday 10:00 in Asia/Ho_Chi_Minh (UTC+7).</summary>
    private readonly DateTime _now = new(2026, 6, 10, 3, 0, 0, DateTimeKind.Utc);

    private readonly DateOnly _weekMonday = new(2026, 6, 8);
    private readonly DateOnly _weekSunday = new(2026, 6, 14);

    private readonly InMemoryUnitOfWork _db = new();
    private readonly Mock<IClaimsService> _claimsService = new();
    private readonly Mock<ICurrentTime> _currentTime = new();

    private ScheduleService CreateSut(Guid? currentUserId = null)
    {
        _claimsService.Setup(c => c.GetCurrentUserId).Returns(currentUserId ?? _studentId);
        _currentTime.Setup(t => t.GetCurrentTime()).Returns(_now);
        return new ScheduleService(
            _db,
            _claimsService.Object,
            _currentTime.Object,
            NullLogger<ScheduleService>.Instance);
    }

    private void SeedStudent(Guid? id = null, string code = "STD-001")
    {
        var studentId = id ?? _studentId;
        _db.Users.Seed(new User
        {
            Id = studentId,
            Code = code,
            Email = $"{code.ToLower()}@test.com",
            Role = RoleType.Student,
            IsDeleted = false,
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
            IsDeleted = false,
        });
    }

    private void SeedParent()
    {
        _db.Users.Seed(new User
        {
            Id = _parentId,
            Code = "PAR-001",
            Email = "parent@test.com",
            Role = RoleType.Parent,
            IsDeleted = false,
        });
    }

    private void SeedParentLink(bool isVerified = true)
    {
        _db.ParentStudents.Seed(new ParentStudent
        {
            Id = Guid.NewGuid(),
            ParentId = _parentId,
            StudentId = _studentId,
            IsVerified = isVerified,
            IsDeleted = false,
        });
    }

    private Class SeedClass(
        Guid? id = null,
        string code = "CLS-001",
        string name = "Cohort A",
        Guid? mentorId = null)
    {
        var entity = new Class
        {
            Id = id ?? _classId,
            Code = code,
            Name = name,
            ProgramId = _programId,
            MentorId = mentorId ?? _mentorId,
            Status = ClassStatus.InProgress,
            MaxCapacity = 30,
            StartDate = _now.AddDays(-7),
            EndDate = _now.AddDays(60),
            IsDeleted = false,
        };
        _db.Classes.Seed(entity);
        return entity;
    }

    private void SeedClassEnrollment(
        Guid classId,
        ClassEnrollmentStatus status = ClassEnrollmentStatus.Active,
        bool isDeleted = false)
    {
        _db.ClassEnrollments.Seed(new ClassEnrollment
        {
            Id = Guid.NewGuid(),
            ClassId = classId,
            StudentId = _studentId,
            ProgramEnrollmentId = _programEnrollmentId,
            Status = status,
            EnrolledAt = _now.AddDays(-3),
            IsDeleted = isDeleted,
        });
    }

    private ClassSession SeedSession(
        Guid classId,
        DateTime startUtc,
        DateTime endUtc,
        string title = "Lesson",
        ClassSessionStatus status = ClassSessionStatus.Scheduled,
        Guid? activityId = null,
        string? location = "P.012",
        Guid? id = null)
    {
        var session = new ClassSession
        {
            Id = id ?? Guid.NewGuid(),
            ClassId = classId,
            ModuleId = _moduleId,
            ActivityId = activityId,
            Title = title,
            StartTime = startUtc,
            EndTime = endUtc,
            Location = location,
            MeetingUrl = null,
            SessionKind = SessionKind.LiveOnline,
            Status = status,
            IsDeleted = false,
        };
        _db.ClassSessions.Seed(session);
        return session;
    }

    private void SeedAttendance(Guid sessionId, AttendanceStatus status)
    {
        _db.SessionAttendances.Seed(new SessionAttendance
        {
            Id = Guid.NewGuid(),
            ClassSessionId = sessionId,
            StudentId = _studentId,
            ModuleEnrollmentId = _moduleEnrollmentId,
            Status = status,
            CheckedInAt = status is AttendanceStatus.Present or AttendanceStatus.Late ? _now : null,
            IsDeleted = false,
        });
    }

    [Fact]
    public async Task GetWeeklySchedule_DefaultsToCurrentVietnamMonday_AndAlwaysHasSevenDays()
    {
        SeedStudent();
        var sut = CreateSut();

        var result = await sut.GetWeeklyScheduleAsync();

        Assert.Equal(_studentId, result.StudentId);
        Assert.Equal(_weekMonday, result.WeekStart);
        Assert.Equal(_weekSunday, result.WeekEnd);
        Assert.Equal("Asia/Ho_Chi_Minh", result.Timezone);
        Assert.Equal(7, result.Days.Count);
        Assert.Equal(DayOfWeek.Monday, result.Days[0].DayOfWeek);
        Assert.Equal(_weekMonday, result.Days[0].Date);
        Assert.Equal(DayOfWeek.Sunday, result.Days[6].DayOfWeek);
        Assert.Equal(_weekSunday, result.Days[6].Date);
        Assert.All(result.Days, day => Assert.Empty(day.Sessions));
    }

    [Fact]
    public async Task GetWeeklySchedule_ThrowsBadRequest_WhenWeekStartIsNotMonday()
    {
        SeedStudent();
        var sut = CreateSut();

        var ex = await Assert.ThrowsAsync<BadRequestException>(
            () => sut.GetWeeklyScheduleAsync(new DateOnly(2026, 6, 9)));

        Assert.Contains("Monday", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetWeeklySchedule_ThrowsForbidden_WhenNotStudentOrParent()
    {
        SeedManager();
        var sut = CreateSut(currentUserId: _managerId);

        await Assert.ThrowsAsync<ForbiddenException>(() => sut.GetWeeklyScheduleAsync(_weekMonday));
    }

    [Fact]
    public async Task GetWeeklySchedule_ThrowsForbidden_WhenStudentRequestsAnotherStudent()
    {
        SeedStudent();
        SeedStudent(_otherStudentId, "STD-002");
        var sut = CreateSut();

        await Assert.ThrowsAsync<ForbiddenException>(
            () => sut.GetWeeklyScheduleAsync(_weekMonday, _otherStudentId));
    }

    [Fact]
    public async Task GetWeeklySchedule_ParentSeesLinkedChildSchedule()
    {
        SeedStudent();
        SeedParent();
        SeedParentLink();
        SeedClass();
        SeedClassEnrollment(_classId);

        var start = new DateTime(2026, 6, 8, 1, 0, 0, DateTimeKind.Utc);
        var session = SeedSession(_classId, start, start.AddHours(2), "Child lesson");

        var sut = CreateSut(currentUserId: _parentId);

        var result = await sut.GetWeeklyScheduleAsync(_weekMonday, _studentId);

        Assert.Equal(_studentId, result.StudentId);
        var items = result.Days.SelectMany(d => d.Sessions).ToList();
        Assert.Single(items);
        Assert.Equal(session.Id, items[0].Id);
    }

    [Fact]
    public async Task GetWeeklySchedule_ThrowsBadRequest_WhenParentOmitsStudentId()
    {
        SeedParent();
        var sut = CreateSut(currentUserId: _parentId);

        await Assert.ThrowsAsync<BadRequestException>(() => sut.GetWeeklyScheduleAsync(_weekMonday));
    }

    [Fact]
    public async Task GetWeeklySchedule_ThrowsForbidden_WhenParentLinkIsUnverified()
    {
        SeedStudent();
        SeedParent();
        SeedParentLink(isVerified: false);
        var sut = CreateSut(currentUserId: _parentId);

        await Assert.ThrowsAsync<ForbiddenException>(
            () => sut.GetWeeklyScheduleAsync(_weekMonday, _studentId));
    }

    [Fact]
    public async Task GetWeeklySchedule_OmitsCancelledSessions_AndInactiveEnrollments()
    {
        SeedStudent();
        SeedClass();
        SeedClass(id: _otherClassId, code: "CLS-002", name: "Withdrawn cohort");
        SeedClassEnrollment(_classId);
        SeedClassEnrollment(_otherClassId, ClassEnrollmentStatus.Withdrawn);

        var keepStart = new DateTime(2026, 6, 8, 1, 0, 0, DateTimeKind.Utc);
        var keep = SeedSession(_classId, keepStart, keepStart.AddHours(2), "Keep");
        SeedSession(
            _classId,
            keepStart.AddHours(3),
            keepStart.AddHours(5),
            "Cancelled",
            ClassSessionStatus.Cancelled);
        SeedSession(
            _otherClassId,
            keepStart.AddHours(6),
            keepStart.AddHours(8),
            "Withdrawn class");

        var sut = CreateSut();

        var result = await sut.GetWeeklyScheduleAsync(_weekMonday);

        var sessions = result.Days.SelectMany(d => d.Sessions).ToList();
        Assert.Single(sessions);
        Assert.Equal(keep.Id, sessions[0].Id);
        Assert.Equal("CLS-001", sessions[0].ClassCode);
        Assert.Equal("Cohort A", sessions[0].ClassName);
    }

    [Fact]
    public async Task GetWeeklySchedule_GroupsSessionByVietnamLocalStartDate()
    {
        SeedStudent();
        SeedClass();
        SeedClassEnrollment(_classId);

        var mondayLocalStart = new DateTime(2026, 6, 7, 17, 30, 0, DateTimeKind.Utc);
        var afterMidnight = SeedSession(_classId, mondayLocalStart, mondayLocalStart.AddHours(2), "After midnight");

        var sut = CreateSut();

        var result = await sut.GetWeeklyScheduleAsync(_weekMonday);

        var monday = result.Days.Single(d => d.Date == _weekMonday);
        Assert.Single(monday.Sessions);
        Assert.Equal(afterMidnight.Id, monday.Sessions[0].Id);
        Assert.Empty(result.Days.Single(d => d.DayOfWeek == DayOfWeek.Sunday).Sessions);
    }

    [Fact]
    public async Task GetWeeklySchedule_MapsAttendance()
    {
        SeedStudent();
        SeedClass();
        SeedClassEnrollment(_classId);

        var presentStart = new DateTime(2026, 6, 8, 1, 0, 0, DateTimeKind.Utc);
        var lateStart = new DateTime(2026, 6, 9, 1, 0, 0, DateTimeKind.Utc);
        var expectedStart = new DateTime(2026, 6, 10, 1, 0, 0, DateTimeKind.Utc);
        var missingStart = new DateTime(2026, 6, 11, 1, 0, 0, DateTimeKind.Utc);

        var present = SeedSession(_classId, presentStart, presentStart.AddHours(2), "Present", activityId: _activityId);
        var late = SeedSession(_classId, lateStart, lateStart.AddHours(2), "Late");
        var expected = SeedSession(_classId, expectedStart, expectedStart.AddHours(2), "Expected");
        var missing = SeedSession(_classId, missingStart, missingStart.AddHours(2), "No roster");

        SeedAttendance(present.Id, AttendanceStatus.Present);
        SeedAttendance(late.Id, AttendanceStatus.Late);
        SeedAttendance(expected.Id, AttendanceStatus.Expected);

        var sut = CreateSut();

        var result = await sut.GetWeeklyScheduleAsync(_weekMonday);
        var byId = result.Days.SelectMany(d => d.Sessions).ToDictionary(s => s.Id);

        Assert.Equal(AttendanceStatus.Present, byId[present.Id].AttendanceStatus);
        Assert.Equal("CLS-001", byId[present.Id].ClassCode);

        Assert.Equal(AttendanceStatus.Late, byId[late.Id].AttendanceStatus);

        Assert.Equal(AttendanceStatus.Expected, byId[expected.Id].AttendanceStatus);

        Assert.Null(byId[missing.Id].AttendanceStatus);
    }

    [Fact]
    public async Task GetWeeklySchedule_ExcludesSessionsOutsideTheWeekWindow()
    {
        SeedStudent();
        SeedClass();
        SeedClassEnrollment(_classId);

        var beforeWeek = new DateTime(2026, 6, 7, 16, 30, 0, DateTimeKind.Utc);
        var afterWeek = new DateTime(2026, 6, 14, 17, 0, 0, DateTimeKind.Utc);
        var inWeek = new DateTime(2026, 6, 14, 16, 0, 0, DateTimeKind.Utc);

        SeedSession(_classId, beforeWeek, beforeWeek.AddHours(1), "Before");
        SeedSession(_classId, afterWeek, afterWeek.AddHours(1), "After");
        var sundayInWeek = SeedSession(_classId, inWeek, inWeek.AddHours(1), "Sunday in week");

        var sut = CreateSut();

        var result = await sut.GetWeeklyScheduleAsync(_weekMonday);
        var sessions = result.Days.SelectMany(d => d.Sessions).ToList();

        Assert.Single(sessions);
        Assert.Equal(sundayInWeek.Id, sessions[0].Id);
        Assert.Equal(DayOfWeek.Sunday, result.Days.Single(d => d.Sessions.Count == 1).DayOfWeek);
    }

    [Fact]
    public async Task GetWeeklySchedule_MarksCompletedSessions()
    {
        SeedStudent();
        SeedClass();
        SeedClassEnrollment(_classId);

        var start = new DateTime(2026, 6, 8, 2, 0, 0, DateTimeKind.Utc);
        SeedSession(_classId, start, start.AddHours(2), "Done", ClassSessionStatus.Completed);

        var sut = CreateSut();

        var result = await sut.GetWeeklyScheduleAsync(_weekMonday);
        var session = Assert.Single(result.Days.SelectMany(d => d.Sessions));
        Assert.True(session.IsCompleted);
        Assert.Equal(ClassSessionStatus.Completed, session.Status);
    }
}
