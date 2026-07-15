using OboxSteam.Application.Notifications;

namespace OboxSteam.Application.Interfaces;

/// <summary>Resolves <see cref="NotificationAudience"/> to distinct user ids.</summary>
public interface INotificationRecipientResolver
{
    Task<IReadOnlyList<Guid>> ResolveAsync(NotificationAudience audience, CancellationToken cancellationToken = default);
}
