using System.Globalization;
using OboxSteam.Application.Commons;
using OboxSteam.Application.DTOs.ProgramAdvisoryDTO;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;
using OboxSteam.Domain.Interfaces;

namespace OboxSteam.Application.Validation;

/// <summary>
/// Evaluates a framework version's rules against a live curriculum tree. A check is
/// emitted only for rules that are on; <see cref="FrameworkCheckItemDto.AffectedCurriculumLinks"/>
/// lists the failing components (empty = program-level).
/// </summary>
public static class FrameworkRuleEvaluator
{
    public static async Task<List<FrameworkCheckItemDto>> EvaluateAsync(
        IUnitOfWork unitOfWork,
        ProgramFrameworkVersion version,
        ProgramCurriculumTreeSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(version);
        ArgumentNullException.ThrowIfNull(snapshot);

        ProgramCategory? frameworkCategory = null;
        if (version.RequireCategoryMatch)
        {
            frameworkCategory = (await unitOfWork.ProgramFrameworks.GetByIdAsync(version.FrameworkId))?.Category;
        }

        var skillCount = 0;
        if (version.MinSkillsGained.HasValue)
        {
            var programId = snapshot.Program.Id;
            skillCount = (await unitOfWork.ProgramSkills.GetAllAsync(s => s.ProgramId == programId && !s.IsDeleted)).Count;
        }

        return Evaluate(version, frameworkCategory, snapshot, skillCount);
    }

    public static List<FrameworkCheckItemDto> Evaluate(
        ProgramFrameworkVersion version,
        ProgramCategory? frameworkCategory,
        ProgramCurriculumTreeSnapshot snapshot,
        int programSkillCount)
    {
        ArgumentNullException.ThrowIfNull(version);
        ArgumentNullException.ThrowIfNull(snapshot);

        var checks = new List<FrameworkCheckItemDto>();
        var modules = snapshot.Modules;
        var orderedIds = snapshot.GlobalActivityOrder
            .Concat(snapshot.ActivitiesById.Keys)
            .Distinct()
            .Where(snapshot.ActivitiesById.ContainsKey);
        var activities = orderedIds.Select(id => snapshot.ActivitiesById[id]).ToList();

        if (version.MinModules.HasValue)
        {
            checks.Add(Check(
                "MinModules",
                "Minimum modules",
                version.MinModules.Value.ToString(CultureInfo.InvariantCulture),
                modules.Count.ToString(CultureInfo.InvariantCulture),
                modules.Count >= version.MinModules.Value,
                modules.Select(ModuleLink)));
        }

        if (version.MaxModules.HasValue)
        {
            var passed = modules.Count <= version.MaxModules.Value;
            checks.Add(Check(
                "MaxModules",
                "Maximum modules",
                $"<= {version.MaxModules.Value}",
                modules.Count.ToString(CultureInfo.InvariantCulture),
                passed,
                passed ? [] : modules.Skip(version.MaxModules.Value).Select(ModuleLink)));
        }

        if (version.MinCoursesPerModule.HasValue || version.MaxCoursesPerModule.HasValue)
        {
            var courseModules = modules.Where(m => m.ModuleType != ModuleType.Research).ToList();
            var counts = courseModules
                .Select(m => (Module: m, Count: snapshot.CoursesByModuleId.TryGetValue(m.Id, out var c) ? c.Count : 0))
                .ToList();
            var failing = counts
                .Where(x => x.Count < (version.MinCoursesPerModule ?? int.MinValue)
                            || x.Count > (version.MaxCoursesPerModule ?? int.MaxValue))
                .Select(x => x.Module)
                .ToList();
            var actual = counts.Count == 0
                ? "No course modules"
                : counts.Min(x => x.Count) == counts.Max(x => x.Count)
                    ? $"{counts[0].Count} per module"
                    : $"{counts.Min(x => x.Count)}-{counts.Max(x => x.Count)} per module";
            checks.Add(Check(
                "CoursesPerModule",
                "Courses per module",
                Range(version.MinCoursesPerModule, version.MaxCoursesPerModule, " per module"),
                actual,
                failing.Count == 0,
                failing.Select(ModuleLink)));
        }

        if (version.MinTotalHours.HasValue || version.MaxTotalHours.HasValue)
        {
            var totalMinutes = activities.Sum(a => Math.Max(a.DurationMinutes ?? 0, 0));
            var passed = (!version.MinTotalHours.HasValue || totalMinutes >= version.MinTotalHours.Value * 60)
                         && (!version.MaxTotalHours.HasValue || totalMinutes <= version.MaxTotalHours.Value * 60);
            checks.Add(Check(
                "TotalHours",
                "Total hours",
                Range(version.MinTotalHours, version.MaxTotalHours, " h"),
                $"{(totalMinutes / 60m).ToString("0.#", CultureInfo.InvariantCulture)} h",
                passed,
                []));
        }

        if (version.MaxActivityMinutes.HasValue)
        {
            var over = activities.Where(a => a.DurationMinutes > version.MaxActivityMinutes.Value).ToList();
            var longest = activities.Count == 0 ? 0 : activities.Max(a => a.DurationMinutes ?? 0);
            checks.Add(Check(
                "MaxActivityDuration",
                "Maximum activity duration",
                $"<= {version.MaxActivityMinutes.Value} min",
                $"{longest} min",
                over.Count == 0,
                over.Select(ActivityLink)));
        }

        if (version.RequireActivityDuration)
        {
            var missing = activities
                .Where(a => a.ActivityType != ActivityType.SelfPaced && a.DurationMinutes is null or <= 0)
                .ToList();
            checks.Add(Check(
                "ActivityDurationSet",
                "Activity duration set",
                "All scheduled activities",
                missing.Count == 0 ? "All scheduled activities" : $"{missing.Count} missing",
                missing.Count == 0,
                missing.Select(ActivityLink)));
        }

        var offline = activities.Where(a => a.ActivityType == ActivityType.Offline).ToList();
        var live = activities.Where(a => a.ActivityType == ActivityType.LiveOnline).ToList();

        if (version.MinOfflineSessions.HasValue)
        {
            checks.Add(Check(
                "MinOfflineSessions",
                "Minimum Offline sessions",
                version.MinOfflineSessions.Value.ToString(CultureInfo.InvariantCulture),
                offline.Count.ToString(CultureInfo.InvariantCulture),
                offline.Count >= version.MinOfflineSessions.Value,
                offline.Select(ActivityLink)));
        }

        if (version.MinLiveSessions.HasValue)
        {
            checks.Add(Check(
                "MinLiveSessions",
                "Minimum LiveOnline sessions",
                version.MinLiveSessions.Value.ToString(CultureInfo.InvariantCulture),
                live.Count.ToString(CultureInfo.InvariantCulture),
                live.Count >= version.MinLiveSessions.Value,
                live.Select(ActivityLink)));
        }

        if (version.MinOfflineRatioPercent.HasValue)
        {
            checks.Add(RatioCheck("OfflineRatio", "Offline activity share", version.MinOfflineRatioPercent.Value, offline.Count, activities.Count));
        }

        if (version.MinLiveRatioPercent.HasValue)
        {
            checks.Add(RatioCheck("LiveRatio", "LiveOnline activity share", version.MinLiveRatioPercent.Value, live.Count, activities.Count));
        }

        if (version.RequireAssignmentPerModule)
        {
            var modulesWithAssignment = snapshot.AssignmentsById.Values.Select(a => a.ModuleId).ToHashSet();
            var missing = modules.Where(m => !modulesWithAssignment.Contains(m.Id)).ToList();
            checks.Add(Check(
                "AssignmentPerModule",
                "Assignment in every module",
                "At least 1 per module",
                missing.Count == 0 ? "All modules" : $"{missing.Count} module(s) without an assignment",
                missing.Count == 0,
                missing.Select(ModuleLink)));
        }

        if (version.RequireAssignmentPassScore)
        {
            var invalid = snapshot.AssignmentsById.Values
                .Where(a => a.PassScore <= 0 || a.PassScore > a.MaxPoints)
                .OrderBy(a => a.Code)
                .ToList();
            checks.Add(Check(
                "AssignmentPassScore",
                "Assignment pass score",
                "Set and <= max points",
                invalid.Count == 0 ? "All assignments" : $"{invalid.Count} invalid",
                invalid.Count == 0,
                invalid.Select(a => Link(ProgramAdvisoryTargetType.Assignment, a.Id, a.Title))));
        }

        if (version.MinMaterialsPerActivity.HasValue)
        {
            var failing = activities
                .Where(a => a.ActivityType == ActivityType.SelfPaced
                            && (snapshot.MaterialsByActivityId.ContainsKey(a.Id) ? 1 : 0) < version.MinMaterialsPerActivity.Value)
                .ToList();
            checks.Add(Check(
                "MaterialsPerActivity",
                "Materials per SelfPaced activity",
                $">= {version.MinMaterialsPerActivity.Value}",
                failing.Count == 0 ? "All SelfPaced activities" : $"{failing.Count} below minimum",
                failing.Count == 0,
                failing.Select(ActivityLink)));
        }

        if (version.RequireCategoryMatch && frameworkCategory.HasValue)
        {
            checks.Add(Check(
                "CategoryMatch",
                "Category matches framework",
                frameworkCategory.Value.ToString(),
                snapshot.Program.Category.ToString(),
                snapshot.Program.Category == frameworkCategory.Value,
                []));
        }

        if (version.MinDescriptionLength.HasValue)
        {
            var length = snapshot.Program.Description?.Trim().Length ?? 0;
            checks.Add(Check(
                "DescriptionLength",
                "Description length",
                $">= {version.MinDescriptionLength.Value} characters",
                $"{length} characters",
                length >= version.MinDescriptionLength.Value,
                []));
        }

        if (version.MinSkillsGained.HasValue)
        {
            checks.Add(Check(
                "SkillsGained",
                "Skills gained",
                $">= {version.MinSkillsGained.Value}",
                programSkillCount.ToString(CultureInfo.InvariantCulture),
                programSkillCount >= version.MinSkillsGained.Value,
                []));
        }

        if (version.RequireThumbnail)
        {
            var hasThumbnail = !string.IsNullOrWhiteSpace(snapshot.Program.ThumbnailUrl);
            checks.Add(Check(
                "ThumbnailSet",
                "Thumbnail set",
                "Set",
                hasThumbnail ? "Set" : "Missing",
                hasThumbnail,
                []));
        }

        if (version.RequireCapstoneResearchMilestone == true)
        {
            var capstones = snapshot.MilestonesByModuleId.Values
                .SelectMany(m => m)
                .Where(m => m.IsCapstone && !m.IsDeleted)
                .ToList();
            checks.Add(Check(
                "RequireCapstoneResearchMilestone",
                "Capstone research milestone",
                "At least 1",
                capstones.Count.ToString(CultureInfo.InvariantCulture),
                capstones.Count >= 1,
                capstones.Select(m => Link(ProgramAdvisoryTargetType.ResearchMilestone, m.Id, m.Title))));
        }

        return checks;
    }

    private static FrameworkCheckItemDto RatioCheck(string code, string label, int minPercent, int count, int total)
    {
        var ratio = total == 0 ? 0m : count * 100m / total;
        return Check(
            code,
            label,
            $">= {minPercent}%",
            $"{ratio.ToString("0.#", CultureInfo.InvariantCulture)}%",
            ratio >= minPercent,
            []);
    }

    private static string Range(int? min, int? max, string unit) => (min, max) switch
    {
        ({ } lo, { } hi) => $"{lo}-{hi}{unit}",
        ({ } lo, null) => $">= {lo}{unit}",
        (null, { } hi) => $"<= {hi}{unit}",
        _ => string.Empty,
    };

    private static FrameworkCheckItemDto Check(
        string code,
        string label,
        string expected,
        string actual,
        bool passed,
        IEnumerable<AffectedCurriculumLinkDto> links) => new()
    {
        Code = code,
        Label = label,
        Expected = expected,
        Actual = actual,
        Passed = passed,
        AffectedCurriculumLinks = links.ToList(),
    };

    private static AffectedCurriculumLinkDto ModuleLink(Module module)
        => Link(ProgramAdvisoryTargetType.Module, module.Id, module.Name);

    private static AffectedCurriculumLinkDto ActivityLink(Activity activity)
        => Link(ProgramAdvisoryTargetType.Activity, activity.Id, activity.Name);

    private static AffectedCurriculumLinkDto Link(ProgramAdvisoryTargetType type, Guid id, string label)
        => new() { TargetType = type, Id = id, Label = label };
}
