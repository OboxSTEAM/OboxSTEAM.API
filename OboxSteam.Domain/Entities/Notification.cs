using System.ComponentModel.DataAnnotations;
using OboxSteam.Domain.Enums;

namespace OboxSteam.Domain.Entities;

/// <summary>
/// Persisted in-app notification delivered to a single recipient via REST inbox and SignalR.
/// </summary>
public class Notification : BaseEntity
{
    public Guid RecipientUserId { get; set; }
    public User Recipient { get; set; } = null!;

    public NotificationType Type { get; set; }

    [MaxLength(200)]
    public string Title { get; set; } = null!;

    [MaxLength(2000)]
    public string? Body { get; set; }

    /// <summary>JSON payload for client deep-links (entity ids, program id, etc.).</summary>
    public string? PayloadJson { get; set; }

    public DateTime? ReadAt { get; set; }

    public Guid? ActorUserId { get; set; }

    [MaxLength(100)]
    public string? EntityType { get; set; }

    public Guid? EntityId { get; set; }
}
