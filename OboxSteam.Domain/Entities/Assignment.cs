using System.ComponentModel.DataAnnotations;
using OboxSteam.Domain.Enums;

namespace OboxSteam.Domain.Entities;

public class Assignment : BaseEntity
{
    [MaxLength(50)]
    public string Code { get; set; } = null!;

    public Guid ModuleId { get; set; }
    public Module Module { get; set; } = null!;

    /// <summary>Null if it belongs to the whole Module (not a specific course).</summary>
    public Guid? CourseId { get; set; }
    public Course? Course { get; set; }

    [MaxLength(255)]
    public string Title { get; set; } = null!;

    public string? Description { get; set; }

    public AssignmentType AssignmentType { get; set; }

    public int MaxPoints { get; set; }

    public DateTime? DueDate { get; set; }

    public bool AllowShuffle { get; set; } = true;

    // Navigation
    public ICollection<QuizQuestion> QuizQuestions { get; set; } = new List<QuizQuestion>();
    public ICollection<Submission> Submissions { get; set; } = new List<Submission>();
}
