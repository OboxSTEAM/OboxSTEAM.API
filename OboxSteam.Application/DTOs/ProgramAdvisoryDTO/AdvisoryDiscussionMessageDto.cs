using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.DTOs.ProgramAdvisoryDTO;

public sealed class AdvisoryDiscussionMessageDto
{
    public Guid Id { get; set; }
    public Guid ProgramId { get; set; }
    public long Sequence { get; set; }
    public string Cursor { get; set; } = null!;
    public DiscussionMessageKind Kind { get; set; }
    public Guid? AuthorUserId { get; set; }
    public string? AuthorName { get; set; }
    public RoleType? AuthorRole { get; set; }
    public string Text { get; set; } = null!;
    public string ClientMessageId { get; set; } = null!;
    public AdvisoryDiscussionSystemEventDto? SystemEvent { get; set; }

    /// <summary>Ordered by first token occurrence in <see cref="Text"/>.</summary>
    public List<AdvisoryReferenceDto> References { get; set; } = [];

    public List<AdvisoryDiscussionAttachmentDto> Attachments { get; set; } = [];
    public AdvisoryDiscussionPinDto? Pin { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? EditedAt { get; set; }

    /// <summary>Author removal tombstone: empty text, no references, attachments, or pin.</summary>
    public bool IsDeleted { get; set; }
}
