namespace OboxSteam.Domain.Enums;

/// <summary>
/// Lifecycle of a student request for extra assignment attempts / personal deadline.
/// </summary>
public enum AssessmentRecoveryRequestStatus
{
    Pending,
    Approved,
    Rejected,
    Withdrawn
}
