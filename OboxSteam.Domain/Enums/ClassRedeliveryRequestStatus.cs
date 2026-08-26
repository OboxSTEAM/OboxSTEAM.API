namespace OboxSteam.Domain.Enums;

/// <summary>
/// Lifecycle of a request to rejoin another class for experiential re-delivery.
/// </summary>
public enum ClassRedeliveryRequestStatus
{
    /// <summary>Legacy auto-match placeholder; new creates use AwaitingClassSelection or PendingManager.</summary>
    PendingAutoMatch,

    MatchedPendingPayment,
    PendingManager,
    Approved,
    Rejected,
    Completed,
    Withdrawn,

    /// <summary>Student must pick among eligible Standard cohorts.</summary>
    AwaitingClassSelection,

    /// <summary>Student must accept or decline an intensive remedial class offer.</summary>
    AwaitingIntensiveConsent,
}
