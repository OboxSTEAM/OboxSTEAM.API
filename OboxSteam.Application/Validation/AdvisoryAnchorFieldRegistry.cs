using OboxSteam.Application.DTOs.ProgramAdvisoryDTO;
using OboxSteam.Application.Utils;
using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.Validation;

/// <summary>
/// Source of truth for advisory <c>anchorField</c> / <c>fieldKey</c> values.
/// Unknown keys are rejected with 400.
/// </summary>
public static class AdvisoryAnchorFieldRegistry
{
    private static readonly IReadOnlyDictionary<ProgramAdvisoryTargetType, (string Key, string Label)[]> Fields =
        new Dictionary<ProgramAdvisoryTargetType, (string Key, string Label)[]>
        {
            [ProgramAdvisoryTargetType.Program] =
            [
                ("name", "Name"),
                ("code", "Code"),
                ("description", "Description"),
                ("skillsGained", "Skills gained"),
            ],
            [ProgramAdvisoryTargetType.Module] =
            [
                ("name", "Name"),
                ("code", "Code"),
                ("type", "Module type"),
                ("learningOutcomes", "Learning outcomes"),
            ],
            [ProgramAdvisoryTargetType.Course] =
            [
                ("name", "Name"),
                ("code", "Code"),
                ("description", "Description"),
            ],
            [ProgramAdvisoryTargetType.Activity] =
            [
                ("name", "Name"),
                ("type", "Activity type"),
                ("description", "Description"),
                ("durationMinutes", "Duration (minutes)"),
                ("requireQrCheckin", "Require QR check-in"),
                ("requireMediaEvidence", "Require media evidence"),
            ],
            [ProgramAdvisoryTargetType.Assignment] =
            [
                ("title", "Title"),
                ("code", "Code"),
                ("description", "Description"),
                ("assignmentType", "Assignment type"),
                ("maxPoints", "Max points"),
                ("passScore", "Pass score"),
                ("isRequiredForModulePass", "Required for module pass"),
            ],
            [ProgramAdvisoryTargetType.ResearchMilestone] =
            [
                ("title", "Title"),
                ("code", "Code"),
                ("description", "Description"),
                ("isCapstone", "Capstone"),
            ],
            [ProgramAdvisoryTargetType.Material] =
            [
                ("title", "Title"),
                ("materialType", "Material type"),
                ("fileName", "File name"),
            ],
            [ProgramAdvisoryTargetType.RubricCriterion] =
            [
                ("name", "Name"),
                ("description", "Description"),
                ("evidenceGuidance", "Evidence guidance"),
                ("maxScore", "Max score"),
            ],
        };

    public static IReadOnlyList<AdvisoryAnchorFieldDto> ListAll()
        => Fields
            .SelectMany(pair => pair.Value.Select(field => new AdvisoryAnchorFieldDto
            {
                TargetType = pair.Key,
                FieldKey = field.Key,
                Label = field.Label,
            }))
            .ToList();

    public static void EnsureAllowed(ProgramAdvisoryTargetType targetType, string? fieldKey)
    {
        if (string.IsNullOrWhiteSpace(fieldKey))
        {
            return;
        }

        if (!Fields.TryGetValue(targetType, out var fields)
            || !fields.Any(f => string.Equals(f.Key, fieldKey, StringComparison.OrdinalIgnoreCase)))
        {
            throw ErrorHelper.BadRequest(
                "AnchorField is not supported for this target type.",
                "ADVISORY_ANCHOR_FIELD_INVALID");
        }
    }
}
