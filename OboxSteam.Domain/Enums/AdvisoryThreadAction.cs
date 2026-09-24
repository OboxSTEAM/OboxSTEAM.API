namespace OboxSteam.Domain.Enums;

/// <summary>
/// One per-item action on an advisory thread. Replaces status PATCH transitions.
/// </summary>
public enum AdvisoryThreadAction
{
    MarkFixed,
    Acknowledge,
    Accept,
}
