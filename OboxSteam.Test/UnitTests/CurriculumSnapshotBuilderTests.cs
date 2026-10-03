using OboxSteam.Application.Commons;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;

namespace OboxSteam.Test.UnitTests;

public sealed class CurriculumSnapshotBuilderTests
{
    [Fact]
    public void BuildCurriculumSnapshot_PreservesRichNestedTreeAndStableIds()
    {
        var programId = Guid.NewGuid();
        var moduleId = Guid.NewGuid();
        var courseId = Guid.NewGuid();
        var activityId = Guid.NewGuid();
        var materialId = Guid.NewGuid();
        var assignmentId = Guid.NewGuid();
        var questionBankId = Guid.NewGuid();

        var program = new OboxSteam.Domain.Entities.Program
        {
            Id = programId,
            Code = "PRG-ROBOTICS",
            Name = "Robotics",
            Description = "Build and test a rover.",
            FrameworkVersionId = Guid.NewGuid(),
        };
        var module = new Module
        {
            Id = moduleId,
            ProgramId = programId,
            Code = "MOD-01",
            Name = "Build",
            ModuleType = ModuleType.Experiential,
            ModuleOrder = 1,
            IsMandatory = true,
            LearningOutcomes = ["Prototype"],
        };
        var course = new Course
        {
            Id = courseId,
            ModuleId = moduleId,
            Code = "CRS-01",
            Name = "Rover Lab",
            CourseOrder = 1,
            Description = "Hands-on lab.",
        };
        var activity = new Activity
        {
            Id = activityId,
            CourseId = courseId,
            Code = "ACT-01",
            Name = "Assemble chassis",
            ActivityType = ActivityType.Offline,
            ActivityOrder = 1,
            Description = "Assemble the frame.",
            DurationMinutes = 90,
            RequireQrCheckin = true,
            RequireMediaEvidence = true,
        };
        var material = new Material
        {
            Id = materialId,
            ActivityId = activityId,
            Title = "Chassis guide",
            MaterialType = MaterialType.PDF,
            FileUrl = "https://cdn.example.test/chassis-guide.pdf",
            FileSizeBytes = 1234,
            IsDeleted = false,
        };
        var assignment = new Assignment
        {
            Id = assignmentId,
            ModuleId = moduleId,
            CourseId = courseId,
            Code = "ASM-01",
            Title = "Rover quiz",
            AssignmentType = AssignmentType.Quiz,
            Description = "Upload the build evidence.",
            MaxPoints = 20,
            PassScore = 12,
            MaxAttempts = 2,
            AllowShuffle = true,
            QuestionBankId = questionBankId,
            QuestionCount = 8,
            ShuffleOptions = true,
            EasyPercent = 40,
            MediumPercent = 40,
            HardPercent = 20,
        };

        var tree = new ProgramCurriculumTreeSnapshot
        {
            Program = program,
            Modules = [module],
            CoursesByModuleId = new() { [moduleId] = [course] },
            ActivitiesByCourseId = new() { [courseId] = [activity] },
            ActivitiesById = new() { [activityId] = activity },
            MaterialsByActivityId = new() { [activityId] = material },
            AssignmentsByCourseId = new() { [courseId] = [assignment] },
            AssignmentsById = new() { [assignmentId] = assignment },
        };

        var snapshot = CurriculumSnapshotBuilder.BuildCurriculumSnapshot(tree);

        Assert.Equal(programId, snapshot.Program.Id);
        Assert.Equal("PRG-ROBOTICS", snapshot.Program.Code);
        var moduleSnapshot = Assert.Single(snapshot.Modules);
        Assert.Equal("MOD-01", moduleSnapshot.Code);
        var courseSnapshot = Assert.Single(moduleSnapshot.Courses);
        var activitySnapshot = Assert.Single(courseSnapshot.Activities);
        Assert.Equal(activityId, activitySnapshot.Id);
        Assert.Equal(90, activitySnapshot.DurationMinutes);
        Assert.Equal(materialId, activitySnapshot.Material!.Id);
        var assignmentSnapshot = Assert.Single(moduleSnapshot.Assignments);
        Assert.Equal(20, assignmentSnapshot.MaxPoints);
        Assert.Equal(12, assignmentSnapshot.PassScore);
        Assert.True(assignmentSnapshot.AllowShuffle);
        Assert.Equal(questionBankId, assignmentSnapshot.QuestionBankId);
        Assert.Equal(8, assignmentSnapshot.QuestionCount);
        Assert.True(assignmentSnapshot.ShuffleOptions);
        Assert.Equal(40, assignmentSnapshot.EasyPercent);
        Assert.Equal(40, assignmentSnapshot.MediumPercent);
        Assert.Equal(20, assignmentSnapshot.HardPercent);
        Assert.Null(typeof(CurriculumSnapshotBuilder.AssignmentSnapshot).GetProperty("AvailableFrom"));
        Assert.Null(typeof(CurriculumSnapshotBuilder.AssignmentSnapshot).GetProperty("DueAt"));
    }
}
