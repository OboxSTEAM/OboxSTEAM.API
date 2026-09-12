using OboxSteam.Domain.Enums;

namespace OboxSteam.Domain.Entities;

public sealed class ProgramAdvisoryStreamRead : BaseEntity
{
    public Guid ProgramId { get; set; }
    public Program Program { get; set; } = null!;
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public AdvisoryStreamType StreamType { get; set; }
    public Guid? ThreadId { get; set; }
    public ProgramAdvisoryThread? Thread { get; set; }
    public long LastReadSequence { get; set; }
}
