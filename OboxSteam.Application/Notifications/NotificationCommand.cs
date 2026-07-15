using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.Notifications;

/// <summary>Publish request built by <see cref="NotificationCatalog"/>; consumed by <see cref="Interfaces.INotificationPublisher"/>.</summary>
public sealed class NotificationCommand
{
    public NotificationType Type { get; }
    public NotificationAudience Audience { get; }
    public string Title { get; }
    public string? Body { get; }
    public NotificationPayload? Payload { get; }
    public Guid? ActorUserId { get; }
    public string? EntityType { get; }
    public Guid? EntityId { get; }

    public NotificationCommand(
        NotificationType type,
        NotificationAudience audience,
        string title,
        string? body = null,
        NotificationPayload? payload = null,
        Guid? actorUserId = null,
        string? entityType = null,
        Guid? entityId = null)
    {
        Type = type;
        Audience = audience ?? throw new ArgumentNullException(nameof(audience));
        Title = title ?? throw new ArgumentNullException(nameof(title));
        Body = body;
        Payload = payload;
        ActorUserId = actorUserId;
        EntityType = entityType;
        EntityId = entityId;
    }
}
