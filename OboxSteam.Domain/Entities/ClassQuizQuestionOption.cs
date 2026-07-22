namespace OboxSteam.Domain.Entities;

public class ClassQuizQuestionOption : BaseEntity
{
    public Guid ClassQuizQuestionId { get; set; }
    public ClassQuizQuestion ClassQuizQuestion { get; set; } = null!;

    public string OptionText { get; set; } = null!;

    public bool IsCorrect { get; set; }
}
