namespace OboxSteam.Application.DTOs.QuestionBankDTO;

/// <summary>
/// Flat question-bank row for the manager catalog and quiz picker.
/// Carries program/module/course context for the Edit deep-link.
/// </summary>
public class QuestionBankListItemDto
{
    public Guid Id { get; set; }

    public Guid CourseId { get; set; }

    public string Name { get; set; } = null!;

    public string? Description { get; set; }

    /// <summary>Count of non-deleted questions in this bank.</summary>
    public int QuestionCount { get; set; }

    public string CourseName { get; set; } = null!;

    public Guid ModuleId { get; set; }

    public string ModuleName { get; set; } = null!;

    public Guid ProgramId { get; set; }

    public string ProgramName { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }
}
