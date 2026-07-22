namespace OboxSteam.Domain.Entities;

/// <summary>
/// Fixed quiz question snapshot for one (Class, Assignment) pair, pulled from the shared bank.
/// Mentors edit this copy — never the master QuestionBank.
/// </summary>
public class ClassQuizQuestionSet : BaseEntity
{
    public Guid ClassId { get; set; }
    public Class Class { get; set; } = null!;

    public Guid AssignmentId { get; set; }
    public Assignment Assignment { get; set; } = null!;

    public DateTime PulledAt { get; set; }

    public ICollection<ClassQuizQuestion> Questions { get; set; } = new List<ClassQuizQuestion>();
}
