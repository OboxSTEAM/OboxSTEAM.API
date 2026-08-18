using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.Notifications;

/// <summary>Publish request built by <see cref="NotificationCatalog"/>; consumed by <see cref="Interfaces.INotificationPublisher"/>.</summary>
public sealed class NotificationCommand
{
    public NotificationType Type { get; }

    public NotificationAudience Audience { get; }

    /// <summary>Default (usually student) title with shared tokens already interpolated.</summary>
    public string Title { get; }

    /// <summary>Default (usually student) body with shared tokens already interpolated.</summary>
    public string? Body { get; }

    public NotificationRoleTemplates Templates { get; }

    public IReadOnlyDictionary<string, string> Tokens { get; }

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
        : this(
            type,
            audience,
            NotificationRoleTemplates.FromDefault(title, body),
            payload,
            actorUserId,
            entityType,
            entityId,
            tokens: null)
    {
    }

    public NotificationCommand(
        NotificationType type,
        NotificationAudience audience,
        NotificationRoleTemplates templates,
        NotificationPayload? payload = null,
        Guid? actorUserId = null,
        string? entityType = null,
        Guid? entityId = null,
        IReadOnlyDictionary<string, string>? tokens = null)
    {
        Type = type;
        Audience = audience ?? throw new ArgumentNullException(nameof(audience));
        Templates = templates ?? throw new ArgumentNullException(nameof(templates));
        Tokens = tokens ?? new Dictionary<string, string>(StringComparer.Ordinal);
        Payload = payload;
        ActorUserId = actorUserId;
        EntityType = entityType;
        EntityId = entityId;

        var renderedDefault = NotificationTemplateRenderer.Interpolate(templates.Default, Tokens);
        Title = renderedDefault.Title;
        Body = renderedDefault.Body;
    }
}
