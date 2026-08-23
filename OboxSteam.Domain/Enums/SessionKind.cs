namespace OboxSteam.Domain.Enums;

/// <summary>
/// Concrete class-session delivery kind. Aligns with <see cref="ActivityType"/>
/// for scheduled activities: LiveOnline / Offline. Assignment windows use
/// <see cref="AssignmentWindow"/>.
/// </summary>
public enum SessionKind
{
    LiveOnline,
    Offline,
    AssignmentWindow,
}
