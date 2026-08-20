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

public sealed class ClassSessionGenerateTests
{
    private readonly Guid _mentorId = Guid.Parse("14141414-1414-1414-1414-141414141414");
    private readonly Guid _managerId = Guid.Parse("13131313-1313-1313-1313-131313131313");
    private readonly Guid _programId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private readonly Guid _moduleId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private readonly Guid _courseId = Guid.Parse("34343434-3434-3434-3434-343434343434");
    private readonly Guid _liveActivityId = Guid.Parse("35353535-3535-3535-3535-353535353535");
    private readonly Guid _offlineActivityId = Guid.Parse("36363636-3636-3636-3636-363636363636");
    private readonly Guid _assignmentId = Guid.Parse("37373737-3737-3737-3737-373737373737");
    private readonly Guid _classId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private readonly Guid _otherClassId = Guid.Parse("45454545-4545-4545-4545-454545454545");

    // 2026-08-22 is a Saturday.
    private readonly DateTime _classStart = new(2026, 8, 22, 0, 0, 0, DateTimeKind.Utc);
    private readonly DateTime _classEnd = new(2026, 10, 31, 0, 0, 0, DateTimeKind.Utc);

    private readonly InMemoryUnitOfWork _db = new();
    private readonly Mock<IClaimsService> _claimsService = new();
    private readonly Mock<INotificationPublisher> _notificationPublisher = new();

    private ClassSessionService CreateSut()
    {
        _claimsService.Setup(c => c.GetCurrentUserId).Returns(_managerId);
        _notificationPublisher
            .Setup(n => n.PublishManyAsync(
                It.IsAny<IReadOnlyList<NotificationCommand>>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        return new ClassSessionService(
            _db,
            _claimsService.Object,
            NullLogger<ClassSessionService>.Instance,
            _notificationPublisher.Object);
    }

    private static GenerateClassSessionsRequestDto SaturdayPattern() => new()
    {
        DaysOfWeek = new List<DayOfWeek> { DayOfWeek.Saturday },
        SessionStartTime = new TimeOnly(9, 0),
        SessionEndTime = new TimeOnly(11, 0),
    };

    private Class SeedClass(Guid? mentorId = null, DateTime? endDate = null)
    {
        var entity = new Class
        {
            Id = _classId,
            Code = "CLS-001",
            Name = "Cohort A",
            ProgramId = _programId,
            MentorId = mentorId,
            Status = ClassStatus.Open,
            MaxCapacity = 20,
            StartDate = _classStart,
            EndDate = endDate ?? _classEnd,
            IsDeleted = false,
        };
        _db.Classes.Seed(entity);
        return entity;
    }

    private void SeedCurriculum()
    {
        _db.Modules.Seed(new Module
        {
            Id = _moduleId,
            Code = "MOD-001",
            ProgramId = _programId,
            Name = "Module 1",
            ModuleOrder = 1,
            IsDeleted = false,
        });
        _db.Courses.Seed(new Course
        {
            Id = _courseId,
            Code = "CRS-001",
            ModuleId = _moduleId,
            Name = "Course 1",
            IsDeleted = false,
        });
        _db.Activities.Seed(
            new Activity
            {
                Id = _liveActivityId,
                Code = "ACT-001",
                CourseId = _courseId,
                Name = "Live lesson",
                ActivityType = ActivityType.LiveOnline,
                ActivityOrder = 1,
                Location = "https://meet.example.com/live",
                IsDeleted = false,
            },
            new Activity
            {
                Id = _offlineActivityId,
                Code = "ACT-002",
                CourseId = _courseId,
                Name = "Field trip",
                ActivityType = ActivityType.Offline,
                ActivityOrder = 2,
                Location = "123 Le Loi, HCMC",
                IsDeleted = false,
            },
            new Activity
            {
                Id = Guid.NewGuid(),
                Code = "ACT-003",
                CourseId = _courseId,
                Name = "Self-paced reading",
                ActivityType = ActivityType.SelfPaced,
                ActivityOrder = 3,
                IsDeleted = false,
            });
        _db.Assignments.Seed(new Assignment
        {
            Id = _assignmentId,
            Code = "ASM-001",
            ModuleId = _moduleId,
            Title = "Quiz 1",
            IsDeleted = false,
        });
    }

    [Fact]
    public async Task Generate_CreatesWeeklySessions_InCurriculumOrder()
    {
        SeedClass(mentorId: _mentorId);
        SeedCurriculum();
        var sut = CreateSut();

        var result = await sut.GenerateClassSessionsAsync(_classId, SaturdayPattern());

        // SelfPaced activities are skipped; assignments become AssignmentWindow sessions.
        Assert.Equal(3, result.Count);

        var live = result[0];
        Assert.Equal(_liveActivityId, live.ActivityId);
        Assert.Equal(SessionKind.Lesson, live.SessionKind);
        Assert.Equal(new DateTime(2026, 8, 22, 9, 0, 0, DateTimeKind.Utc), live.StartTime);
        Assert.Equal(new DateTime(2026, 8, 22, 11, 0, 0, DateTimeKind.Utc), live.EndTime);
        Assert.Equal("https://meet.example.com/live", live.MeetingUrl);
        Assert.Null(live.Location);
        Assert.True(live.RequiresAttendance);

        var offline = result[1];
        Assert.Equal(_offlineActivityId, offline.ActivityId);
        Assert.Equal(SessionKind.FieldTrip, offline.SessionKind);
        Assert.Equal(new DateTime(2026, 8, 29, 9, 0, 0, DateTimeKind.Utc), offline.StartTime);
        Assert.Equal("123 Le Loi, HCMC", offline.Location);
        Assert.Null(offline.MeetingUrl);

        var assignment = result[2];
        Assert.Equal(_assignmentId, assignment.AssignmentId);
        Assert.Equal(SessionKind.AssignmentWindow, assignment.SessionKind);
        Assert.Equal(new DateTime(2026, 9, 5, 9, 0, 0, DateTimeKind.Utc), assignment.StartTime);
        Assert.False(assignment.RequiresAttendance);

        Assert.Equal(3, _db.ClassSessions.Items.Count);
        Assert.All(_db.ClassSessions.Items, s => Assert.Equal(ClassSessionStatus.Scheduled, s.Status));

        _notificationPublisher.Verify(
            n => n.PublishManyAsync(
                It.Is<IReadOnlyList<NotificationCommand>>(cmds => cmds.Count == 3),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Generate_ThrowsConflict_WhenClassAlreadyHasSessions()
    {
        var classEntity = SeedClass(mentorId: _mentorId);
        SeedCurriculum();
        _db.ClassSessions.Seed(new ClassSession
        {
            Id = Guid.NewGuid(),
            ClassId = _classId,
            ModuleId = _moduleId,
            Title = "Existing",
            StartTime = _classStart.AddDays(1),
            EndTime = _classStart.AddDays(1).AddHours(2),
            Status = ClassSessionStatus.Scheduled,
            Class = classEntity,
            IsDeleted = false,
        });
        var sut = CreateSut();

        await Assert.ThrowsAsync<ConflictException>(() =>
            sut.GenerateClassSessionsAsync(_classId, SaturdayPattern()));

        Assert.Single(_db.ClassSessions.Items);
    }

    [Fact]
    public async Task Generate_ThrowsBadRequest_WhenClassHasNoMentor()
    {
        SeedClass(mentorId: null);
        SeedCurriculum();
        var sut = CreateSut();

        await Assert.ThrowsAsync<BadRequestException>(() =>
            sut.GenerateClassSessionsAsync(_classId, SaturdayPattern()));
    }

    [Fact]
    public async Task Generate_ThrowsBadRequest_WhenDateRangeTooShort()
    {
        SeedClass(mentorId: _mentorId, endDate: new DateTime(2026, 8, 23, 0, 0, 0, DateTimeKind.Utc));
        SeedCurriculum();
        var sut = CreateSut();

        var ex = await Assert.ThrowsAsync<BadRequestException>(() =>
            sut.GenerateClassSessionsAsync(_classId, SaturdayPattern()));

        Assert.Contains("only fits 1 of 3 sessions", ex.Message);
        Assert.Empty(_db.ClassSessions.Items);
    }

    [Fact]
    public async Task Generate_ThrowsBadRequest_WhenCurriculumHasNothingSchedulable()
    {
        SeedClass(mentorId: _mentorId);
        var sut = CreateSut();

        await Assert.ThrowsAsync<BadRequestException>(() =>
            sut.GenerateClassSessionsAsync(_classId, SaturdayPattern()));
    }

    [Fact]
    public async Task Generate_ThrowsConflict_WhenMentorHasOverlappingSession()
    {
        SeedClass(mentorId: _mentorId);
        SeedCurriculum();

        var busyClass = new Class
        {
            Id = _otherClassId,
            Code = "CLS-BUSY",
            Name = "Cohort B",
            ProgramId = _programId,
            MentorId = _mentorId,
            Status = ClassStatus.InProgress,
            MaxCapacity = 20,
            StartDate = _classStart,
            EndDate = _classEnd,
            IsDeleted = false,
        };
        _db.Classes.Seed(busyClass);
        _db.ClassSessions.Seed(new ClassSession
        {
            Id = Guid.NewGuid(),
            ClassId = _otherClassId,
            ModuleId = _moduleId,
            Title = "Busy session",
            StartTime = new DateTime(2026, 8, 22, 10, 0, 0, DateTimeKind.Utc),
            EndTime = new DateTime(2026, 8, 22, 12, 0, 0, DateTimeKind.Utc),
            Status = ClassSessionStatus.Scheduled,
            Class = busyClass,
            IsDeleted = false,
        });
        var sut = CreateSut();

        await Assert.ThrowsAsync<ConflictException>(() =>
            sut.GenerateClassSessionsAsync(_classId, SaturdayPattern()));

        // All-or-nothing: nothing is persisted when any slot conflicts.
        Assert.DoesNotContain(_db.ClassSessions.Items, s => s.ClassId == _classId);
    }
}
