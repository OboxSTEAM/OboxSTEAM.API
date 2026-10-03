using Microsoft.Extensions.Logging;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.Services;

/// <summary>
/// FE-test fixture programs and curricula for the advisory chat.
/// Idempotent on <c>PRG-ADV-DRAFT-ADVICE</c>.
/// </summary>
public partial class SeedService
{
    internal const string SeedMakerAdvisoryFrameworkName = "Maker Advisory Family";
    internal const string SeedExpertAdvisorySeriesName = "Expert Advisory Demo";
    internal const string SeedAdvDraftAdviceCode = "PRG-ADV-DRAFT-ADVICE";
    internal const string SeedAdvDraftFixCode = "PRG-ADV-DRAFT-FIX";
    internal const string SeedAdvPendingCode = "PRG-ADV-PENDING";
    internal const string SeedAdvResubmitCode = "PRG-ADV-RESUBMIT";
    internal const string SeedAdvApprovedCode = "PRG-ADV-APPROVED";
    internal const string SeedAdvActiveCode = "PRG-ADV-ACTIVE";
    internal const string SeedAdvPinV1Code = "PRG-ADV-PIN-V1";
    internal const string SeedAdvShareACode = "PRG-ADV-SHARE-A";
    internal const string SeedAdvShareBCode = "PRG-ADV-SHARE-B";

    private static readonly decimal SeedAdvCatalogPrice = 1_200_000m;

    private async Task SeedExpertAdvisoryDemoAsync()
    {
        _loggerService.LogInformation("Starting seed expert advisory demo");

        var existingAnchor = await _unitOfWork.Programs.FirstOrDefaultAsync(
            p => p.Code == SeedAdvDraftAdviceCode && !p.IsDeleted);
        if (existingAnchor != null)
        {
            _loggerService.LogInformation(
                "Expert advisory demo already present ({Code}). Skipping.",
                SeedAdvDraftAdviceCode);
            return;
        }

        var expert001 = await _unitOfWork.Experts.FirstOrDefaultAsync(e => e.Code == "EXP-001" && !e.IsDeleted);
        var expert002 = await _unitOfWork.Experts.FirstOrDefaultAsync(e => e.Code == "EXP-002" && !e.IsDeleted);
        var manager = await _unitOfWork.Users.FirstOrDefaultAsync(u => u.Code == "MNG-001" && !u.IsDeleted);

        if (expert001 == null || expert002 == null || manager == null)
        {
            _loggerService.LogWarning(
                "EXP-001 / EXP-002 / MNG-001 missing. Skipping expert advisory demo seed.");
            return;
        }

        var (framework, publishedV1, _) = await EnsureMakerAdvisoryFrameworkAsync(expert001.Id);

        // A — Draft with a board contributor
        var progA = await CreateAdvProgramAsync(
            SeedAdvDraftAdviceCode,
            "ADV Draft Advice",
            "Scenario A: Draft with chat mentions, an open and an addressed pin; EXP-001 advisor, EXP-002 on board.",
            ProgramStatus.Draft,
            framework.Id,
            publishedV1.Id,
            expert001.Id);
        await EnsureAdvCurriculumAsync(progA, "DRAFT-ADVICE", includeAssignment: true);
        await EnsureExpertOnProgramBoardAsync(expert001, progA.Id, "Advisor");
        await EnsureExpertOnProgramBoardAsync(expert002, progA.Id, "Board Contributor");
        _loggerService.LogInformation("Seeded advisory scenario {Code}", SeedAdvDraftAdviceCode);

        // B — Draft, advisor only
        var progB = await CreateAdvProgramAsync(
            SeedAdvDraftFixCode,
            "ADV Draft Fix",
            "Scenario B: Draft; EXP-001 advisor only.",
            ProgramStatus.Draft,
            framework.Id,
            publishedV1.Id,
            expert001.Id);
        await EnsureAdvCurriculumAsync(progB, "DRAFT-FIX", includeAssignment: true);
        await EnsureExpertOnProgramBoardAsync(expert001, progB.Id, "Advisor");
        _loggerService.LogInformation("Seeded advisory scenario {Code}", SeedAdvDraftFixCode);

        // C — Draft with an approval request and no pins
        var progC = await CreateAdvProgramAsync(
            SeedAdvPendingCode,
            "ADV Ready For Approval",
            "Scenario C: Draft; framework check passes and approval is requested, so the advisor can approve.",
            ProgramStatus.Draft,
            framework.Id,
            publishedV1.Id,
            expert001.Id);
        await EnsureAdvCurriculumAsync(progC, "PENDING", includeAssignment: true);
        await EnsureExpertOnProgramBoardAsync(expert001, progC.Id, "Advisor");
        _loggerService.LogInformation("Seeded advisory scenario {Code}", SeedAdvPendingCode);

        // D — Approval revoked by curriculum edits (revision applied by the advisory chat seed)
        var progD = await CreateAdvProgramAsync(
            SeedAdvResubmitCode,
            "ADV Revised Curriculum",
            "Scenario D: Draft; approval revoked after curriculum edits (added activity, outcome and description edits, module reorder); EXP-002 on board.",
            ProgramStatus.Draft,
            framework.Id,
            publishedV1.Id,
            expert001.Id);
        var currD = await EnsureAdvCurriculumAsync(
            progD,
            "RESUBMIT",
            includeAssignment: true);
        await EnsureExpertOnProgramBoardAsync(expert001, progD.Id, "Advisor");
        await EnsureExpertOnProgramBoardAsync(expert002, progD.Id, "Board Contributor");

        await EnsureAdvMaterialAsync(currD.TheorySelfPaced, "Theory reading pack", SeedAdvTheoryReadingUrl);
        await EnsureAdvResearchFlowAsync(progD, currD.TheorySelfPaced, "RESUBMIT");
        _loggerService.LogInformation("Seeded advisory scenario {Code}", SeedAdvResubmitCode);

        // E — Approved
        var progE = await CreateAdvProgramAsync(
            SeedAdvApprovedCode,
            "ADV Approved",
            "Scenario E: Approved with an active approval at the current curriculum version; publishable.",
            ProgramStatus.Approved,
            framework.Id,
            publishedV1.Id,
            expert001.Id);
        await EnsureAdvCurriculumAsync(progE, "APPROVED", includeAssignment: true);
        await EnsureExpertOnProgramBoardAsync(expert001, progE.Id, "Advisor");
        _loggerService.LogInformation("Seeded advisory scenario {Code}", SeedAdvApprovedCode);

        // F — Active catalog
        var progF = await CreateAdvProgramAsync(
            SeedAdvActiveCode,
            "ADV Active Catalog",
            "Scenario F: Active catalog program.",
            ProgramStatus.Active,
            framework.Id,
            publishedV1.Id,
            expert001.Id);
        await EnsureAdvCurriculumAsync(progF, "ACTIVE", includeAssignment: true);
        await EnsureExpertOnProgramBoardAsync(expert001, progF.Id, "Advisor");
        _loggerService.LogInformation("Seeded advisory scenario {Code}", SeedAdvActiveCode);

        // G — Pin published v1 while draft v2 exists
        var progG = await CreateAdvProgramAsync(
            SeedAdvPinV1Code,
            "ADV Pin Published V1",
            "Scenario G: pins published framework v1 while draft v2 exists on the family.",
            ProgramStatus.Draft,
            framework.Id,
            publishedV1.Id,
            expert001.Id);
        await EnsureAdvCurriculumAsync(progG, "PIN-V1", includeAssignment: true);
        await EnsureExpertOnProgramBoardAsync(expert001, progG.Id, "Advisor");
        _loggerService.LogInformation("Seeded advisory scenario {Code}", SeedAdvPinV1Code);

        // H — Shared published version; different advisors
        var progHa = await CreateAdvProgramAsync(
            SeedAdvShareACode,
            "ADV Share A",
            "Scenario H: shares FrameworkVersionId with SHARE-B; EXP-001 advisor.",
            ProgramStatus.Draft,
            framework.Id,
            publishedV1.Id,
            expert001.Id);
        await EnsureAdvCurriculumAsync(progHa, "SHARE-A", includeAssignment: true);
        await EnsureExpertOnProgramBoardAsync(expert001, progHa.Id, "Advisor");
        await EnsureExpertOnProgramBoardAsync(expert002, progHa.Id, "Board Contributor");
        _loggerService.LogInformation("Seeded advisory scenario {Code}", SeedAdvShareACode);

        var progHb = await CreateAdvProgramAsync(
            SeedAdvShareBCode,
            "ADV Share B",
            "Scenario H: same FrameworkVersionId as SHARE-A; EXP-002 advisor; EXP-001 board contributor.",
            ProgramStatus.Draft,
            framework.Id,
            publishedV1.Id,
            expert002.Id);
        await EnsureAdvCurriculumAsync(progHb, "SHARE-B", includeAssignment: true);
        await EnsureExpertOnProgramBoardAsync(expert002, progHb.Id, "Advisor");
        await EnsureExpertOnProgramBoardAsync(expert001, progHb.Id, "Board Contributor");
        _loggerService.LogInformation("Seeded advisory scenario {Code}", SeedAdvShareBCode);

        await _unitOfWork.SaveChangesAsync();
        _loggerService.LogInformation("Finished seed expert advisory demo");
    }

    private async Task<(
        ProgramFramework Framework,
        ProgramFrameworkVersion PublishedV1,
        ProgramFrameworkVersion DraftV2)> EnsureMakerAdvisoryFrameworkAsync(Guid expertId)
    {
        var framework = await _unitOfWork.ProgramFrameworks.FirstOrDefaultAsync(
            f => f.ExpertId == expertId && f.Name == SeedMakerAdvisoryFrameworkName && !f.IsDeleted);
        if (framework == null)
        {
            framework = new ProgramFramework
            {
                Id = Guid.NewGuid(),
                ExpertId = expertId,
                Name = SeedMakerAdvisoryFrameworkName,
                Category = ProgramCategory.Technology,
                CreatedAt = _seedNow,
                CreatedBy = Guid.Empty,
                IsDeleted = false,
            };
            await _unitOfWork.ProgramFrameworks.AddAsync(framework);
        }

        var published = await _unitOfWork.ProgramFrameworkVersions.FirstOrDefaultAsync(
            v => v.FrameworkId == framework.Id && v.VersionNumber == 1 && !v.IsDeleted);
        if (published == null)
        {
            published = new ProgramFrameworkVersion
            {
                Id = Guid.NewGuid(),
                FrameworkId = framework.Id,
                VersionNumber = 1,
                Description = "Description for maker programs: offline lab plus theory SelfPaced progression.",
                AcademicGuidance =
                    "Prioritize safe facilitation in Offline labs and a clear theory→practice progression.",
                MinModules = 4,
                MinOfflineSessions = 1,
                RequireCapstoneResearchMilestone = true,
                IsPublished = true,
                PublishedAt = _seedNow,
                CreatedAt = _seedNow,
                CreatedBy = Guid.Empty,
                IsDeleted = false,
            };
            await _unitOfWork.ProgramFrameworkVersions.AddAsync(published);
        }
        else
        {
            published.AcademicGuidance ??=
                "Prioritize safe facilitation in Offline labs and a clear theory→practice progression.";
            published.Description ??=
                "Description for maker programs: offline lab plus theory SelfPaced progression.";
            published.MinOfflineSessions ??= 1;
            published.MinModules ??= 4;
            published.RequireCapstoneResearchMilestone ??= true;
            if (!published.IsPublished)
            {
                published.IsPublished = true;
                published.PublishedAt = _seedNow;
            }

            await _unitOfWork.ProgramFrameworkVersions.Update(published);
        }

        var draft = await _unitOfWork.ProgramFrameworkVersions.FirstOrDefaultAsync(
            v => v.FrameworkId == framework.Id && v.VersionNumber == 2 && !v.IsDeleted);
        if (draft == null)
        {
            draft = new ProgramFrameworkVersion
            {
                Id = Guid.NewGuid(),
                FrameworkId = framework.Id,
                VersionNumber = 2,
                Description =
                    "Description for maker programs (draft v2 refinements). Not auto-adopted by pinned programs.",
                AcademicGuidance = "Draft v2 — not auto-adopted",
                MinOfflineSessions = 1,
                IsPublished = false,
                PublishedAt = null,
                CreatedAt = _seedNow,
                CreatedBy = Guid.Empty,
                IsDeleted = false,
            };
            await _unitOfWork.ProgramFrameworkVersions.AddAsync(draft);
        }

        await _unitOfWork.SaveChangesAsync();
        return (framework, published, draft);
    }

    private async Task<Program> CreateAdvProgramAsync(
        string code,
        string name,
        string description,
        ProgramStatus status,
        Guid frameworkId,
        Guid frameworkVersionId,
        Guid advisorExpertId)
    {
        var program = new Program
        {
            Id = Guid.NewGuid(),
            Code = code,
            Name = name,
            SeriesName = SeedExpertAdvisorySeriesName,
            Description = description,
            Level = DifficultyLevel.Beginner,
            Category = ProgramCategory.Technology,
            EstimatedDuration = "6 weeks at 3 hours a week",
            ThumbnailUrl =
                "https://images.unsplash.com/photo-1581091226825-a6a2a5aee158?q=80&w=1170&auto=format&fit=crop",
            Status = status,
            Price = SeedAdvCatalogPrice,
            RetakeFee = CatalogRetakeFee(SeedAdvCatalogPrice),
            FrameworkId = frameworkId,
            FrameworkVersionId = frameworkVersionId,
            AdvisorExpertId = advisorExpertId,
            CreatedAt = _seedNow,
            CreatedBy = Guid.Empty,
            IsDeleted = false,
        };
        await _unitOfWork.Programs.AddAsync(program);
        await _unitOfWork.SaveChangesAsync();
        return program;
    }

    private async Task<AdvCurriculumBundle> EnsureAdvCurriculumAsync(
        Program program,
        string slug,
        bool includeAssignment)
    {
        var experiential = await EnsureAdvModuleAsync(
            program.Id,
            $"MOD-ADV-{slug}-01",
            "Maker Lab (Experiential)",
            ModuleType.Experiential,
            moduleOrder: 1,
            ["Set up a safe maker workspace", "Complete one Offline facilitation loop"]);
        var theory = await EnsureAdvModuleAsync(
            program.Id,
            $"MOD-ADV-{slug}-02",
            "Maker Theory",
            ModuleType.Theory,
            moduleOrder: 2,
            ["Explain progression from reading to lab", "Identify facilitation checkpoints"],
            prerequisiteModuleId: experiential.Id);

        var expCourse = await EnsureAdvCourseAsync(
            experiential.Id,
            $"CRS-ADV-{slug}-01",
            "Offline Lab Course",
            "Hands-on Offline activity to satisfy MinOfflineSessions.");
        var theoryCourse = await EnsureAdvCourseAsync(
            theory.Id,
            $"CRS-ADV-{slug}-02",
            "Theory Studio",
            "SelfPaced theory before or after the lab.");

        var offline = await EnsureAdvActivityAsync(
            expCourse.Id,
            $"ACT-ADV-{slug}-EX-OFF",
            "Offline maker lab",
            ActivityType.Offline,
            activityOrder: 1,
            "Facilitated Offline session (MinOfflineSessions).",
            durationMinutes: 90,
            requireQrCheckin: true,
            requireMediaEvidence: true);
        var theorySp = await EnsureAdvActivityAsync(
            theoryCourse.Id,
            $"ACT-ADV-{slug}-TH-SP",
            "Theory SelfPaced reading",
            ActivityType.SelfPaced,
            activityOrder: 1,
            "Self-paced reading for learning progression.",
            durationMinutes: null,
            requireQrCheckin: false);

        if (includeAssignment)
        {
            await EnsureAdvModuleAssignmentAsync(
                experiential.Id,
                $"ASG-ADV-{slug}-01",
                "Maker lab reflection");
        }

        return new AdvCurriculumBundle(experiential, theory, expCourse, theoryCourse, offline, theorySp);
    }

    private async Task EnsureAdvMaterialAsync(Activity activity, string title, string fileUrl)
    {
        var material = await _unitOfWork.Materials.FirstOrDefaultAsync(
            m => m.ActivityId == activity.Id && !m.IsDeleted);
        if (material != null)
        {
            material.Title = title;
            material.FileUrl = fileUrl;
            material.MaterialType = MaterialType.PDF;
            material.FileSizeBytes ??= 245_760;
            await _unitOfWork.Materials.Update(material);
            return;
        }

        await _unitOfWork.Materials.AddAsync(new Material
        {
            Id = Guid.NewGuid(),
            ActivityId = activity.Id,
            Title = title,
            MaterialType = MaterialType.PDF,
            FileUrl = fileUrl,
            FileSizeBytes = 245_760,
            CreatedAt = _seedNow,
            CreatedBy = Guid.Empty,
            IsDeleted = false,
        });
        await _unitOfWork.SaveChangesAsync();
    }

    private async Task<AdvResearchBundle> EnsureAdvResearchFlowAsync(
        Program program,
        Activity linkedActivity,
        string slug)
    {
        var module = await EnsureAdvModuleAsync(
            program.Id,
            $"MOD-ADV-{slug}-03",
            "Research Capstone",
            ModuleType.Research,
            moduleOrder: 3,
            ["Frame a safe maker question", "Present evidence from the revised learning path"],
            prerequisiteModuleId: null);

        var assignmentCode = $"ASG-ADV-{slug}-MS-CAP";
        var assignment = await _unitOfWork.Assignments.FirstOrDefaultAsync(
            a => a.Code == assignmentCode && !a.IsDeleted);
        if (assignment == null)
        {
            assignment = new Assignment
            {
                Id = Guid.NewGuid(),
                Code = assignmentCode,
                ModuleId = module.Id,
                CourseId = null,
                Title = "Maker evidence portfolio",
                Description = "Upload the evidence portfolio and reflection for the advisory pilot.",
                AssignmentType = AssignmentType.FileUpload,
                MaxPoints = 100,
                PassScore = 60,
                IsRequiredForModulePass = true,
                MaxAttempts = 3,
                TimeLimitMinutes = 60,
                CreatedAt = _seedNow,
                CreatedBy = Guid.Empty,
                IsDeleted = false,
            };
            await _unitOfWork.Assignments.AddAsync(assignment);
            await _unitOfWork.SaveChangesAsync();
        }

        var milestoneCode = $"RML-ADV-{slug}-CAP";
        var milestone = await _unitOfWork.ResearchMilestones.FirstOrDefaultAsync(
            m => m.Code == milestoneCode && !m.IsDeleted);
        if (milestone == null)
        {
            milestone = new ResearchMilestone
            {
                Id = Guid.NewGuid(),
                Code = milestoneCode,
                ModuleId = module.Id,
                Title = "Maker evidence capstone",
                Description = "Present the final evidence portfolio and reflect on facilitation choices.",
                MilestoneOrder = 1,
                IsCapstone = true,
                AssignmentId = assignment.Id,
                CreatedAt = _seedNow,
                CreatedBy = Guid.Empty,
                IsDeleted = false,
            };
            await _unitOfWork.ResearchMilestones.AddAsync(milestone);
            await _unitOfWork.SaveChangesAsync();
        }

        var link = await _unitOfWork.ResearchMilestoneActivities.FirstOrDefaultAsync(
            l => l.ResearchMilestoneId == milestone.Id
                 && l.ActivityId == linkedActivity.Id
                 && !l.IsDeleted);
        if (link == null)
        {
            await _unitOfWork.ResearchMilestoneActivities.AddAsync(new ResearchMilestoneActivity
            {
                Id = Guid.NewGuid(),
                ResearchMilestoneId = milestone.Id,
                ActivityId = linkedActivity.Id,
                IsRequiredForSubmission = true,
                DisplayOrder = 1,
                CreatedAt = _seedNow,
                CreatedBy = Guid.Empty,
                IsDeleted = false,
            });
            await _unitOfWork.SaveChangesAsync();
        }

        return new AdvResearchBundle(module, assignment, milestone);
    }

    private async Task<Module> EnsureAdvModuleAsync(
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

    private async Task<Course> EnsureAdvCourseAsync(
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

    private async Task<Activity> EnsureAdvActivityAsync(
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

    private async Task EnsureAdvModuleAssignmentAsync(Guid moduleId, string code, string title)
    {
        var existing = await _unitOfWork.Assignments.FirstOrDefaultAsync(a => a.Code == code && !a.IsDeleted);
        if (existing != null)
        {
            return;
        }

        await _unitOfWork.Assignments.AddAsync(new Assignment
        {
            Id = Guid.NewGuid(),
            Code = code,
            ModuleId = moduleId,
            CourseId = null,
            Title = title,
            Description = "Module-scoped reflection for advisory FE demos.",
            AssignmentType = AssignmentType.FileUpload,
            MaxPoints = 100,
            PassScore = 50,
            IsRequiredForModulePass = false,
            CreatedAt = _seedNow,
            CreatedBy = Guid.Empty,
            IsDeleted = false,
        });
        await _unitOfWork.SaveChangesAsync();
    }

    private sealed record AdvCurriculumBundle(
        Module ExperientialModule,
        Module TheoryModule,
        Course ExperientialCourse,
        Course TheoryCourse,
        Activity OfflineActivity,
        Activity TheorySelfPaced);

    private sealed record AdvResearchBundle(
        Module Module,
        Assignment Assignment,
        ResearchMilestone Milestone);
}
