namespace OboxSteam.Domain.Entities;

public sealed class CurriculumChangeSeen : BaseEntity
{
    public Guid ProgramId { get; set; }
    public Program Program { get; set; } = null!;

    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public long SeenVersion { get; set; }
}
