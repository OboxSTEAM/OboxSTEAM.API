using System.ComponentModel;
using OboxSteam.Application.Notifications;
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

    /// <summary>
    /// Typed deeplink bag (camelCase). Prefer this over <see cref="PayloadJson"/>.
    /// Null keys are omitted when the bag was stored; missing properties mean unused for that type.
    /// </summary>
    [Description(
        "Typed deep-link ids for client routing (programId, enrollmentId, nextActivityId, " +
        "assignmentId, classId, mediaAssetId, …) plus display names " +
        "(studentName, actorName, className, programName). Prefer this over payloadJson.")]
    public NotificationPayload? Payload { get; set; }

    /// <summary>
    /// Legacy JSON string of <see cref="Payload"/> (same content). Prefer <see cref="Payload"/>.
    /// </summary>
    [Description(
        "Legacy camelCase JSON string of the typed payload object. Prefer `payload`. " +
        "Same shape as NotificationPayload.")]
    public string? PayloadJson { get; set; }

    public DateTime? ReadAt { get; set; }
    public Guid? ActorUserId { get; set; }
    public string? EntityType { get; set; }
    public Guid? EntityId { get; set; }
    public DateTime CreatedAt { get; set; }
}
