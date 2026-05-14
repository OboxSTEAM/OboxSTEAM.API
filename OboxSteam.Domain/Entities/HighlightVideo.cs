using System.ComponentModel.DataAnnotations;

namespace OboxSteam.Domain.Entities;

public class HighlightVideo : BaseEntity
{
    public Guid StudentId { get; set; }
    public User Student { get; set; } = null!;

    public Guid ProgramId { get; set; }
    public Program Program { get; set; } = null!;

    public string? VideoUrl { get; set; }

    [MaxLength(50)]
    public string? Status { get; set; }
}
