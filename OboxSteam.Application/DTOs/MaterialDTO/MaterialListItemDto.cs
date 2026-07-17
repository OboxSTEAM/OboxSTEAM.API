using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.DTOs.MaterialDTO;

/// <summary>
/// Flat material row for the manager materials list. Carries the full
/// program/course/activity context needed for the "Edit" deep-link.
/// </summary>
public class MaterialListItemDto
{
    public Guid Id { get; set; }

    public string Title { get; set; } = null!;

    public MaterialType MaterialType { get; set; }

    /// <summary>Upload time (CreatedAt from BaseEntity).</summary>
    public DateTime UploadedAt { get; set; }

    public Guid ActivityId { get; set; }
    public string ActivityName { get; set; } = null!;

    public Guid CourseId { get; set; }
    public string CourseName { get; set; } = null!;

    public Guid ProgramId { get; set; }
    public string ProgramName { get; set; } = null!;
}
