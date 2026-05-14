using System.ComponentModel.DataAnnotations;

namespace OboxSteam.Domain.Entities;

public class StandardizedTest : BaseEntity
{
    public Guid StudentId { get; set; }
    public User Student { get; set; } = null!;

    [MaxLength(100)]
    public string TestName { get; set; } = null!; // IELTS, SAT, etc.

    [MaxLength(50)]
    public string? Score { get; set; }

    public DateOnly? IssueDate { get; set; }
}
