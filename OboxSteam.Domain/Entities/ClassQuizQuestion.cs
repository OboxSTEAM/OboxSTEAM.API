using System.ComponentModel.DataAnnotations;

namespace OboxSteam.Domain.Entities;

public class ClassQuizQuestion : BaseEntity
{
    public Guid ClassQuizQuestionSetId { get; set; }
    public ClassQuizQuestionSet ClassQuizQuestionSet { get; set; } = null!;

    /// <summary>Traceability only — BankQuestion copied at pull time.</summary>
    public Guid? SourceBankQuestionId { get; set; }

    public string QuestionText { get; set; } = null!;

    [MaxLength(50)]
    public string QuestionType { get; set; } = null!;

    public decimal Points { get; set; }

    public int DifficultyLevel { get; set; }

    public int OrderIndex { get; set; }

    public ICollection<ClassQuizQuestionOption> Options { get; set; } = new List<ClassQuizQuestionOption>();
}
