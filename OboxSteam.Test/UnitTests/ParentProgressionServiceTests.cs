using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OboxSteam.Application.Commons;
using OboxSteam.Application.Exceptions;
using OboxSteam.Application.Interfaces;
using OboxSteam.Application.Services;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;
using OboxSteam.Test.Helpers;

namespace OboxSteam.Test.UnitTests;

public sealed class ParentProgressionServiceTests
{
    private readonly Guid _parentId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private readonly Guid _studentId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private readonly Guid _otherStudentId = Guid.Parse("12121212-1212-1212-1212-121212121212");
    private readonly Guid _programId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private readonly Guid _programEnrollmentId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private readonly Guid _theoryModuleId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private readonly Guid _researchModuleId = Guid.Parse("45454545-4545-4545-4545-454545454545");
    private readonly Guid _courseId = Guid.Parse("55555555-5555-5555-5555-555555555555");
    private readonly Guid _activity1Id = Guid.Parse("66666666-6666-6666-6666-666666666666");
    private readonly Guid _activity2Id = Guid.Parse("67676767-6767-6767-6767-676767676767");
    private readonly Guid _moduleAssignmentId = Guid.Parse("6a6a6a6a-6a6a-6a6a-6a6a-6a6a6a6a6a6a");
    private readonly Guid _moduleEnrollmentId = Guid.Parse("88888888-8888-8888-8888-888888888888");
    private readonly Guid _classId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private readonly Guid _mentorId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");

    private readonly InMemoryUnitOfWork _db = new();
    private readonly Mock<IClaimsService> _claimsService = new();

    private ParentProgressionService CreateSut(Guid? currentUserId = null)
    {
        _claimsService.Setup(c => c.GetCurrentUserId).Returns(currentUserId ?? _parentId);
        return new ParentProgressionService(
            _db,
            _claimsService.Object,
            NullLogger<ParentProgressionService>.Instance);
    }

    private void SeedParentAndVerifiedLink(bool verified = true)
    {
        _db.Users.Seed(new User
        {
            Id = _parentId,
            Code = "PAR-001",
            Email = "parent@test.com",
            FullName = "Parent User",
            Role = RoleType.Parent,
            IsDeleted = false,
        });
        _db.Users.Seed(new User
        {
            Id = _studentId,
            Code = "STD-001",
            Email = "student@test.com",
            FullName = "Student User",
            Role = RoleType.Student,
            IsDeleted = false,
        });
        _db.ParentStudents.Seed(new ParentStudent
        {
            Id = Guid.NewGuid(),
            ParentId = _parentId,
            StudentId = _studentId,
            IsVerified = verified,
            CreatedAt = DateTime.UtcNow.AddDays(-30),
            IsDeleted = false,
        });
    }

    private void SeedCurriculumAndEnrollment()
    {
        var theoryModule = new Module
        {
            Id = _theoryModuleId,
            Code = "MOD-THEORY",
            Name = "Theory Module",
            ProgramId = _programId,
            ModuleType = ModuleType.Theory,
            ModuleOrder = 1,
            Price = 100m,
            RetakeFee = 50m,
            IsMandatory = true,
            IsDeleted = false,
        };
        var researchModule = new Module
        {
            Id = _researchModuleId,
            Code = "MOD-RESEARCH",
            Name = "Research Module",
            ProgramId = _programId,
            ModuleType = ModuleType.Research,
            ModuleOrder = 2,
            PrerequisiteModuleId = _theoryModuleId,
            Price = 200m,
            RetakeFee = 75m,
            IsMandatory = true,
            IsDeleted = false,
        };
        var program = new Program
        {
            Id = _programId,
            Code = "PRG-001",
            Name = "STEAM Program",
            Category = ProgramCategory.Technology,
            Level = DifficultyLevel.Beginner,
            Status = "Published",
            ThumbnailUrl = "https://cdn.example/thumb.png",
            Price = 1000m,
            IsDeleted = false,
            Modules = [theoryModule, researchModule],
        };
        _db.Programs.Seed(program);
        _db.Modules.Seed(theoryModule, researchModule);

        _db.Courses.Seed(new Course
        {
            Id = _courseId,
            Code = "CRS-001",
            Name = "Intro Course",
            ModuleId = _theoryModuleId,
            IsDeleted = false,
        });

        _db.Activities.Seed(
            new Activity
            {
                Id = _activity1Id,
                Code = "ACT-001",
                Name = "Video Lesson",
                CourseId = _courseId,
                ActivityType = ActivityType.SelfPaced,
                ActivityOrder = 1,
                IsDeleted = false,
            },
            new Activity
            {
                Id = _activity2Id,
                Code = "ACT-002",
                Name = "Reading Lesson",
                CourseId = _courseId,
                ActivityType = ActivityType.SelfPaced,
                ActivityOrder = 2,
                IsDeleted = false,
            });

        _db.Assignments.Seed(new Assignment
        {
            Id = _moduleAssignmentId,
            Code = "ASG-001",
            Title = "Module Quiz",
            ModuleId = _theoryModuleId,
            AssignmentType = AssignmentType.Quiz,
            MaxPoints = 100,
            PassScore = 70m,
            IsRequiredForModulePass = true,
            MaxAttempts = 2,
            IsDeleted = false,
        });

        _db.ProgramEnrollments.Seed(new ProgramEnrollment
        {
            Id = _programEnrollmentId,
            StudentId = _studentId,
            ProgramId = _programId,
            Status = EnrollmentStatus.Active,
            ProgressPercent = 40m,
            EnrolledAt = DateTime.UtcNow.AddDays(-10),
            StartedAt = DateTime.UtcNow.AddDays(-9),
            IsDeleted = false,
            Program = program,
        });

        _db.ModuleEnrollments.Seed(new ModuleEnrollment
        {
            Id = _moduleEnrollmentId,
            StudentId = _studentId,
            ModuleId = _theoryModuleId,
            ProgramEnrollmentId = _programEnrollmentId,
            Status = EnrollmentStatus.Active,
            ProgressPercent = 50m,
            AttemptNumber = 1,
            EnrolledAt = DateTime.UtcNow.AddDays(-9),
            StartedAt = DateTime.UtcNow.AddDays(-8),
            IsDeleted = false,
        });

        _db.ActivityProgresses.Seed(new ActivityProgress
        {
            Id = Guid.NewGuid(),
            StudentId = _studentId,
            ActivityId = _activity1Id,
            ModuleEnrollmentId = _moduleEnrollmentId,
            ActivityStatus = ActivityStatus.Done,
            IsCompleted = true,
            CompletedAt = DateTime.UtcNow.AddDays(-2),
            LastAccessedAt = DateTime.UtcNow.AddHours(-3),
            IsDeleted = false,
        });
    }

    [Fact]
    public async Task GetChildProgression_ReturnsBrief_ForVerifiedParent()
    {
        SeedParentAndVerifiedLink();
        SeedCurriculumAndEnrollment();
        var sut = CreateSut();

        var result = await sut.GetChildProgressionAsync(_studentId);

        Assert.Equal(_studentId, result.Student.LinkedUserId);
        Assert.True(result.Student.IsVerified);
        Assert.Equal(1, result.Summary.ActiveEnrollmentCount);
        Assert.Single(result.Enrollments);
        Assert.Equal(_programEnrollmentId, result.Enrollments[0].EnrollmentId);
        Assert.Equal("STEAM Program", result.Enrollments[0].ProgramName);
        Assert.NotNull(result.Enrollments[0].CurrentModule);
        Assert.Equal(_theoryModuleId, result.Enrollments[0].CurrentModule!.ModuleId);
        Assert.NotNull(result.Enrollments[0].CurrentActivity);
        Assert.Equal(_activity2Id, result.Enrollments[0].CurrentActivity!.ActivityId);
        Assert.Contains(result.RecentMilestones, m => m.Type == ParentProgressEventType.ActivityCompleted);
    }

    [Fact]
    public async Task GetChildProgression_ThrowsForbidden_WhenUnverified()
    {
        SeedParentAndVerifiedLink(verified: false);
        SeedCurriculumAndEnrollment();
        var sut = CreateSut();

        await Assert.ThrowsAsync<ForbiddenException>(() => sut.GetChildProgressionAsync(_studentId));
    }

    [Fact]
    public async Task GetChildProgression_ThrowsNotFound_WhenStudentUnknown()
    {
        SeedParentAndVerifiedLink();
        var sut = CreateSut();

        await Assert.ThrowsAsync<NotFoundException>(() =>
            sut.GetChildProgressionAsync(Guid.Parse("99999999-9999-9999-9999-999999999999")));
    }

    [Fact]
    public async Task GetEnrollmentProgression_ReturnsModulesAndAssignments()
    {
        SeedParentAndVerifiedLink();
        SeedCurriculumAndEnrollment();
        _db.Users.Seed(new User
        {
            Id = _mentorId,
            Code = "MNT-001",
            Email = "mentor@test.com",
            FullName = "Mentor One",
            Role = RoleType.Mentor,
            IsDeleted = false,
        });
        _db.Classes.Seed(new Class
        {
            Id = _classId,
            Code = "CLS-001",
            Name = "Cohort A",
            ProgramId = _programId,
            MentorId = _mentorId,
            StartDate = DateTime.UtcNow.AddDays(-30),
            EndDate = DateTime.UtcNow.AddDays(30),
            MaxCapacity = 30,
            Status = ClassStatus.InProgress,
            IsDeleted = false,
        });
        _db.ClassEnrollments.Seed(new ClassEnrollment
        {
            Id = Guid.NewGuid(),
            ClassId = _classId,
            StudentId = _studentId,
            ProgramEnrollmentId = _programEnrollmentId,
            Status = ClassEnrollmentStatus.Active,
            EnrolledAt = DateTime.UtcNow.AddDays(-10),
            IsDeleted = false,
        });

        var sut = CreateSut();
        var result = await sut.GetEnrollmentProgressionAsync(_studentId, _programEnrollmentId);

        Assert.Equal(_studentId, result.StudentId);
        Assert.Equal(_programEnrollmentId, result.Enrollment.EnrollmentId);
        Assert.NotNull(result.ClassInfo);
        Assert.Equal("Cohort A", result.ClassInfo!.ClassName);
        Assert.Equal("Mentor One", result.ClassInfo.MentorName);
        Assert.Equal(2, result.Modules.Count);
        Assert.False(result.Modules[0].IsLocked);
        Assert.True(result.Modules[1].IsLocked);
        Assert.Equal(2, result.Modules[0].ActivityStats.Total);
        Assert.Equal(1, result.Modules[0].ActivityStats.Completed);
        Assert.Contains(result.Modules[0].Assignments, a => a.AssignmentId == _moduleAssignmentId);
        Assert.Equal(ParentModuleOutcomeLabel.InProgress, result.Modules[0].OutcomeLabel);
        Assert.Equal(ParentModuleOutcomeLabel.NotStarted, result.Modules[1].OutcomeLabel);
    }

    [Fact]
    public async Task GetEnrollmentProgression_ThrowsNotFound_WhenEnrollmentBelongsToOtherStudent()
    {
        SeedParentAndVerifiedLink();
        SeedCurriculumAndEnrollment();
        _db.Users.Seed(new User
        {
            Id = _otherStudentId,
            Code = "STD-002",
            Email = "other@test.com",
            Role = RoleType.Student,
            IsDeleted = false,
        });
        _db.ParentStudents.Seed(new ParentStudent
        {
            Id = Guid.NewGuid(),
            ParentId = _parentId,
            StudentId = _otherStudentId,
            IsVerified = true,
            IsDeleted = false,
        });

        var sut = CreateSut();
        await Assert.ThrowsAsync<NotFoundException>(() =>
            sut.GetEnrollmentProgressionAsync(_otherStudentId, _programEnrollmentId));
    }

    [Fact]
    public async Task GetEnrollmentProgression_MapsCompletedAssignmentOutcome()
    {
        SeedParentAndVerifiedLink();
        SeedCurriculumAndEnrollment();
        _db.Submissions.Seed(new Submission
        {
            Id = Guid.NewGuid(),
            StudentId = _studentId,
            AssignmentId = _moduleAssignmentId,
            ModuleEnrollmentId = _moduleEnrollmentId,
            Status = SubmissionStatus.Graded,
            AssignedGrade = 85m,
            AttemptNumber = 1,
            SubmittedAt = DateTime.UtcNow.AddDays(-1),
            GradedAt = DateTime.UtcNow.AddHours(-12),
            IsDeleted = false,
        });

        // Unlock module assignment by completing all activities
        _db.ActivityProgresses.Seed(new ActivityProgress
        {
            Id = Guid.NewGuid(),
            StudentId = _studentId,
            ActivityId = _activity2Id,
            ModuleEnrollmentId = _moduleEnrollmentId,
            ActivityStatus = ActivityStatus.Done,
            IsCompleted = true,
            CompletedAt = DateTime.UtcNow.AddDays(-1),
            IsDeleted = false,
        });

        var sut = CreateSut();
        var result = await sut.GetEnrollmentProgressionAsync(_studentId, _programEnrollmentId);
        var assignment = result.Modules[0].Assignments.Single(a => a.AssignmentId == _moduleAssignmentId);

        Assert.Equal(CurriculumStatusHelper.StatusCompleted, assignment.Status);
        Assert.Equal(85m, assignment.Score);
        Assert.True(assignment.Passed);
        Assert.Equal(1, assignment.AttemptUsed);
        Assert.Equal(2, assignment.MaxAttempts);
    }
}
