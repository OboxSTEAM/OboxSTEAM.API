namespace OboxSteam.Domain.Enums;

/// <summary>
/// Distinguishes initial highlight generation from user-driven output trimming.
/// </summary>
public enum HighlightVideoGenerationKind
{
    Initial,
    Trim,
    SegmentAdd
}
