using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OboxSteam.Application.Commons;
using OboxSteam.Application.Exceptions;
using OboxSteam.Application.Interfaces;
using OboxSteam.Application.Services;
using OboxSteam.Application.Validation;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;
using OboxSteam.Test.Helpers;

namespace OboxSteam.Test.UnitTests;

public sealed class ClassStudentProgressServiceTests
{
    private readonly Guid _mentorId = Guid.Parse("14141414-1414-1414-1414-141414141414");
    private readonly Guid _otherMentorId = Guid.Parse("15151515-1515-1515-1515-151515151515");
    private readonly Guid _student1Id = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private readonly Guid _student2Id = Guid.Parse("12121212-1212-1212-1212-121212121212");
    private readonly Guid _programId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private readonly Guid _moduleId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private readonly Guid _courseId = Guid.Parse("34343434-3434-3434-3434-343434343434");
    private readonly Guid _activityId = Guid.Parse("35353535-3535-3535-3535-353535353535");
    private readonly Guid _liveActivityId = Guid.Parse("36363636-3636-3636-3636-363636363636");
    private readonly Guid _assignmentId = Guid.Parse("55555555-5555-5555-5555-555555555555");
    private readonly Guid _classId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private readonly Guid _programEnrollment1Id = Guid.Parse("99999999-9999-9999-9999-999999999999");
    private readonly Guid _programEnrollment2Id = Guid.Parse("98989898-9898-9898-9898-989898989898");
    private readonly Guid _moduleEnrollment1Id = Guid.Parse("a1a1a1a1-a1a1-a1a1-a1a1-a1a1a1a1a1a1");
    private readonly Guid _moduleEnrollment2Id = Guid.Parse("a2a2a2a2-a2a2-a2a2-a2a2-a2a2a2a2a2a2");
    private readonly Guid _sessionId = Guid.Parse("c1c1c1c1-c1c1-c1c1-c1c1-c1c1c1c1c1c1");

    private readonly DateTime _now = DateTime.UtcNow;
    private readonly InMemoryUnitOfWork _db = new();
    private readonly Mock<IClaimsService> _claimsService = new();

    private ClassStudentProgressService CreateSut(Guid? currentUserId = null)
    {
        _claimsService.Setup(c => c.GetCurrentUserId).Returns(currentUserId ?? _mentorId);
        return new ClassStudentProgressService(
            _db,
            _claimsService.Object,
            NullLogger<ClassStudentProgressService>.Instance);
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
            Status = AccountStatus.Active,
            IsDeleted = false,
        });
    }

    private void SeedCurriculum(bool includeLive = false)
    {
        var module = new Module
        {
            Id = _moduleId,
            Code = "MOD-001",
            Name = "Module 1",
            ProgramId = _programId,
            ModuleType = ModuleType.Theory,
            ModuleOrder = 1,
            IsDeleted = false,
        };

        _db.Programs.Seed(new Program
        {
            Id = _programId,
            Code = "PRG-001",
            Name = "Robotics",
            Category = ProgramCategory.Technology,
            Level = DifficultyLevel.Beginner,
            IsDeleted = false,
            Modules = [module],
        });
        _db.Modules.Seed(module);

        _db.Courses.Seed(new Course
        {
            Id = _courseId,
            Code = "CRS-001",
            Name = "Course 1",
            ModuleId = _moduleId,
            CourseOrder = 1,
            IsDeleted = false,
        });

        _db.Activities.Seed(new Activity
        {
            Id = _activityId,
            Code = "ACT-001",
            Name = "Reading",
            CourseId = _courseId,
            ActivityType = ActivityType.SelfPaced,
            ActivityOrder = 1,
            IsDeleted = false,
        });

        if (includeLive)
        {
            _db.Activities.Seed(new Activity
            {
                Id = _liveActivityId,
                Code = "ACT-LIVE",
                Name = "Live Kickoff",
                CourseId = _courseId,
                ActivityType = ActivityType.LiveOnline,
                ActivityOrder = 2,
                IsDeleted = false,
            });
        }

        _db.Assignments.Seed(new Assignment
        {
            Id = _assignmentId,
            Code = "ASN-001",
            ModuleId = _moduleId,
            CourseId = _courseId,
            Title = "Homework",
            AssignmentType = AssignmentType.FileUpload,
            MaxPoints = 100,
            PassScore = 50,
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
            MentorId = mentorId ?? _mentorId,
            Status = ClassStatus.InProgress,
            MaxCapacity = 20,
            StartDate = _now.AddDays(-7),
            EndDate = _now.AddDays(30),
            IsDeleted = false,
        });
    }

    private void SeedActiveClassEnrollment(Guid studentId, Guid programEnrollmentId)
    {
        _db.ProgramEnrollments.Seed(new ProgramEnrollment
        {
            Id = programEnrollmentId,
            StudentId = studentId,
            ProgramId = _programId,
            Status = EnrollmentStatus.Active,
            IsDeleted = false,
        });

        _db.ClassEnrollments.Seed(new ClassEnrollment
        {
            Id = Guid.NewGuid(),
            ClassId = _classId,
            StudentId = studentId,
            ProgramEnrollmentId = programEnrollmentId,
            Status = ClassEnrollmentStatus.Active,
            EnrolledAt = _now,
            IsDeleted = false,
        });
    }

    private void SeedModuleEnrollment(Guid id, Guid studentId, Guid programEnrollmentId, int attemptNumber = 1)
    {
        _db.ModuleEnrollments.Seed(new ModuleEnrollment
        {
            Id = id,
            StudentId = studentId,
            ModuleId = _moduleId,
            ProgramEnrollmentId = programEnrollmentId,
            Status = EnrollmentStatus.Active,
            AttemptNumber = attemptNumber,
            IsDeleted = false,
        });
    }

    [Fact]
    public async Task GetActivityStudentProgress_ThrowsForbidden_WhenNotAssignedMentor()
    {
        SeedUser(_mentorId, RoleType.Mentor, "MENTOR1");
        SeedUser(_otherMentorId, RoleType.Mentor, "MENTOR2");
        SeedCurriculum();
        SeedClass(mentorId: _otherMentorId);

        var sut = CreateSut(_mentorId);

        var ex = await Assert.ThrowsAsync<ForbiddenException>(
            () => sut.GetActivityStudentProgressAsync(_classId, _activityId));

        Assert.Equal(MentorScopeValidator.OwnsClassForbiddenMessage, ex.Message);
    }

    [Fact]
    public async Task GetActivityStudentProgress_ReturnsRosterComplete_WithNotStartDefault()
    {
        SeedUser(_mentorId, RoleType.Mentor, "MENTOR1");
        SeedUser(_student1Id, RoleType.Student, "STU1", "Alice");
        SeedUser(_student2Id, RoleType.Student, "STU2", "Bob");
        SeedCurriculum();
        SeedClass();
        SeedActiveClassEnrollment(_student1Id, _programEnrollment1Id);
        SeedActiveClassEnrollment(_student2Id, _programEnrollment2Id);
        SeedModuleEnrollment(_moduleEnrollment1Id, _student1Id, _programEnrollment1Id);
        SeedModuleEnrollment(_moduleEnrollment2Id, _student2Id, _programEnrollment2Id);

        _db.ActivityProgresses.Seed(new ActivityProgress
        {
            Id = Guid.NewGuid(),
            StudentId = _student1Id,
            ActivityId = _activityId,
            ModuleEnrollmentId = _moduleEnrollment1Id,
            ActivityStatus = ActivityStatus.Done,
            IsCompleted = true,
            CompletedAt = _now,
            CompletionSource = CompletionSource.Manual,
            LastAccessedAt = _now.AddHours(-1),
            IsDeleted = false,
        });

        var sut = CreateSut();
        var result = await sut.GetActivityStudentProgressAsync(_classId, _activityId);

        Assert.Equal(2, result.TotalStudents);
        Assert.Equal(1, result.CompletedCount);
        Assert.Equal(0, result.InProgressCount);
        Assert.Equal(1, result.NotStartedCount);
        Assert.Equal(ActivityType.SelfPaced, result.ActivityType);
        Assert.Null(result.ClassSessionId);

        Assert.Equal(2, result.Students.Count);
        Assert.Equal("Alice", result.Students[0].StudentName);
        Assert.Equal(ActivityStatus.Done, result.Students[0].ActivityStatus);
        Assert.Equal(CompletionSource.Manual, result.Students[0].CompletionSource);
        Assert.Null(result.Students[0].AttendanceStatus);

        Assert.Equal("Bob", result.Students[1].StudentName);
        Assert.Equal(ActivityStatus.NotStart, result.Students[1].ActivityStatus);
    }

    [Fact]
    public async Task GetActivityStudentProgress_Live_IncludesAttendanceFromPrimarySession()
    {
        SeedUser(_mentorId, RoleType.Mentor, "MENTOR1");
        SeedUser(_student1Id, RoleType.Student, "STU1", "Alice");
        SeedCurriculum(includeLive: true);
        SeedClass();
        SeedActiveClassEnrollment(_student1Id, _programEnrollment1Id);
        SeedModuleEnrollment(_moduleEnrollment1Id, _student1Id, _programEnrollment1Id);

        _db.ClassSessions.Seed(new ClassSession
        {
            Id = _sessionId,
            ClassId = _classId,
            ModuleId = _moduleId,
            ActivityId = _liveActivityId,
            SessionKind = SessionKind.LiveOnline,
            Title = "Live 1",
            StartTime = _now.AddHours(-1),
            EndTime = _now.AddHours(1),
            Status = ClassSessionStatus.InProgress,
            IsDeleted = false,
        });

        _db.SessionAttendances.Seed(new SessionAttendance
        {
            Id = Guid.NewGuid(),
            ClassSessionId = _sessionId,
            StudentId = _student1Id,
            ModuleEnrollmentId = _moduleEnrollment1Id,
            Status = AttendanceStatus.Present,
            CheckedInAt = _now.AddMinutes(-30),
            ParticipationMinutes = 25,
            IsDeleted = false,
        });

        var sut = CreateSut();
        var result = await sut.GetActivityStudentProgressAsync(_classId, _liveActivityId);

        Assert.Equal(_sessionId, result.ClassSessionId);
        Assert.Equal(ClassSessionStatus.InProgress, result.SessionStatus);
        var row = Assert.Single(result.Students);
        Assert.Equal(AttendanceStatus.Present, row.AttendanceStatus);
        Assert.Equal(25, row.ParticipationMinutes);
        Assert.Equal(ActivityStatus.NotStart, row.ActivityStatus);
    }

    [Fact]
    public async Task GetAssignmentStudentProgress_RosterComplete_LatestAttemptAndStatus()
    {
        SeedUser(_mentorId, RoleType.Mentor, "MENTOR1");
        SeedUser(_student1Id, RoleType.Student, "STU1", "Alice");
        SeedUser(_student2Id, RoleType.Student, "STU2", "Bob");
        SeedCurriculum();
        SeedClass();
        SeedActiveClassEnrollment(_student1Id, _programEnrollment1Id);
        SeedActiveClassEnrollment(_student2Id, _programEnrollment2Id);
        SeedModuleEnrollment(_moduleEnrollment1Id, _student1Id, _programEnrollment1Id);
        SeedModuleEnrollment(_moduleEnrollment2Id, _student2Id, _programEnrollment2Id);

        var olderId = Guid.Parse("b1b1b1b1-b1b1-b1b1-b1b1-b1b1b1b1b1b1");
        var latestId = Guid.Parse("b2b2b2b2-b2b2-b2b2-b2b2-b2b2b2b2b2b2");

        _db.Submissions.Seed(
            new Submission
            {
                Id = olderId,
                Code = "SUB-OLD",
                AssignmentId = _assignmentId,
                StudentId = _student1Id,
                ModuleEnrollmentId = _moduleEnrollment1Id,
                Status = SubmissionStatus.Graded,
                AssignedGrade = 40m,
                AttemptNumber = 1,
                SubmittedAt = _now.AddDays(-2),
                GradedAt = _now.AddDays(-1),
                IsDeleted = false,
            },
            new Submission
            {
                Id = latestId,
                Code = "SUB-NEW",
                AssignmentId = _assignmentId,
                StudentId = _student1Id,
                ModuleEnrollmentId = _moduleEnrollment1Id,
                Status = SubmissionStatus.TurnedIn,
                AttemptNumber = 2,
                SubmittedAt = _now,
                IsDeleted = false,
            });

        var sut = CreateSut();
        var result = await sut.GetAssignmentStudentProgressAsync(_classId, _assignmentId);

        Assert.Equal(2, result.TotalStudents);
        Assert.Equal(1, result.SubmittedCount);
        Assert.Equal(0, result.GradedCount);
        Assert.Equal(1, result.NotStartedCount);
        Assert.Equal(CurriculumStatusHelper.StatusSubmitted, result.Status);

        var alice = result.Students.Single(s => s.StudentId == _student1Id);
        Assert.Equal(latestId, alice.SubmissionId);
        Assert.Equal(2, alice.AttemptNumber);
        Assert.Equal(SubmissionStatus.TurnedIn, alice.SubmissionStatus);
        Assert.Null(alice.Passed);

        var bob = result.Students.Single(s => s.StudentId == _student2Id);
        Assert.Null(bob.SubmissionId);
        Assert.Null(bob.SubmissionStatus);
    }

    [Fact]
    public async Task GetAssignmentStudentProgress_AllGraded_IsCompleted()
    {
        SeedUser(_mentorId, RoleType.Mentor, "MENTOR1");
        SeedUser(_student1Id, RoleType.Student, "STU1", "Alice");
        SeedUser(_student2Id, RoleType.Student, "STU2", "Bob");
        SeedCurriculum();
        SeedClass();
        SeedActiveClassEnrollment(_student1Id, _programEnrollment1Id);
        SeedActiveClassEnrollment(_student2Id, _programEnrollment2Id);
        SeedModuleEnrollment(_moduleEnrollment1Id, _student1Id, _programEnrollment1Id);
        SeedModuleEnrollment(_moduleEnrollment2Id, _student2Id, _programEnrollment2Id);

        _db.Submissions.Seed(
            new Submission
            {
                Id = Guid.NewGuid(),
                Code = "SUB-A",
                AssignmentId = _assignmentId,
                StudentId = _student1Id,
                ModuleEnrollmentId = _moduleEnrollment1Id,
                Status = SubmissionStatus.Graded,
                AssignedGrade = 40m,
                AttemptNumber = 1,
                SubmittedAt = _now,
                GradedAt = _now,
                IsDeleted = false,
            },
            new Submission
            {
                Id = Guid.NewGuid(),
                Code = "SUB-B",
                AssignmentId = _assignmentId,
                StudentId = _student2Id,
                ModuleEnrollmentId = _moduleEnrollment2Id,
                Status = SubmissionStatus.Graded,
                AssignedGrade = 90m,
                AttemptNumber = 1,
                SubmittedAt = _now,
                GradedAt = _now,
                IsDeleted = false,
            });

        var sut = CreateSut();
        var result = await sut.GetAssignmentStudentProgressAsync(_classId, _assignmentId);

        Assert.Equal(CurriculumStatusHelper.StatusCompleted, result.Status);
        Assert.Equal(2, result.GradedCount);
        Assert.Equal(2, result.SubmittedCount);
        Assert.Equal(0, result.NotStartedCount);
        Assert.Equal(65d, result.AverageScore);

        var alice = result.Students.Single(s => s.StudentId == _student1Id);
        Assert.False(alice.Passed);
        var bob = result.Students.Single(s => s.StudentId == _student2Id);
        Assert.True(bob.Passed);
    }
}
