namespace OboxSteam.Domain.Entities;

public sealed class ProgramAdvisoryMessage : BaseEntity
{
    public Guid ThreadId { get; set; }
    public ProgramAdvisoryThread Thread { get; set; } = null!;
    public Guid AuthorUserId { get; set; }
    public User AuthorUser { get; set; } = null!;
    public string Message { get; set; } = null!;
}
