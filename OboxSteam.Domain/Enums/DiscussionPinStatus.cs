namespace OboxSteam.Domain.Enums;

/// <summary>"Cần sửa" pin state. Only <see cref="Open"/> blocks approval.</summary>
public enum DiscussionPinStatus
{
    Open,
    Addressed,
    Resolved,
}
