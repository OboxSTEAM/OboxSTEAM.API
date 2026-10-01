using OboxSteam.Application.Utils;
using OboxSteam.Domain.Entities;

namespace OboxSteam.Application.Validation;

/// <summary>
/// Framework name and rule-value validation. Multiple failures are joined into one message.
/// </summary>
public static class ProgramFrameworkValidator
{
    public const int MaxNameLength = 255;
    public const string RulesInvalidCode = "FRAMEWORK_RULES_INVALID";

    public static void ValidateName(string? name, bool required)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            if (required)
            {
                throw ErrorHelper.BadRequest("Framework name is required.");
            }

            return;
        }

        if (name.Trim().Length > MaxNameLength)
        {
            throw ErrorHelper.BadRequest($"Framework name must be at most {MaxNameLength} characters.");
        }
    }

    /// <summary>
    /// Every value ≥ 0, each min ≤ its max, ratios in 0–100 and their sum ≤ 100.
    /// Throws 400 <see cref="RulesInvalidCode"/> listing every violation.
    /// </summary>
    public static void ValidateRules(ProgramFrameworkVersion version)
    {
        ArgumentNullException.ThrowIfNull(version);
        var errors = new List<string>();

        foreach (var (field, value) in new (string, int?)[]
                 {
                     (nameof(version.MinModules), version.MinModules),
                     (nameof(version.MaxModules), version.MaxModules),
                     (nameof(version.MinCoursesPerModule), version.MinCoursesPerModule),
                     (nameof(version.MaxCoursesPerModule), version.MaxCoursesPerModule),
                     (nameof(version.MinTotalHours), version.MinTotalHours),
                     (nameof(version.MaxTotalHours), version.MaxTotalHours),
                     (nameof(version.MaxActivityMinutes), version.MaxActivityMinutes),
                     (nameof(version.MinOfflineSessions), version.MinOfflineSessions),
                     (nameof(version.MinLiveSessions), version.MinLiveSessions),
                     (nameof(version.MinOfflineRatioPercent), version.MinOfflineRatioPercent),
                     (nameof(version.MinLiveRatioPercent), version.MinLiveRatioPercent),
                     (nameof(version.MinMaterialsPerActivity), version.MinMaterialsPerActivity),
                     (nameof(version.MinDescriptionLength), version.MinDescriptionLength),
                     (nameof(version.MinSkillsGained), version.MinSkillsGained),
                 })
        {
            if (value < 0)
            {
                errors.Add($"{field} must be 0 or greater.");
            }
        }

        AddMinMaxError(errors, nameof(version.MinModules), version.MinModules, nameof(version.MaxModules), version.MaxModules);
        AddMinMaxError(
            errors,
            nameof(version.MinCoursesPerModule),
            version.MinCoursesPerModule,
            nameof(version.MaxCoursesPerModule),
            version.MaxCoursesPerModule);
        AddMinMaxError(errors, nameof(version.MinTotalHours), version.MinTotalHours, nameof(version.MaxTotalHours), version.MaxTotalHours);

        if (version.MinOfflineRatioPercent > 100)
        {
            errors.Add($"{nameof(version.MinOfflineRatioPercent)} must be at most 100.");
        }

        if (version.MinLiveRatioPercent > 100)
        {
            errors.Add($"{nameof(version.MinLiveRatioPercent)} must be at most 100.");
        }

        if ((version.MinOfflineRatioPercent ?? 0) + (version.MinLiveRatioPercent ?? 0) > 100)
        {
            errors.Add(
                $"{nameof(version.MinOfflineRatioPercent)} + {nameof(version.MinLiveRatioPercent)} must be at most 100.");
        }

        if (errors.Count > 0)
        {
            throw ErrorHelper.BadRequest(string.Join(" ", errors), RulesInvalidCode);
        }
    }

    private static void AddMinMaxError(List<string> errors, string minField, int? min, string maxField, int? max)
    {
        if (min.HasValue && max.HasValue && min.Value > max.Value)
        {
            errors.Add($"{minField} must be less than or equal to {maxField}.");
        }
    }
}
