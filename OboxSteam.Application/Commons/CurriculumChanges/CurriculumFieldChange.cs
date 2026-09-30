using System.Text.Json.Nodes;

namespace OboxSteam.Application.Commons.CurriculumChanges;

/// <summary>
/// One field transition. <see cref="Label"/> is only stored for dynamic keys
/// (skill and milestone-activity links) whose display name cannot be derived later.
/// </summary>
public sealed class CurriculumFieldChange
{
    public string FieldKey { get; set; } = null!;
    public string? Label { get; set; }
    public JsonNode? Before { get; set; }
    public JsonNode? After { get; set; }
}
