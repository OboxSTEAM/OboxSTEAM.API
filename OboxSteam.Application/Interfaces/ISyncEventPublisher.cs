using OboxSteam.Application.Notifications;

namespace OboxSteam.Application.Interfaces;

/// <summary>
/// Publishes ephemeral <see cref="Realtime.SyncEvent"/> hints: resolves the audience
/// per-user like notifications do, but never persists anything.
/// </summary>
public interface ISyncEventPublisher
{
    Task PublishAsync(
        string scope,
        NotificationAudience audience,
        string entityType,
        Guid entityId,
        CancellationToken cancellationToken = default);
}
