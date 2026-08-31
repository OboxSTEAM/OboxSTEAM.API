using Microsoft.Extensions.Logging;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.Services;

public partial class SeedService
{
    internal const string SeedFrameworkRoboticsName = "Robotics cơ bản";
    internal const string SeedFrameworkCsharpName = "Lập trình C#";
    internal const string SeedFrameworkOpenName = "Open family (no rubric)";
    internal const string SeedFrameworkOtherExpertName = "STEAM curriculum (Minh)";
    internal const string SeedFrameworkEmptyProgramCode = "PRG-FW-EMPTY";
    internal const string SeedFrameworkPendingProgramCode = "PRG-FW-PENDING";
    internal const string SeedFrameworkEditableProgramCode = "PRG-FW-EDIT";

    private async Task SeedExpertCredentialsAsync()
    {
        var expert = await _unitOfWork.Experts.FirstOrDefaultAsync(e => e.Code == "EXP-001" && !e.IsDeleted);
        if (expert == null)
        {
            _loggerService.LogWarning("EXP-001 missing. Skipping expert credential seed.");
            return;
        }

        var hasDegree = await _unitOfWork.ExpertDegrees.FirstOrDefaultAsync(
            d => d.ExpertId == expert.Id && d.Title == "PhD in Robotics Education" && !d.IsDeleted);
        if (hasDegree == null)
        {
            await _unitOfWork.ExpertDegrees.AddAsync(new ExpertDegree
            {
                Id = Guid.NewGuid(),
                ExpertId = expert.Id,
                Title = "PhD in Robotics Education",
                Institution = "Hanoi University of Science and Technology",
                Year = 2016,
                CreatedAt = _seedNow,
                CreatedBy = Guid.Empty,
                IsDeleted = false,
            });
        }

        var hasPublication = await _unitOfWork.ExpertPublications.FirstOrDefaultAsync(
            p => p.ExpertId == expert.Id && p.Title == "Hands-on robotics for ages 6-8" && !p.IsDeleted);
        if (hasPublication == null)
        {
            await _unitOfWork.ExpertPublications.AddAsync(new ExpertPublication
            {
                Id = Guid.NewGuid(),
                ExpertId = expert.Id,
                Title = "Hands-on robotics for ages 6-8",
                Venue = "STEAM Education Review",
                Year = 2023,
                Url = "https://example.com/oboxsteam/robotics-6-8",
                CreatedAt = _seedNow,
                CreatedBy = Guid.Empty,
                IsDeleted = false,
            });
        }

        await _unitOfWork.SaveChangesAsync();
    }

    private async Task SeedProgramFrameworksAsync()
    {
        _loggerService.LogInformation("Starting seed program frameworks");

        var expert001 = await _unitOfWork.Experts.FirstOrDefaultAsync(e => e.Code == "EXP-001" && !e.IsDeleted);
        var expert002 = await _unitOfWork.Experts.FirstOrDefaultAsync(e => e.Code == "EXP-002" && !e.IsDeleted);
        if (expert001 == null)
        {
            _loggerService.LogWarning("EXP-001 missing. Skipping program framework seed.");
            return;
        }

        var robotics = await EnsureFrameworkAsync(
            expert001.Id,
            SeedFrameworkRoboticsName,
            "Hands-on family: at least one Offline lab. Rubric is used at expert review.",
            ProgramCategory.Technology,
            minOfflineSessions: 1,
            requireFinalAssessment: null,
            criteria:
            [
                ("Safety and workspace setup", "Students can set up and reset the kit safely.", 10, 1),
                ("Hands-on build quality", "Prototype matches the session goal.", 10, 2),
            ]);

        var csharp = await EnsureFrameworkAsync(
            expert001.Id,
            SeedFrameworkCsharpName,
            "Live coaching plus a capstone research milestone (IsCapstone).",
            ProgramCategory.Technology,
            minLiveSessions: 1,
            requireFinalAssessment: true,
            criteria:
            [
                ("Live coaching quality", "LiveOnline sessions cover the learning outcomes.", 10, 1),
                ("Capstone completeness", "Final research milestone is present and required.", 10, 2),
            ]);

        await EnsureFrameworkAsync(
            expert001.Id,
            SeedFrameworkOpenName,
            "No numeric rules and no rubric — programs on this frame may submit without waiting for expert review.",
            ProgramCategory.Technology,
            criteria: null);

        if (expert002 != null)
        {
            await EnsureFrameworkAsync(
                expert002.Id,
                SeedFrameworkOtherExpertName,
                "Owned by EXP-002 so Expert list isolation can be checked.",
                ProgramCategory.Technology,
                minModules: 1,
                criteria:
                [
                    ("Curriculum coverage", "Minimum module count is met.", 5, 1),
                ]);
        }

        await AttachFrameworkIfUnsetAsync("PRG-ROBOTICS", robotics.Id);
        await EnsureDraftProgramAsync(
            SeedFrameworkEditableProgramCode,
            "Robotics framework QA (editable)",
            robotics.Id,
            ProgramStatus.Draft);
        await EnsureDraftProgramAsync(
            SeedFrameworkEmptyProgramCode,
            "Framework pre-check empty draft",
            csharp.Id,
            ProgramStatus.Draft);
        await EnsureDraftProgramAsync(
            SeedFrameworkPendingProgramCode,
            "Framework pending-review fixture",
            robotics.Id,
            ProgramStatus.PendingReview);

        await _unitOfWork.SaveChangesAsync();
        _loggerService.LogInformation("Finished seed program frameworks");
    }

    private async Task<ProgramFramework> EnsureFrameworkAsync(
        Guid expertId,
        string name,
        string description,
        ProgramCategory category,
        int? minModules = null,
        int? minOfflineSessions = null,
        int? minLiveSessions = null,
        bool? requireFinalAssessment = null,
        (string Name, string Description, int MaxScore, int DisplayOrder)[]? criteria = null)
    {
        var existing = await _unitOfWork.ProgramFrameworks.FirstOrDefaultAsync(
            f => f.ExpertId == expertId && f.Name == name && !f.IsDeleted);
        if (existing != null)
        {
            await EnsureFrameworkCriteriaAsync(existing.Id, criteria);
            return existing;
        }

        var framework = new ProgramFramework
        {
            Id = Guid.NewGuid(),
            ExpertId = expertId,
            Name = name,
            Description = description,
            Category = category,
            MinModules = minModules,
            MinOfflineSessions = minOfflineSessions,
            MinLiveSessions = minLiveSessions,
            RequireFinalAssessment = requireFinalAssessment,
            CreatedAt = _seedNow,
            CreatedBy = Guid.Empty,
            IsDeleted = false,
        };
        await _unitOfWork.ProgramFrameworks.AddAsync(framework);
        await EnsureFrameworkCriteriaAsync(framework.Id, criteria);
        return framework;
    }

    private async Task EnsureFrameworkCriteriaAsync(
        Guid frameworkId,
        (string Name, string Description, int MaxScore, int DisplayOrder)[]? criteria)
    {
        if (criteria == null)
        {
            return;
        }

        foreach (var item in criteria)
        {
            var existing = await _unitOfWork.FrameworkRubricCriteria.FirstOrDefaultAsync(
                c => c.FrameworkId == frameworkId && c.Name == item.Name && !c.IsDeleted);
            if (existing != null)
            {
                continue;
            }

            await _unitOfWork.FrameworkRubricCriteria.AddAsync(new FrameworkRubricCriterion
            {
                Id = Guid.NewGuid(),
                FrameworkId = frameworkId,
                Name = item.Name,
                Description = item.Description,
                MaxScore = item.MaxScore,
                DisplayOrder = item.DisplayOrder,
                CreatedAt = _seedNow,
                CreatedBy = Guid.Empty,
                IsDeleted = false,
            });
        }
    }

    private async Task AttachFrameworkIfUnsetAsync(string programCode, Guid frameworkId)
    {
        var program = await _unitOfWork.Programs.FirstOrDefaultAsync(p => p.Code == programCode && !p.IsDeleted);
        if (program == null || program.FrameworkId.HasValue)
        {
            return;
        }

        program.FrameworkId = frameworkId;
        await _unitOfWork.Programs.Update(program);
    }

    private async Task EnsureDraftProgramAsync(
        string code,
        string name,
        Guid frameworkId,
        ProgramStatus status)
    {
        var existing = await _unitOfWork.Programs.FirstOrDefaultAsync(p => p.Code == code && !p.IsDeleted);
        if (existing != null)
        {
            if (existing.FrameworkId != frameworkId || existing.Status != status)
            {
                existing.FrameworkId = frameworkId;
                existing.Status = status;
                await _unitOfWork.Programs.Update(existing);
            }

            return;
        }

        await _unitOfWork.Programs.AddAsync(new Program
        {
            Id = Guid.NewGuid(),
            Code = code,
            Name = name,
            SeriesName = "Framework QA",
            Description = "Seed fixture for program-framework PUT tests. No classes — not locked by CurriculumEditGuard.",
            Level = DifficultyLevel.Beginner,
            Category = ProgramCategory.Technology,
            EstimatedDuration = "n/a",
            SkillsGained = "QA",
            Status = status,
            Price = 0m,
            FrameworkId = frameworkId,
            CreatedAt = _seedNow,
            CreatedBy = Guid.Empty,
            IsDeleted = false,
        });
    }
}
