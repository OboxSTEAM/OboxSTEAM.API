namespace OboxSteam.Domain.Entities;

public sealed class ProgramAdvisoryRead : BaseEntity
{
    public Guid ProgramId { get; set; }
    public Program Program { get; set; } = null!;
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public DateTime LastReadAt { get; set; }
}
