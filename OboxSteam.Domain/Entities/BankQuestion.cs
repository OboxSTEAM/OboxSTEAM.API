using System.ComponentModel.DataAnnotations;

namespace OboxSteam.Domain.Entities;

public class BankQuestion : BaseEntity
{
    public Guid QuestionBankId { get; set; }
    public QuestionBank QuestionBank { get; set; } = null!;

    public string QuestionText { get; set; } = null!;

    [MaxLength(50)]
    public string QuestionType { get; set; } = null!; // SingleChoice, MultipleChoice

    public decimal Points { get; set; } = 1;

    /// <summary>Difficulty from 1 (easiest) to 5 (hardest).</summary>
    public int DifficultyLevel { get; set; } = 3;

    public int OrderIndex { get; set; }

    // Navigation
    public ICollection<BankQuestionOption> Options { get; set; } = new List<BankQuestionOption>();
}
