using System.ComponentModel.DataAnnotations;

namespace OboxSteam.Domain.Entities;

public class ProgramReview : BaseEntity
{
    public Guid ProgramId { get; set; }
    public Program Program { get; set; } = null!;

    public Guid StudentId { get; set; }
    public User Student { get; set; } = null!;

    /// <summary>Star rating from 1 to 5.</summary>
    public int StarRating { get; set; }

    [MaxLength(2000)]
    public string? Comment { get; set; }
}
