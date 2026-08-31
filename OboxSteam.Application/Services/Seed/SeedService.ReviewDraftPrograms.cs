using Microsoft.Extensions.Logging;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.Services;

/// <summary>
/// Two complete Draft programs with no framework. Manager attaches a blueprint via PUT
/// then POST submit-review to reach PendingReview. Curriculum meets every seeded
/// framework pre-check (3 modules, Offline, LiveOnline, capstone).
/// Re-seed does not reset Status or FrameworkId after the manager mutates them.
/// </summary>
public partial class SeedService
{
    internal const string SeedReviewDraftIotProgramCode = "PRG-REV-IOT";
    internal const string SeedReviewDraftCodeProgramCode = "PRG-REV-CODE";

    private async Task SeedReviewDraftProgramsAsync()
    {
        _loggerService.LogInformation("Starting seed review-ready draft programs");

        foreach (var definition in GetReviewDraftProgramDefinitions())
        {
            await SeedOneReviewDraftProgramAsync(definition);
        }

        _loggerService.LogInformation("Finished seed review-ready draft programs");
    }

    private static IReadOnlyList<ReviewDraftProgramDefinition> GetReviewDraftProgramDefinitions() =>
    [
        new(
            ProgramCode: SeedReviewDraftIotProgramCode,
            Slug: "IOT",
            Name: "IoT Home Lab Track",
            SeriesName: "Curriculum Review Drafts",
            Description:
                "Draft STEAM track for testing expert review. No framework is attached at seed. "
                + "Attach Robotics cơ bản, Lập trình C#, or Open family, then submit-review.",
            Level: DifficultyLevel.Beginner,
            Category: ProgramCategory.Technology,
            EstimatedDuration: "8 weeks at 3 hours a week",
            SkillsGained: "Sensors, data logging, circuit safety, prototype iteration",
            Price: 1_500_000m,
            ThumbnailUrl:
                "https://images.unsplash.com/photo-1518770660439-4636190af475?q=80&w=1170&auto=format&fit=crop",
            TheoryModuleName: "Sensing the World",
            ExperientialModuleName: "Build a Home Sensor Kit",
            ResearchModuleName: "IoT Investigation Capstone",
            TheoryCourseName: "Sensor Theory Studio",
            ExperientialCourseName: "Kit Assembly Lab",
            ResearchCourseName: "Field Study Studio",
            TheoryOutcomes: ["Name common household sensors", "Read analog vs digital signals"],
            ExperientialOutcomes: ["Assemble a safe sensor circuit", "Log a 24-hour data sample"],
            ResearchOutcomes: ["Frame an IoT research question", "Present findings from logged data"]),
        new(
            ProgramCode: SeedReviewDraftCodeProgramCode,
            Slug: "CODE",
            Name: "Creative Coding Studio",
            SeriesName: "Curriculum Review Drafts",
            Description:
                "Draft coding track for testing expert review. No framework is attached at seed. "
                + "Attach a framework via program update, then submit-review to enter the expert queue.",
            Level: DifficultyLevel.Intermediate,
            Category: ProgramCategory.Technology,
            EstimatedDuration: "10 weeks at 4 hours a week",
            SkillsGained: "Block coding, live debugging, computational thinking, project storytelling",
            Price: 1_800_000m,
            ThumbnailUrl:
                "https://images.unsplash.com/photo-1515879218367-8466d910aaa4?q=80&w=1169&auto=format&fit=crop",
            TheoryModuleName: "How Programs Think",
            ExperientialModuleName: "Live Debug Workshop",
            ResearchModuleName: "Interactive Story Capstone",
            TheoryCourseName: "Logic and Sequences",
            ExperientialCourseName: "Pair Debugging Lab",
            ResearchCourseName: "Story Project Studio",
            TheoryOutcomes: ["Trace a simple program", "Explain loops and conditions"],
            ExperientialOutcomes: ["Debug a broken script live", "Document a fix with the mentor"],
            ResearchOutcomes: ["Ship an interactive story", "Reflect on design trade-offs"]),
    ];

    private async Task SeedOneReviewDraftProgramAsync(ReviewDraftProgramDefinition definition)
    {
        var program = await EnsureReviewDraftProgramAsync(definition);

        var theory = await EnsureReviewDraftModuleAsync(
            program.Id,
            $"MOD-REV-{definition.Slug}-01",
            definition.TheoryModuleName,
            ModuleType.Theory,
            moduleOrder: 1,
            definition.TheoryOutcomes);
        var experiential = await EnsureReviewDraftModuleAsync(
            program.Id,
            $"MOD-REV-{definition.Slug}-02",
            definition.ExperientialModuleName,
            ModuleType.Experiential,
            moduleOrder: 2,
            definition.ExperientialOutcomes,
            prerequisiteModuleId: theory.Id);
        var research = await EnsureReviewDraftModuleAsync(
            program.Id,
            $"MOD-REV-{definition.Slug}-03",
            definition.ResearchModuleName,
            ModuleType.Research,
            moduleOrder: 3,
            definition.ResearchOutcomes,
            prerequisiteModuleId: experiential.Id);

        var theoryCourse = await EnsureReviewDraftCourseAsync(
            theory.Id,
            $"CRS-REV-{definition.Slug}-01",
            definition.TheoryCourseName,
            "Theory course: SelfPaced reading plus a LiveOnline walkthrough.");
        var experientialCourse = await EnsureReviewDraftCourseAsync(
            experiential.Id,
            $"CRS-REV-{definition.Slug}-02",
            definition.ExperientialCourseName,
            "Hands-on course: prep plus an Offline lab (Robotics MinOfflineSessions).");
        var researchCourse = await EnsureReviewDraftCourseAsync(
            research.Id,
            $"CRS-REV-{definition.Slug}-03",
            definition.ResearchCourseName,
            "Research course feeding the capstone milestone (C# RequireFinalAssessment).");

        await EnsureReviewDraftActivityAsync(
            theoryCourse.Id,
            $"ACT-REV-{definition.Slug}-TH-SP",
            $"{definition.TheoryCourseName} Reading",
            ActivityType.SelfPaced,
            activityOrder: 1,
            "Self-paced intro reading.",
            durationMinutes: null,
            requireQrCheckin: false);
        await EnsureReviewDraftActivityAsync(
            theoryCourse.Id,
            $"ACT-REV-{definition.Slug}-TH-LIVE",
            $"{definition.TheoryCourseName} Live Session",
            ActivityType.LiveOnline,
            activityOrder: 2,
            "LiveOnline session so C# MinLiveSessions pre-check passes.",
            durationMinutes: 90,
            requireQrCheckin: false);

        await EnsureReviewDraftActivityAsync(
            experientialCourse.Id,
            $"ACT-REV-{definition.Slug}-EX-SP",
            $"{definition.ExperientialCourseName} Prep",
            ActivityType.SelfPaced,
            activityOrder: 1,
            "Self-paced prep before the on-site lab.",
            durationMinutes: null,
            requireQrCheckin: false);
        var offlineLab = await EnsureReviewDraftActivityAsync(
            experientialCourse.Id,
            $"ACT-REV-{definition.Slug}-EX-OFF",
            $"{definition.ExperientialCourseName} Offline Lab",
            ActivityType.Offline,
            activityOrder: 2,
            "Offline lab so Robotics MinOfflineSessions pre-check passes.",
            durationMinutes: 120,
            requireQrCheckin: true,
            requireMediaEvidence: true);

        var researchBrief = await EnsureReviewDraftActivityAsync(
            researchCourse.Id,
            $"ACT-REV-{definition.Slug}-RS-SP",
            $"{definition.ResearchCourseName} Brief",
            ActivityType.SelfPaced,
            activityOrder: 1,
            "Self-paced research brief before the capstone upload.",
            durationMinutes: null,
            requireQrCheckin: false);

        await EnsureReviewDraftCapstoneAsync(
            research.Id,
            researchBrief.Id,
            offlineLab.Id,
            definition.Slug,
            definition.ResearchModuleName);
    }

    private async Task<Program> EnsureReviewDraftProgramAsync(ReviewDraftProgramDefinition definition)
    {
        var existing = await _unitOfWork.Programs.FirstOrDefaultAsync(
            p => p.Code == definition.ProgramCode && !p.IsDeleted);
        if (existing != null)
        {
            return existing;
        }

        var program = new Program
        {
            Id = Guid.NewGuid(),
            Code = definition.ProgramCode,
            Name = definition.Name,
            SeriesName = definition.SeriesName,
            Description = definition.Description,
            Level = definition.Level,
            Category = definition.Category,
            EstimatedDuration = definition.EstimatedDuration,
            SkillsGained = definition.SkillsGained,
            ThumbnailUrl = definition.ThumbnailUrl,
            Status = ProgramStatus.Draft,
            Price = definition.Price,
            RetakeFee = CatalogRetakeFee(definition.Price),
            FrameworkId = null,
            CreatedAt = _seedNow,
            CreatedBy = Guid.Empty,
            IsDeleted = false,
        };

        await _unitOfWork.Programs.AddAsync(program);
        await _unitOfWork.SaveChangesAsync();
        _loggerService.LogInformation(
            "Seeded review-ready draft program {Code} with no framework.",
            program.Code);
        return program;
    }

    private async Task<Module> EnsureReviewDraftModuleAsync(
        Guid programId,
        string code,
        string name,
        ModuleType moduleType,
        int moduleOrder,
        string[] learningOutcomes,
        Guid? prerequisiteModuleId = null)
    {
        var existing = await _unitOfWork.Modules.FirstOrDefaultAsync(m => m.Code == code && !m.IsDeleted);
        if (existing != null)
        {
            if (existing.LearningOutcomes.Length == 0 && learningOutcomes.Length > 0)
            {
                existing.LearningOutcomes = learningOutcomes;
                await _unitOfWork.Modules.Update(existing);
                await _unitOfWork.SaveChangesAsync();
            }

            return existing;
        }

        var module = new Module
        {
            Id = Guid.NewGuid(),
            Code = code,
            ProgramId = programId,
            Name = name,
            ModuleType = moduleType,
            ModuleOrder = moduleOrder,
            PrerequisiteModuleId = prerequisiteModuleId,
            IsMandatory = true,
            LearningOutcomes = learningOutcomes,
            CreatedAt = _seedNow,
            CreatedBy = Guid.Empty,
            IsDeleted = false,
        };

        await _unitOfWork.Modules.AddAsync(module);
        await _unitOfWork.SaveChangesAsync();
        return module;
    }

    private async Task<Course> EnsureReviewDraftCourseAsync(
        Guid moduleId,
        string code,
        string name,
        string description)
    {
        var existing = await _unitOfWork.Courses.FirstOrDefaultAsync(c => c.Code == code && !c.IsDeleted);
        if (existing != null)
        {
            return existing;
        }

        var course = new Course
        {
            Id = Guid.NewGuid(),
            Code = code,
            ModuleId = moduleId,
            Name = name,
            Description = description,
            CourseOrder = 1,
            CreatedAt = _seedNow,
            CreatedBy = Guid.Empty,
            IsDeleted = false,
        };

        await _unitOfWork.Courses.AddAsync(course);
        await _unitOfWork.SaveChangesAsync();
        return course;
    }

    private async Task<Activity> EnsureReviewDraftActivityAsync(
        Guid courseId,
        string code,
        string name,
        ActivityType activityType,
        int activityOrder,
        string description,
        int? durationMinutes,
        bool requireQrCheckin,
        bool requireMediaEvidence = false)
    {
        var existing = await _unitOfWork.Activities.FirstOrDefaultAsync(a => a.Code == code && !a.IsDeleted);
        if (existing != null)
        {
            return existing;
        }

        var activity = new Activity
        {
            Id = Guid.NewGuid(),
            Code = code,
            CourseId = courseId,
            Name = name,
            ActivityType = activityType,
            Description = description,
            ActivityOrder = activityOrder,
            DurationMinutes = durationMinutes,
            RequireQrCheckin = requireQrCheckin,
            RequireMediaEvidence = requireMediaEvidence,
            CreatedAt = _seedNow,
            CreatedBy = Guid.Empty,
            IsDeleted = false,
        };

        await _unitOfWork.Activities.AddAsync(activity);
        await _unitOfWork.SaveChangesAsync();
        return activity;
    }

    private async Task EnsureReviewDraftCapstoneAsync(
        Guid researchModuleId,
        Guid researchBriefActivityId,
        Guid offlineLabActivityId,
        string slug,
        string researchModuleName)
    {
        var milestoneCode = $"RML-REV-{slug}-CAP";
        var existing = await _unitOfWork.ResearchMilestones.FirstOrDefaultAsync(
            m => m.Code == milestoneCode && !m.IsDeleted);
        if (existing != null)
        {
            return;
        }

        var assignment = new Assignment
        {
            Id = Guid.NewGuid(),
            Code = $"ASG-REV-{slug}-CAP",
            ModuleId = researchModuleId,
            Title = $"{researchModuleName} deliverable",
            Description = "Capstone file upload so C# RequireFinalAssessment pre-check passes.",
            AssignmentType = AssignmentType.FileUpload,
            MaxPoints = 100,
            PassScore = 60m,
            IsRequiredForModulePass = true,
            MaxAttempts = 3,
            TimeLimitMinutes = 60,
            CreatedAt = _seedNow,
            CreatedBy = Guid.Empty,
            IsDeleted = false,
        };

        var milestone = new ResearchMilestone
        {
            Id = Guid.NewGuid(),
            Code = milestoneCode,
            ModuleId = researchModuleId,
            Title = $"{researchModuleName} capstone",
            Description = "Final assessment milestone required by the C# framework blueprint.",
            MilestoneOrder = 1,
            IsCapstone = true,
            AssignmentId = assignment.Id,
            CreatedAt = _seedNow,
            CreatedBy = Guid.Empty,
            IsDeleted = false,
        };

        await _unitOfWork.Assignments.AddAsync(assignment);
        await _unitOfWork.ResearchMilestones.AddAsync(milestone);
        await _unitOfWork.SaveChangesAsync();

        await _unitOfWork.ResearchMilestoneActivities.AddRangeAsync(
        [
            new ResearchMilestoneActivity
            {
                Id = Guid.NewGuid(),
                ResearchMilestoneId = milestone.Id,
                ActivityId = researchBriefActivityId,
                IsRequiredForSubmission = true,
                DisplayOrder = 1,
                CreatedAt = _seedNow,
                CreatedBy = Guid.Empty,
                IsDeleted = false,
            },
            new ResearchMilestoneActivity
            {
                Id = Guid.NewGuid(),
                ResearchMilestoneId = milestone.Id,
                ActivityId = offlineLabActivityId,
                IsRequiredForSubmission = true,
                DisplayOrder = 2,
                CreatedAt = _seedNow,
                CreatedBy = Guid.Empty,
                IsDeleted = false,
            },
        ]);
        await _unitOfWork.SaveChangesAsync();
    }

    private sealed record ReviewDraftProgramDefinition(
        string ProgramCode,
        string Slug,
        string Name,
        string SeriesName,
        string Description,
        DifficultyLevel Level,
        ProgramCategory Category,
        string EstimatedDuration,
        string SkillsGained,
        decimal Price,
        string ThumbnailUrl,
        string TheoryModuleName,
        string ExperientialModuleName,
        string ResearchModuleName,
        string TheoryCourseName,
        string ExperientialCourseName,
        string ResearchCourseName,
        string[] TheoryOutcomes,
        string[] ExperientialOutcomes,
        string[] ResearchOutcomes);
}
