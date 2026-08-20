using OboxSteam.Application.Interfaces;
using OboxSteam.Application.Notifications;

namespace OboxSteam.Test.Helpers;

/// <summary>
/// In-memory <see cref="ISyncEventPublisher"/> that records published sync events
/// so tests can assert realtime fan-out without SignalR.
/// </summary>
public sealed class FakeSyncEventPublisher : ISyncEventPublisher
{
    public sealed record PublishedEvent(
        string Scope,
        NotificationAudience Audience,
        string EntityType,
        Guid EntityId);

    public List<PublishedEvent> Events { get; } = new();

    public Task PublishAsync(
        string scope,
        NotificationAudience audience,
        string entityType,
        Guid entityId,
        CancellationToken cancellationToken = default)
    {
        Events.Add(new PublishedEvent(scope, audience, entityType, entityId));
        return Task.CompletedTask;
    }
}
