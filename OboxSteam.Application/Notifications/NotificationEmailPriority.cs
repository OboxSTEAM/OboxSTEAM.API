using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.Notifications;

/// <summary>
/// Types that also send transactional email after inbox persist + SignalR.
/// Rich payment emails (parent checkout link, invoice, enrollment confirmation)
/// stay on <c>IEmailService</c> and are excluded here to avoid duplicate mail
/// without a payment CTA. Session/assignment reminder types should be added
/// when those catalog events exist.
/// </summary>
public static class NotificationEmailPriority
{
    private static readonly HashSet<NotificationType> Types =
    [
        NotificationType.ProgramPendingPayment,
        NotificationType.ModuleRetakePendingPayment,
        NotificationType.PendingPaymentExpired,
        NotificationType.PaymentFailed,
        NotificationType.PaymentCancelled,
        NotificationType.ResearchReturnedForRevision,
        NotificationType.ResearchWorkSubmitted,
        NotificationType.SessionStartingSoon
    ];

    public static bool ShouldEmail(NotificationType type) => Types.Contains(type);
}
