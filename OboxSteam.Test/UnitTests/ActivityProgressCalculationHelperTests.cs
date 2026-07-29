using OboxSteam.Application.Commons;
using OboxSteam.Application.Exceptions;
using OboxSteam.Application.Utils;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;
using OboxSteam.Test.Helpers;

namespace OboxSteam.Test.UnitTests;

public sealed class ActivityProgressCalculationHelperTests
{
    private readonly InMemoryUnitOfWork _db = new();
    private readonly Guid _studentId = Guid.NewGuid();
    private readonly Guid _programId = Guid.NewGuid();
    private readonly Guid _programEnrollmentId = Guid.NewGuid();
    private readonly Guid _moduleId = Guid.NewGuid();
    private readonly Guid _moduleEnrollmentId = Guid.NewGuid();
    private readonly Guid _courseId = Guid.NewGuid();
    private readonly Guid _activityId = Guid.NewGuid();
    private readonly Guid _assignmentId = Guid.NewGuid();

    public ActivityProgressCalculationHelperTests()
    {
        _db.Programs.Seed(new Program
        {
            Id = _programId,
            Code = "PRG-001",
            Name = "STEM",
            Category = ProgramCategory.Technology,
            Level = DifficultyLevel.Beginner,
            IsDeleted = false,
        });
        _db.Modules.Seed(new Module
        {
            Id = _moduleId,
            Code = "MOD-001",
            Name = "Module 1",
            ProgramId = _programId,
            ModuleType = ModuleType.Theory,
            IsDeleted = false,
        });
        _db.Courses.Seed(new Course
        {
            Id = _courseId,
            Code = "CRS-001",
            Name = "Intro",
            ModuleId = _moduleId,
            IsDeleted = false,
        });
        _db.Activities.Seed(new Activity
        {
            Id = _activityId,
            Code = "ACT-001",
            Name = "Lesson",
            CourseId = _courseId,
            ActivityType = ActivityType.SelfPaced,
            ActivityOrder = 1,
            IsDeleted = false,
        });
        _db.Assignments.Seed(new Assignment
        {
            Id = _assignmentId,
            Code = "ASN-001",
            ModuleId = _moduleId,
            Title = "Quiz",
            AssignmentType = AssignmentType.FileUpload,
            MaxPoints = 10,
            PassScore = 5,
            MaxAttempts = 2,
            IsRequiredForModulePass = true,
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
    }

    private ModuleEnrollment SeedModuleEnrollment(EnrollmentStatus status = EnrollmentStatus.Active)
    {
        var enrollment = new ModuleEnrollment
        {
            Id = _moduleEnrollmentId,
            StudentId = _studentId,
            ModuleId = _moduleId,
            ProgramEnrollmentId = _programEnrollmentId,
            Status = status,
            AttemptNumber = 1,
            IsDeleted = false,
        };
        _db.ModuleEnrollments.Seed(enrollment);
        return enrollment;
    }

    [Fact]
    public async Task RecalculateModuleProgressAsync_ReturnsZero_WhenNoUnits()
    {
        var emptyModuleId = Guid.NewGuid();
        _db.Modules.Seed(new Module
        {
            Id = emptyModuleId,
            Code = "MOD-EMPTY",
            Name = "Empty",
            ProgramId = _programId,
            ModuleType = ModuleType.Theory,
            IsDeleted = false,
        });
        var enrollment = new ModuleEnrollment
        {
            Id = Guid.NewGuid(),
            StudentId = _studentId,
            ModuleId = emptyModuleId,
            ProgramEnrollmentId = _programEnrollmentId,
            Status = EnrollmentStatus.Active,
            AttemptNumber = 1,
            IsDeleted = false,
        };
        _db.ModuleEnrollments.Seed(enrollment);

        var progress = await ActivityProgressCalculationHelper.RecalculateModuleProgressAsync(_db, enrollment);

        Assert.Equal(0m, progress);
        Assert.Equal(0m, enrollment.ProgressPercent);
    }

    [Fact]
    public async Task RecalculateModuleProgressAsync_CompletesModule_WhenAllUnitsDone()
    {
        var enrollment = SeedModuleEnrollment();
        _db.ActivityProgresses.Seed(new ActivityProgress
        {
            Id = Guid.NewGuid(),
            ModuleEnrollmentId = _moduleEnrollmentId,
            ActivityId = _activityId,
            ActivityStatus = ActivityStatus.Done,
            IsDeleted = false,
        });
        _db.Submissions.Seed(new Submission
        {
            Id = Guid.NewGuid(),
            Code = "SUB-001",
            ModuleEnrollmentId = _moduleEnrollmentId,
            AssignmentId = _assignmentId,
            StudentId = _studentId,
            Status = SubmissionStatus.Graded,
            AssignedGrade = 8,
            AttemptNumber = 1,
            IsDeleted = false,
        });

        var progress = await ActivityProgressCalculationHelper.RecalculateModuleProgressAsync(_db, enrollment);

        Assert.Equal(100m, progress);
        Assert.Equal(EnrollmentStatus.Completed, enrollment.Status);
        Assert.NotNull(enrollment.CompletedAt);
    }

    [Fact]
    public async Task RecalculateModuleProgressAsync_CountsPartialProgress()
    {
        var enrollment = SeedModuleEnrollment();
        _db.ActivityProgresses.Seed(new ActivityProgress
        {
            Id = Guid.NewGuid(),
            ModuleEnrollmentId = _moduleEnrollmentId,
            ActivityId = _activityId,
            ActivityStatus = ActivityStatus.Done,
            IsDeleted = false,
        });

        var progress = await ActivityProgressCalculationHelper.RecalculateModuleProgressAsync(_db, enrollment);

        Assert.Equal(50m, progress);
        Assert.Equal(EnrollmentStatus.Active, enrollment.Status);
    }

    [Fact]
    public async Task GetModuleActivityIdsAsync_IncludesResearchMilestoneActivities()
    {
        var researchModuleId = Guid.NewGuid();
        var milestoneId = Guid.NewGuid();
        var linkedActivityId = Guid.NewGuid();
        _db.Modules.Seed(new Module
        {
            Id = researchModuleId,
            Code = "RES-001",
            Name = "Capstone",
            ProgramId = _programId,
            ModuleType = ModuleType.Research,
            IsDeleted = false,
        });
        _db.ResearchMilestones.Seed(new ResearchMilestone
        {
            Id = milestoneId,
            ModuleId = researchModuleId,
            Title = "M1",
            MilestoneOrder = 1,
            IsDeleted = false,
        });
        _db.ResearchMilestoneActivities.Seed(new ResearchMilestoneActivity
        {
            Id = Guid.NewGuid(),
            ResearchMilestoneId = milestoneId,
            ActivityId = linkedActivityId,
            IsDeleted = false,
        });
        _db.Activities.Seed(new Activity
        {
            Id = linkedActivityId,
            Code = "LAB-001",
            Name = "Lab",
            ActivityType = ActivityType.Offline,
            ActivityOrder = 1,
            IsDeleted = false,
        });

        var ids = await ActivityProgressCalculationHelper.GetModuleActivityIdsAsync(_db, researchModuleId);

        Assert.Contains(linkedActivityId, ids);
    }

    [Fact]
    public async Task RecalculateProgramProgressAsync_UpdatesProgramEnrollment()
    {
        var enrollment = SeedModuleEnrollment();
        _db.ActivityProgresses.Seed(new ActivityProgress
        {
            Id = Guid.NewGuid(),
            ModuleEnrollmentId = _moduleEnrollmentId,
            ActivityId = _activityId,
            ActivityStatus = ActivityStatus.Done,
            IsDeleted = false,
        });
        _db.Submissions.Seed(new Submission
        {
            Id = Guid.NewGuid(),
            Code = "SUB-001",
            ModuleEnrollmentId = _moduleEnrollmentId,
            AssignmentId = _assignmentId,
            StudentId = _studentId,
            Status = SubmissionStatus.Graded,
            AssignedGrade = 10,
            AttemptNumber = 1,
            IsDeleted = false,
        });
        await ActivityProgressCalculationHelper.RecalculateModuleProgressAsync(_db, enrollment);

        var programProgress = await ActivityProgressCalculationHelper.RecalculateProgramProgressAsync(
            _db, _programEnrollmentId, enrollment);

        Assert.Equal(100m, programProgress);
        Assert.Equal(EnrollmentStatus.Completed, _db.ProgramEnrollments.Items.Single().Status);
    }

    [Fact]
    public async Task RecalculateProgramProgressAsync_Throws_WhenEnrollmentMissing()
    {
        var enrollment = SeedModuleEnrollment();

        await Assert.ThrowsAsync<NotFoundException>(() =>
            ActivityProgressCalculationHelper.RecalculateProgramProgressAsync(
                _db, Guid.NewGuid(), enrollment));
    }

    [Fact]
    public void ApiResult_Factories_BuildSuccessAndFailure()
    {
        var ok = ApiResult.Success("201", "Created");
        Assert.True(ok.IsSuccess);
        Assert.Equal("201", ok.Value!.Code);

        var fail = ApiResult.Failure("422", "Invalid");
        Assert.False(fail.IsSuccess);
        Assert.Equal("422", fail.Error!.Code);

        var okData = ApiResult<string>.Success("payload");
        Assert.Equal("payload", okData.Value!.Data);

        var failData = ApiResult<int>.Failure();
        Assert.False(failData.IsSuccess);
    }

    [Fact]
    public void ErrorHelper_Internal_ReturnsInternalException()
    {
        var ex = Assert.IsType<InternalException>(ErrorHelper.Internal("boom"));
        Assert.Equal("boom", ex.Message);
    }
}
