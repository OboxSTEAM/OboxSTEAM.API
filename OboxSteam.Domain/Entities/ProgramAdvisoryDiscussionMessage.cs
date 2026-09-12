using System.ComponentModel.DataAnnotations;

namespace OboxSteam.Domain.Entities;

public sealed class ProgramAdvisoryDiscussionMessage : BaseEntity
{
    public Guid ProgramId { get; set; }
    public Program Program { get; set; } = null!;
    public Guid AuthorUserId { get; set; }
    public User AuthorUser { get; set; } = null!;
    public long Sequence { get; set; }
    [MaxLength(10000)] public string Text { get; set; } = null!;
    [MaxLength(100)] public string ClientMessageId { get; set; } = null!;
    public ICollection<ProgramAdvisoryDiscussionMessageReference> References { get; set; } = [];
}
