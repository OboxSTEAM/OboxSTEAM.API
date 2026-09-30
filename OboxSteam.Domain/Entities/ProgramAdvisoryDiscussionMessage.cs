using System.ComponentModel.DataAnnotations;
using OboxSteam.Domain.Enums;

namespace OboxSteam.Domain.Entities;

public sealed class ProgramAdvisoryDiscussionMessage : BaseEntity
{
    public Guid ProgramId { get; set; }
    public Program Program { get; set; } = null!;

    /// <summary>Null for <see cref="DiscussionMessageKind.System"/> messages.</summary>
    public Guid? AuthorUserId { get; set; }
    public User? AuthorUser { get; set; }

    public long Sequence { get; set; }
    public DiscussionMessageKind Kind { get; set; } = DiscussionMessageKind.User;
    [MaxLength(10000)] public string Text { get; set; } = null!;
    [MaxLength(100)] public string ClientMessageId { get; set; } = null!;

    public DiscussionSystemEventCode? SystemEventCode { get; set; }
    public string? SystemEventPayloadJson { get; set; }

    public DateTime? EditedAt { get; set; }

    /// <summary>
    /// Author retraction. Kept separate from <see cref="BaseEntity.IsDeleted"/> so the
    /// message still appears in the stream as a tombstone.
    /// </summary>
    public DateTime? RemovedAt { get; set; }
    public Guid? RemovedByUserId { get; set; }

    public DiscussionPinStatus? PinStatus { get; set; }
    public Guid? PinnedByUserId { get; set; }
    public DateTime? PinnedAt { get; set; }
    public Guid? AddressedByUserId { get; set; }
    public DateTime? AddressedAt { get; set; }
    public Guid? ResolvedByUserId { get; set; }
    public DateTime? ResolvedAt { get; set; }

    public ICollection<ProgramAdvisoryDiscussionMessageReference> References { get; set; } = [];
    public ICollection<ProgramAdvisoryDiscussionAttachment> Attachments { get; set; } = [];
}
