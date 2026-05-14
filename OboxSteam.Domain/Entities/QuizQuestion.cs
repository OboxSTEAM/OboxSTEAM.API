using System.ComponentModel.DataAnnotations;

namespace OboxSteam.Domain.Entities;

public class QuizQuestion : BaseEntity
{
    public Guid AssignmentId { get; set; }
    public Assignment Assignment { get; set; } = null!;

    public string QuestionText { get; set; } = null!;

    [MaxLength(50)]
    public string QuestionType { get; set; } = null!; // SingleChoice, MultipleChoice

    public decimal Points { get; set; } = 1;

    public int OrderIndex { get; set; }

    // Navigation
    public ICollection<QuizOption> Options { get; set; } = new List<QuizOption>();
}
