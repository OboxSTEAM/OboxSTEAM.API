namespace OboxSteam.Domain.Enums;

/// <summary>
/// Lifecycle of a request to rejoin another class for experiential re-delivery.
/// </summary>
public enum ClassRedeliveryRequestStatus
{
    PendingAutoMatch,
    MatchedPendingPayment,
    PendingManager,
    Approved,
    Rejected,
    Completed,
    Withdrawn
}
