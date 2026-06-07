namespace OboxSteam.Domain.Entities;

public class QuizAnswer : BaseEntity
{
    public Guid SubmissionId { get; set; }
    public Submission Submission { get; set; } = null!;

    public Guid QuizQuestionId { get; set; }
    public QuizQuestion QuizQuestion { get; set; } = null!;

    /// <summary>
    /// Each selected option is stored as a separate row.
    /// SingleChoice → 1 row per question; MultipleChoice → N rows per question.
    /// </summary>
    public Guid QuizOptionId { get; set; }
    public QuizOption QuizOption { get; set; } = null!;
}
