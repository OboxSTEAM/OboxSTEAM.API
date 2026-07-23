using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.DTOs.AssignmentDTO;

/// <summary>
/// Flat assignment row for the manager catalog.
/// Carries module/program context for the Edit deep-link.
/// </summary>
public class AssignmentListItemDto
{
    public Guid Id { get; set; }

    public string Code { get; set; } = null!;

    public string Title { get; set; } = null!;

    public AssignmentType AssignmentType { get; set; }

    public Guid ModuleId { get; set; }

    public Guid? CourseId { get; set; }

    public int MaxPoints { get; set; }

    public decimal PassScore { get; set; }

    public DateTime? DueDate { get; set; }

    public Guid? QuestionBankId { get; set; }

    /// <summary>Quiz config: number of questions drawn per attempt (null = all).</summary>
    public int? QuestionCount { get; set; }

    public string ModuleName { get; set; } = null!;

    public Guid ProgramId { get; set; }

    public string ProgramName { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }
}
