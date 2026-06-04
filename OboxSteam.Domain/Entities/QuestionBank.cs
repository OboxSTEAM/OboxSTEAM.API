using System.ComponentModel.DataAnnotations;

namespace OboxSteam.Domain.Entities;

public class QuestionBank : BaseEntity
{
    public Guid CourseId { get; set; }
    public Course Course { get; set; } = null!;

    [MaxLength(255)]
    public string Name { get; set; } = null!;

    public string? Description { get; set; }

    // Navigation
    public ICollection<BankQuestion> Questions { get; set; } = new List<BankQuestion>();
    public ICollection<Assignment> Assignments { get; set; } = new List<Assignment>();
}
