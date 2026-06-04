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

    /// <summary>
    /// References the original question in the QuestionBank that this snapshot was copied from.
    /// Null when the question was created directly on the assignment (no bank involved).
    /// Kept for audit: if a bank question is later edited, this record still reflects
    /// exactly what the student saw at the time of their attempt.
    /// </summary>
    public Guid? BankQuestionId { get; set; }
    public BankQuestion? BankQuestion { get; set; }

    /// <summary>
    /// The student's attempt number this snapshot belongs to (1-based).
    /// Each attempt generates its own set of QuizQuestion snapshots so that
    /// different random draws / shuffle orders are preserved independently.
    /// </summary>
    public int AttemptNumber { get; set; } = 1;

    // Navigation
    public ICollection<QuizOption> Options { get; set; } = new List<QuizOption>();
}
