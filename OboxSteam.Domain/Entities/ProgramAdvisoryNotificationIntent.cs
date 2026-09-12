using System.ComponentModel.DataAnnotations;
using OboxSteam.Domain.Enums;

namespace OboxSteam.Domain.Entities;

/// <summary>Durable advisory notification work committed with a domain event.</summary>
public sealed class ProgramAdvisoryNotificationIntent : BaseEntity
{
    public Guid ProgramId { get; set; }
    public Program Program { get; set; } = null!;
    public Guid EventId { get; set; }
    [MaxLength(100)] public string EventType { get; set; } = null!;
    public Guid? RecipientUserId { get; set; }
    public NotificationType NotificationType { get; set; }
    [MaxLength(4000)] public string PayloadJson { get; set; } = "{}";
    public AdvisoryNotificationIntentStatus Status { get; set; }
    public int AttemptCount { get; set; }
    public DateTime NextAttemptAt { get; set; }
    public DateTime? ProcessedAt { get; set; }
    [MaxLength(2000)] public string? LastError { get; set; }
}
