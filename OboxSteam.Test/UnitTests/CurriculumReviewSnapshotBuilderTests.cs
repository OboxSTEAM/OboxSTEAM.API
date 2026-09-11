using OboxSteam.Application.Commons;
using OboxSteam.Application.DTOs.ProgramAdvisoryDTO;
using OboxSteam.Application.Services;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;

namespace OboxSteam.Test.UnitTests;

public sealed class CurriculumReviewSnapshotBuilderTests
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
            SkillsGained = "[\"Design\"]",
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

        var snapshot = CurriculumReviewSnapshotBuilder.BuildCurriculumSnapshot(tree);

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
        Assert.Null(typeof(CurriculumReviewSnapshotBuilder.AssignmentSnapshot).GetProperty("AvailableFrom"));
        Assert.Null(typeof(CurriculumReviewSnapshotBuilder.AssignmentSnapshot).GetProperty("DueAt"));
    }

    [Fact]
    public void ApplyBoardPresentationTruncation_TruncatesLongDescriptions()
    {
        var longText = new string('x', CurriculumReviewSnapshotBuilder.SnapshotDescriptionMaxLength + 50);
        var snapshot = new CurriculumReviewSnapshotBuilder.CurriculumSnapshotDocument
        {
            Program = new CurriculumReviewSnapshotBuilder.ProgramSnapshot
            {
                Id = Guid.NewGuid(),
                Name = "P",
                Code = "P1",
                Description = longText,
            },
            Modules =
            [
                new CurriculumReviewSnapshotBuilder.ModuleSnapshot
                {
                    Id = Guid.NewGuid(),
                    Code = "M1",
                    Name = "Module",
                    Type = "Theory",
                    ModuleType = "Theory",
                    Courses =
                    [
                        new CurriculumReviewSnapshotBuilder.CourseSnapshot
                        {
                            Id = Guid.NewGuid(),
                            Code = "C1",
                            Name = "Course",
                            Description = longText,
                        },
                    ],
                    Assignments =
                    [
                        new CurriculumReviewSnapshotBuilder.AssignmentSnapshot
                        {
                            Id = Guid.NewGuid(),
                            Code = "A1",
                            Title = "Quiz",
                            ModuleId = Guid.NewGuid(),
                            Scope = "Course",
                            Description = longText,
                        },
                    ],
                },
            ],
        };

        CurriculumReviewSnapshotBuilder.ApplyBoardPresentationTruncation(snapshot);

        Assert.True(snapshot.Program.DescriptionIsTruncated);
        Assert.Equal(CurriculumReviewSnapshotBuilder.SnapshotDescriptionMaxLength, snapshot.Program.Description!.Length);
        Assert.True(snapshot.Modules[0].Courses[0].DescriptionIsTruncated);
        Assert.True(snapshot.Modules[0].Assignments[0].DescriptionIsTruncated);
    }

    [Fact]
    public void Diff_EmitsFieldLevelChanges_AndTruncatesExcerpts()
    {
        var moduleId = Guid.NewGuid();
        var courseId = Guid.NewGuid();
        var assignmentId = Guid.NewGuid();
        var longBefore = new string('a', CurriculumReviewSnapshotBuilder.DiffExcerptMaxLength + 40);
        var longAfter = new string('b', CurriculumReviewSnapshotBuilder.DiffExcerptMaxLength + 40);

        var previous = new CurriculumReviewSnapshotBuilder.CurriculumSnapshotDocument
        {
            ProgramId = Guid.NewGuid(),
            Program = new CurriculumReviewSnapshotBuilder.ProgramSnapshot
            {
                Id = Guid.NewGuid(),
                Name = "P",
                Code = "P1",
            },
            Modules =
            [
                new CurriculumReviewSnapshotBuilder.ModuleSnapshot
                {
                    Id = moduleId,
                    Code = "M1",
                    Name = "Module A",
                    Order = 1,
                    Type = "Theory",
                    ModuleType = "Theory",
                    Courses =
                    [
                        new CurriculumReviewSnapshotBuilder.CourseSnapshot
                        {
                            Id = courseId,
                            Code = "C1",
                            Name = "Course",
                            Order = 1,
                            Description = "short",
                        },
                    ],
                    Assignments =
                    [
                        new CurriculumReviewSnapshotBuilder.AssignmentSnapshot
                        {
                            Id = assignmentId,
                            Code = "Q1",
                            Title = "Quiz",
                            ModuleId = moduleId,
                            CourseId = courseId,
                            Scope = "Course",
                            Description = longBefore,
                            AssignmentType = "Quiz",
                            MaxPoints = 10,
                            PassScore = 5,
                            MaxAttempts = 1,
                            AllowShuffle = false,
                            QuestionCount = 5,
                            EasyPercent = 50,
                            MediumPercent = 30,
                            HardPercent = 20,
                        },
                    ],
                },
            ],
        };

        var current = new CurriculumReviewSnapshotBuilder.CurriculumSnapshotDocument
        {
            ProgramId = previous.ProgramId,
            Program = previous.Program,
            Modules =
            [
                new CurriculumReviewSnapshotBuilder.ModuleSnapshot
                {
                    Id = moduleId,
                    Code = "M1",
                    Name = "Module B",
                    Order = 2,
                    Type = "Theory",
                    ModuleType = "Theory",
                    Courses =
                    [
                        new CurriculumReviewSnapshotBuilder.CourseSnapshot
                        {
                            Id = courseId,
                            Code = "C1",
                            Name = "Course",
                            Order = 1,
                            Description = "updated",
                        },
                    ],
                    Assignments =
                    [
                        new CurriculumReviewSnapshotBuilder.AssignmentSnapshot
                        {
                            Id = assignmentId,
                            Code = "Q1",
                            Title = "Quiz",
                            ModuleId = moduleId,
                            CourseId = courseId,
                            Scope = "Course",
                            Description = longAfter,
                            AssignmentType = "Quiz",
                            MaxPoints = 10,
                            PassScore = 5,
                            MaxAttempts = 1,
                            AllowShuffle = true,
                            QuestionCount = 8,
                            EasyPercent = 40,
                            MediumPercent = 40,
                            HardPercent = 20,
                        },
                    ],
                },
            ],
        };

        var previousJson = System.Text.Json.JsonSerializer.Serialize(
            previous,
            new System.Text.Json.JsonSerializerOptions { PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase });
        var currentJson = System.Text.Json.JsonSerializer.Serialize(
            current,
            new System.Text.Json.JsonSerializerOptions { PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase });

        var submissionId = Guid.NewGuid();
        var previousSubmissionId = Guid.NewGuid();
        var changes = CurriculumReviewSnapshotBuilder.Diff(
            submissionId,
            previousSubmissionId,
            previousJson,
            currentJson);

        Assert.Contains(changes.Reordered, r => r.Id == moduleId && r.Field == "order");
        Assert.Contains(changes.Modified, m => m.Id == moduleId && m.Field == "name");
        Assert.Contains(changes.Modified, m => m.Id == courseId && m.Field == "description" && m.Before == "short" && m.After == "updated");
        Assert.Contains(changes.Modified, m => m.Id == assignmentId && m.Field == "allowShuffle");
        Assert.Contains(changes.Modified, m => m.Id == assignmentId && m.Field == "questionCount");

        var descriptionChange = Assert.Single(changes.Modified, m => m.Id == assignmentId && m.Field == "description");
        Assert.Equal(CurriculumReviewSnapshotBuilder.DiffExcerptMaxLength, descriptionChange.Before!.Length);
        Assert.Equal(CurriculumReviewSnapshotBuilder.DiffExcerptMaxLength, descriptionChange.After!.Length);
        Assert.DoesNotContain(changes.Modified, m => m.Field is "availableFrom" or "dueAt" or "label");
    }

    [Fact]
    public void BuildStructuredChecks_FailedWithoutLinks_CanMapToProgramHighlight()
    {
        var programId = Guid.NewGuid();
        var snapshot = new CurriculumReviewSnapshotBuilder.CurriculumSnapshotDocument
        {
            ProgramId = programId,
            Program = new CurriculumReviewSnapshotBuilder.ProgramSnapshot
            {
                Id = programId,
                Name = "Empty",
                Code = "P-EMPTY",
            },
            Modules = [],
        };

        var version = new ProgramFrameworkVersion
        {
            Id = Guid.NewGuid(),
            FrameworkId = Guid.NewGuid(),
            VersionNumber = 1,
            IsPublished = true,
            MinModules = 1,
            MinOfflineSessions = 2,
            RequireCapstoneResearchMilestone = true,
        };

        var checks = CurriculumReviewService.BuildStructuredChecks(version, snapshot);
        Assert.All(checks, c => Assert.False(c.Passed));
        Assert.Contains(checks, c => c.Code == "MinOfflineSessions" && c.AffectedCurriculumLinks.Count == 0);
        Assert.Contains(checks, c => c.Code == "RequireCapstoneResearchMilestone" && c.AffectedCurriculumLinks.Count == 0);
        Assert.Contains(checks, c => c.Code == "MinModules" && c.AffectedCurriculumLinks.Count == 0);
    }
}
