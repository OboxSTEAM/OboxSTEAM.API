using System.ComponentModel.DataAnnotations;

namespace OboxSteam.Domain.Entities;

/// <summary>
/// Join table linking Programs to Experts for the advisory/certification board.
/// Composite key: (ProgramId, ExpertId)
/// </summary>
public class ProgramBoard : BaseEntity
{
    public Guid ProgramId { get; set; }
    public Program Program { get; set; } = null!;

    public Guid ExpertId { get; set; }
    public Expert Expert { get; set; } = null!;

    [MaxLength(255)]
    public string? RoleInBoard { get; set; } // Signatures appear on Coursera-style Certificate
}
