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

public sealed class ClassCurriculumProgressServiceTests
{
    private readonly Guid _mentorId = Guid.Parse("14141414-1414-1414-1414-141414141414");
    private readonly Guid _otherMentorId = Guid.Parse("15151515-1515-1515-1515-151515151515");
    private readonly Guid _student1Id = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private readonly Guid _student2Id = Guid.Parse("12121212-1212-1212-1212-121212121212");
    private readonly Guid _student3Id = Guid.Parse("13131313-1313-1313-1313-131313131313");
    private readonly Guid _programId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private readonly Guid _moduleId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private readonly Guid _courseId = Guid.Parse("34343434-3434-3434-3434-343434343434");
    private readonly Guid _activity1Id = Guid.Parse("35353535-3535-3535-3535-353535353535");
    private readonly Guid _activity2Id = Guid.Parse("36363636-3636-3636-3636-363636363636");
    private readonly Guid _assignmentId = Guid.Parse("55555555-5555-5555-5555-555555555555");
    private readonly Guid _classId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private readonly Guid _programEnrollment1Id = Guid.Parse("99999999-9999-9999-9999-999999999999");
    private readonly Guid _programEnrollment2Id = Guid.Parse("98989898-9898-9898-9898-989898989898");
    private readonly Guid _programEnrollment3Id = Guid.Parse("97979797-9797-9797-9797-979797979797");
    private readonly Guid _moduleEnrollment1Id = Guid.Parse("a1a1a1a1-a1a1-a1a1-a1a1-a1a1a1a1a1a1");
    private readonly Guid _moduleEnrollment2Id = Guid.Parse("a2a2a2a2-a2a2-a2a2-a2a2-a2a2a2a2a2a2");
    private readonly Guid _moduleEnrollment3Id = Guid.Parse("a3a3a3a3-a3a3-a3a3-a3a3-a3a3a3a3a3a3");

    private readonly DateTime _now = DateTime.UtcNow;

    private readonly InMemoryUnitOfWork _db = new();
    private readonly Mock<IClaimsService> _claimsService = new();

    private ClassCurriculumProgressService CreateSut(Guid? currentUserId = null)
    {
        _claimsService.Setup(c => c.GetCurrentUserId).Returns(currentUserId ?? _mentorId);
        return new ClassCurriculumProgressService(
            _db,
            _claimsService.Object,
            NullLogger<ClassCurriculumProgressService>.Instance);
    }

    private void SeedUser(Guid id, RoleType role, string code)
    {
        _db.Users.Seed(new User
        {
            Id = id,
            Code = code,
            Email = $"{code.ToLower()}@test.com",
            FullName = code,
            Role = role,
            Status = AccountStatus.Active,
            IsDeleted = false,
        });
    }

    private void SeedCurriculum()
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

        _db.Activities.Seed(
            new Activity
            {
                Id = _activity1Id,
                Code = "ACT-001",
                Name = "Lesson 1",
                CourseId = _courseId,
                ActivityType = ActivityType.SelfPaced,
                ActivityOrder = 1,
                IsDeleted = false,
            },
            new Activity
            {
                Id = _activity2Id,
                Code = "ACT-002",
                Name = "Lesson 2",
                CourseId = _courseId,
                ActivityType = ActivityType.SelfPaced,
                ActivityOrder = 2,
                IsDeleted = false,
            });

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

    private void SeedActivityProgress(
        Guid studentId,
        Guid activityId,
        Guid moduleEnrollmentId,
        ActivityStatus status)
    {
        _db.ActivityProgresses.Seed(new ActivityProgress
        {
            Id = Guid.NewGuid(),
            StudentId = studentId,
            ActivityId = activityId,
            ModuleEnrollmentId = moduleEnrollmentId,
            ActivityStatus = status,
            IsCompleted = status == ActivityStatus.Done,
            CompletedAt = status == ActivityStatus.Done ? _now : null,
            IsDeleted = false,
        });
    }

    private void SeedSubmission(
        Guid studentId,
        SubmissionStatus status,
        decimal? grade = null)
    {
        var moduleEnrollmentId = _db.ModuleEnrollments.Items
            .FirstOrDefault(me => me.StudentId == studentId && !me.IsDeleted)?.Id;

        _db.Submissions.Seed(new Submission
        {
            Id = Guid.NewGuid(),
            Code = $"SUB-{Guid.NewGuid():N}"[..20],
            AssignmentId = _assignmentId,
            StudentId = studentId,
            ModuleEnrollmentId = moduleEnrollmentId,
            Status = status,
            AssignedGrade = grade,
            AttemptNumber = 1,
            SubmittedAt = _now,
            IsDeleted = false,
        });
    }

    [Fact]
    public async Task GetCurriculumProgress_ThrowsForbidden_WhenNotAssignedMentor()
    {
        SeedUser(_mentorId, RoleType.Mentor, "MENTOR1");
        SeedUser(_otherMentorId, RoleType.Mentor, "MENTOR2");
        SeedCurriculum();
        SeedClass(mentorId: _otherMentorId);

        var sut = CreateSut(_mentorId);

        var ex = await Assert.ThrowsAsync<ForbiddenException>(
            () => sut.GetCurriculumProgressAsync(_classId));

        Assert.Equal(MentorScopeValidator.OwnsClassForbiddenMessage, ex.Message);
    }

    [Fact]
    public async Task GetCurriculumProgress_ThrowsForbidden_WhenCallerIsNotMentor()
    {
        SeedUser(_mentorId, RoleType.Manager, "MGR1");
        SeedCurriculum();
        SeedClass();

        var sut = CreateSut(_mentorId);

        await Assert.ThrowsAsync<ForbiddenException>(
            () => sut.GetCurriculumProgressAsync(_classId));
    }

    [Fact]
    public async Task GetCurriculumProgress_ReturnsZeros_WhenNoActiveStudents()
    {
        SeedUser(_mentorId, RoleType.Mentor, "MENTOR1");
        SeedCurriculum();
        SeedClass();

        var sut = CreateSut();
        var result = await sut.GetCurriculumProgressAsync(_classId);

        Assert.Equal(_classId, result.ClassId);
        Assert.Equal(0, result.TotalStudents);
        Assert.Equal(_activity1Id, result.CurrentActivityId);
        Assert.Single(result.Modules);
        Assert.Equal(_moduleId, result.Modules[0].ModuleId);
        Assert.Equal(2, result.Modules[0].Activities.Count);
        Assert.All(
            result.Modules[0].Activities,
            activity =>
            {
                Assert.Equal(0, activity.CompletedCount);
                Assert.Equal(0, activity.InProgressCount);
                Assert.Null(activity.ClassSessionId);
                Assert.Null(activity.SessionStatus);
            });
        Assert.Equal(
            CurriculumStatusHelper.StatusCurrent,
            result.Modules[0].Activities.Single(a => a.ActivityId == _activity1Id).Status);
        Assert.Equal(
            CurriculumStatusHelper.StatusAvailable,
            result.Modules[0].Activities.Single(a => a.ActivityId == _activity2Id).Status);
        Assert.Single(result.Modules[0].Assignments);
        Assert.Equal(CurriculumStatusHelper.StatusAvailable, result.Modules[0].Assignments[0].Status);
        Assert.Equal(0, result.Modules[0].Assignments[0].SubmittedCount);
        Assert.Equal(0, result.Modules[0].Assignments[0].GradedCount);
        Assert.Null(result.Modules[0].Assignments[0].AverageScore);
    }

    [Fact]
    public async Task GetCurriculumProgress_AggregatesActivityAndAssignmentCounts()
    {
        SeedUser(_mentorId, RoleType.Mentor, "MENTOR1");
        SeedUser(_student1Id, RoleType.Student, "STU1");
        SeedUser(_student2Id, RoleType.Student, "STU2");
        SeedUser(_student3Id, RoleType.Student, "STU3");
        SeedCurriculum();
        SeedClass();

        SeedActiveClassEnrollment(_student1Id, _programEnrollment1Id);
        SeedActiveClassEnrollment(_student2Id, _programEnrollment2Id);
        SeedActiveClassEnrollment(_student3Id, _programEnrollment3Id);

        SeedModuleEnrollment(_moduleEnrollment1Id, _student1Id, _programEnrollment1Id);
        SeedModuleEnrollment(_moduleEnrollment2Id, _student2Id, _programEnrollment2Id);
        SeedModuleEnrollment(_moduleEnrollment3Id, _student3Id, _programEnrollment3Id);

        SeedActivityProgress(_student1Id, _activity1Id, _moduleEnrollment1Id, ActivityStatus.Done);
        SeedActivityProgress(_student2Id, _activity1Id, _moduleEnrollment2Id, ActivityStatus.Done);
        SeedActivityProgress(_student3Id, _activity1Id, _moduleEnrollment3Id, ActivityStatus.InProgress);

        SeedActivityProgress(_student1Id, _activity2Id, _moduleEnrollment1Id, ActivityStatus.InProgress);

        SeedSubmission(_student1Id, SubmissionStatus.Graded, 80m);
        SeedSubmission(_student2Id, SubmissionStatus.TurnedIn);
        SeedSubmission(_student3Id, SubmissionStatus.Pending);

        var sut = CreateSut();
        var result = await sut.GetCurriculumProgressAsync(_classId);

        Assert.Equal(3, result.TotalStudents);
        Assert.Equal(_activity1Id, result.CurrentActivityId);

        var module = Assert.Single(result.Modules);
        var activity1 = module.Activities.Single(a => a.ActivityId == _activity1Id);
        Assert.Equal(2, activity1.CompletedCount);
        Assert.Equal(1, activity1.InProgressCount);
        Assert.Equal(CurriculumStatusHelper.StatusCurrent, activity1.Status);

        var activity2 = module.Activities.Single(a => a.ActivityId == _activity2Id);
        Assert.Equal(0, activity2.CompletedCount);
        Assert.Equal(1, activity2.InProgressCount);
        Assert.Equal(CurriculumStatusHelper.StatusAvailable, activity2.Status);

        var assignment = Assert.Single(module.Assignments);
        Assert.Equal(_assignmentId, assignment.AssignmentId);
        Assert.Equal(CurriculumStatusHelper.StatusSubmitted, assignment.Status);
        Assert.Equal(2, assignment.SubmittedCount);
        Assert.Equal(1, assignment.GradedCount);
        Assert.Equal(80d, assignment.AverageScore);
    }

    [Fact]
    public async Task GetCurriculumProgress_UsesLatestModuleAttempt_ForActivityCounts()
    {
        SeedUser(_mentorId, RoleType.Mentor, "MENTOR1");
        SeedUser(_student1Id, RoleType.Student, "STU1");
        SeedCurriculum();
        SeedClass();
        SeedActiveClassEnrollment(_student1Id, _programEnrollment1Id);

        var oldAttemptId = Guid.Parse("b1b1b1b1-b1b1-b1b1-b1b1-b1b1b1b1b1b1");
        var newAttemptId = Guid.Parse("b2b2b2b2-b2b2-b2b2-b2b2-b2b2b2b2b2b2");
        SeedModuleEnrollment(oldAttemptId, _student1Id, _programEnrollment1Id, attemptNumber: 1);
        SeedModuleEnrollment(newAttemptId, _student1Id, _programEnrollment1Id, attemptNumber: 2);

        SeedActivityProgress(_student1Id, _activity1Id, oldAttemptId, ActivityStatus.Done);
        SeedActivityProgress(_student1Id, _activity1Id, newAttemptId, ActivityStatus.InProgress);

        var sut = CreateSut();
        var result = await sut.GetCurriculumProgressAsync(_classId);

        var activity1 = result.Modules[0].Activities.Single(a => a.ActivityId == _activity1Id);
        Assert.Equal(0, activity1.CompletedCount);
        Assert.Equal(1, activity1.InProgressCount);
    }

    [Fact]
    public async Task GetCurriculumProgress_IgnoresInactiveClassEnrollments()
    {
        SeedUser(_mentorId, RoleType.Mentor, "MENTOR1");
        SeedUser(_student1Id, RoleType.Student, "STU1");
        SeedUser(_student2Id, RoleType.Student, "STU2");
        SeedCurriculum();
        SeedClass();

        SeedActiveClassEnrollment(_student1Id, _programEnrollment1Id);
        SeedModuleEnrollment(_moduleEnrollment1Id, _student1Id, _programEnrollment1Id);
        SeedActivityProgress(_student1Id, _activity1Id, _moduleEnrollment1Id, ActivityStatus.Done);
        SeedSubmission(_student1Id, SubmissionStatus.Graded, 90m);

        _db.ProgramEnrollments.Seed(new ProgramEnrollment
        {
            Id = _programEnrollment2Id,
            StudentId = _student2Id,
            ProgramId = _programId,
            Status = EnrollmentStatus.Active,
            IsDeleted = false,
        });
        _db.ClassEnrollments.Seed(new ClassEnrollment
        {
            Id = Guid.NewGuid(),
            ClassId = _classId,
            StudentId = _student2Id,
            ProgramEnrollmentId = _programEnrollment2Id,
            Status = ClassEnrollmentStatus.Withdrawn,
            EnrolledAt = _now,
            IsDeleted = false,
        });
        SeedModuleEnrollment(_moduleEnrollment2Id, _student2Id, _programEnrollment2Id);
        SeedActivityProgress(_student2Id, _activity1Id, _moduleEnrollment2Id, ActivityStatus.Done);
        SeedSubmission(_student2Id, SubmissionStatus.Graded, 70m);

        var sut = CreateSut();
        var result = await sut.GetCurriculumProgressAsync(_classId);

        Assert.Equal(1, result.TotalStudents);
        var activity1 = result.Modules[0].Activities.Single(a => a.ActivityId == _activity1Id);
        Assert.Equal(1, activity1.CompletedCount);
        Assert.Equal(1, result.Modules[0].Assignments[0].SubmittedCount);
        Assert.Equal(90d, result.Modules[0].Assignments[0].AverageScore);
    }

    [Fact]
    public async Task GetCurriculumProgress_LiveSessionCompleted_MarksActivityCompleted()
    {
        SeedUser(_mentorId, RoleType.Mentor, "MENTOR1");
        SeedUser(_student1Id, RoleType.Student, "STU1");
        SeedCurriculumWithLiveFirst();
        SeedClass();
        SeedActiveClassEnrollment(_student1Id, _programEnrollment1Id);
        SeedModuleEnrollment(_moduleEnrollment1Id, _student1Id, _programEnrollment1Id);

        var sessionId = Guid.Parse("c1c1c1c1-c1c1-c1c1-c1c1-c1c1c1c1c1c1");
        _db.ClassSessions.Seed(new ClassSession
        {
            Id = sessionId,
            ClassId = _classId,
            ModuleId = _moduleId,
            ActivityId = _activity1Id,
            SessionKind = SessionKind.LiveOnline,
            Title = "Live 1",
            StartTime = _now.AddHours(-2),
            EndTime = _now.AddHours(-1),
            Status = ClassSessionStatus.Completed,
            IsDeleted = false,
        });

        var sut = CreateSut();
        var result = await sut.GetCurriculumProgressAsync(_classId);

        var live = result.Modules[0].Activities.Single(a => a.ActivityId == _activity1Id);
        Assert.Equal(CurriculumStatusHelper.StatusCompleted, live.Status);
        Assert.Equal(sessionId, live.ClassSessionId);
        Assert.Equal(ClassSessionStatus.Completed, live.SessionStatus);
        Assert.Equal(_activity2Id, result.CurrentActivityId);
    }

    [Fact]
    public async Task GetCurriculumProgress_AssignmentAllGraded_IsCompleted()
    {
        SeedUser(_mentorId, RoleType.Mentor, "MENTOR1");
        SeedUser(_student1Id, RoleType.Student, "STU1");
        SeedUser(_student2Id, RoleType.Student, "STU2");
        SeedCurriculum();
        SeedClass();
        SeedActiveClassEnrollment(_student1Id, _programEnrollment1Id);
        SeedActiveClassEnrollment(_student2Id, _programEnrollment2Id);
        SeedModuleEnrollment(_moduleEnrollment1Id, _student1Id, _programEnrollment1Id);
        SeedModuleEnrollment(_moduleEnrollment2Id, _student2Id, _programEnrollment2Id);
        SeedSubmission(_student1Id, SubmissionStatus.Graded, 70m);
        SeedSubmission(_student2Id, SubmissionStatus.Graded, 85m);

        var sut = CreateSut();
        var result = await sut.GetCurriculumProgressAsync(_classId);

        var assignment = Assert.Single(result.Modules[0].Assignments);
        Assert.Equal(CurriculumStatusHelper.StatusCompleted, assignment.Status);
        Assert.Equal(2, assignment.GradedCount);
        Assert.Equal(2, assignment.SubmittedCount);
    }

    private void SeedCurriculumWithLiveFirst()
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

        _db.Activities.Seed(
            new Activity
            {
                Id = _activity1Id,
                Code = "ACT-001",
                Name = "Live Kickoff",
                CourseId = _courseId,
                ActivityType = ActivityType.LiveOnline,
                ActivityOrder = 1,
                IsDeleted = false,
            },
            new Activity
            {
                Id = _activity2Id,
                Code = "ACT-002",
                Name = "Reading",
                CourseId = _courseId,
                ActivityType = ActivityType.SelfPaced,
                ActivityOrder = 2,
                IsDeleted = false,
            });

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
}
