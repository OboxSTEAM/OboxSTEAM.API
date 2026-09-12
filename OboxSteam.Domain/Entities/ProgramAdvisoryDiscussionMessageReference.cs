namespace OboxSteam.Domain.Entities;

public sealed class ProgramAdvisoryDiscussionMessageReference : BaseEntity
{
    public Guid MessageId { get; set; }
    public ProgramAdvisoryDiscussionMessage Message { get; set; } = null!;
    public Guid ReferenceId { get; set; }
    public ProgramAdvisoryReference Reference { get; set; } = null!;
    public int Ordinal { get; set; }
}
