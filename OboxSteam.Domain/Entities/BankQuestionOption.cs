namespace OboxSteam.Domain.Entities;

public class BankQuestionOption : BaseEntity
{
    public Guid BankQuestionId { get; set; }
    public BankQuestion BankQuestion { get; set; } = null!;

    public string OptionText { get; set; } = null!;

    /// <summary>Flag used by the system for auto-grading.</summary>
    public bool IsCorrect { get; set; }
}
