using OboxSteam.Application.Notifications;

namespace OboxSteam.Application.Interfaces;

/// <summary>
/// Resolves <see cref="NotificationAudience"/> to per-recipient rows
/// (<c>UserId</c>, role, optional context student).
/// </summary>
public interface INotificationRecipientResolver
{
    Task<IReadOnlyList<NotificationRecipient>> ResolveAsync(
        NotificationAudience audience,
        CancellationToken cancellationToken = default);
}
