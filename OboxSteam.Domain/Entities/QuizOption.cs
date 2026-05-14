namespace OboxSteam.Domain.Entities;

public class QuizOption : BaseEntity
{
    public Guid QuestionId { get; set; }
    public QuizQuestion Question { get; set; } = null!;

    public string OptionText { get; set; } = null!;

    /// <summary>Flag used by the system for auto-grading.</summary>
    public bool IsCorrect { get; set; }
}
