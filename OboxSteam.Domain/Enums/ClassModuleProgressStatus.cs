namespace OboxSteam.Domain.Enums;

/// <summary>
/// Furthest non-cancelled session status on a class for one module.
/// InProgress/Completed count as "started" for rebuy class eligibility.
/// </summary>
public enum ClassModuleProgressStatus
{
    NotStarted = 0,
    InProgress = 1,
    Completed = 2,
}
