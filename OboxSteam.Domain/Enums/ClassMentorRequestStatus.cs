namespace OboxSteam.Domain.Enums;

/// <summary>
/// Lifecycle of a mentor's request to be assigned to a class cohort.
/// </summary>
public enum ClassMentorRequestStatus
{
    Pending,
    Approved,
    Rejected,
    Withdrawn
}
