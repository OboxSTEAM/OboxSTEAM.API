namespace OboxSteam.Domain.Enums;

/// <summary>
/// Lifecycle of a request to rejoin another class for experiential continuity.
/// Happy path: AwaitingClassSelection → MatchedPendingPayment → Completed | Withdrawn.
/// </summary>
public enum ClassRedeliveryRequestStatus
{
    /// <summary>Legacy; migrated to AwaitingClassSelection. No longer written.</summary>
    PendingAutoMatch,

    MatchedPendingPayment,

    /// <summary>Legacy waitlist; migrated to AwaitingClassSelection. No longer written.</summary>
    PendingManager,

    /// <summary>Legacy; unused in continuity flow.</summary>
    Approved,

    Rejected,
    Completed,

    /// <summary>Student cancelled the request only — program enrollment stays Active.</summary>
    Withdrawn,

    /// <summary>Student must pick among eligible Standard cohorts.</summary>
    AwaitingClassSelection,

    /// <summary>Legacy intensive offer; migrated to AwaitingClassSelection. No longer written.</summary>
    AwaitingIntensiveConsent,
}
