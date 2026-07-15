using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.DTOs.NotificationDTO;

/// <summary>Inbox item and SignalR push payload.</summary>
public sealed class NotificationDto
{
    public Guid Id { get; set; }
    public Guid RecipientUserId { get; set; }
    public NotificationType Type { get; set; }
    public string Title { get; set; } = null!;
    public string? Body { get; set; }
    public string? PayloadJson { get; set; }
    public DateTime? ReadAt { get; set; }
    public Guid? ActorUserId { get; set; }
    public string? EntityType { get; set; }
    public Guid? EntityId { get; set; }
    public DateTime CreatedAt { get; set; }
}
