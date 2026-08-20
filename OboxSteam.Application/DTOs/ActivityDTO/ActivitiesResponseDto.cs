using OboxSteam.Application.DTOs.MaterialDTO;
using OboxSteam.Application.DTOs.EnrollmentDTO;
using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.DTOs.ActivityDTO;

public class ActivitiesResponseDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = null!;
    public Guid CourseId { get; set; }
    public string Name { get; set; } = null!;
    public ActivityType ActivityType { get; set; }
    public string? Description { get; set; }
    public int ActivityOrder { get; set; }
    public int? DurationMinutes { get; set; }
    public bool RequireQrCheckin { get; set; }
    public bool RequireMediaEvidence { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    /// <summary>Learning material linked to this activity, when one exists.</summary>
    public MaterialResponseDto? Material { get; set; }

    /// <summary>Resume checkpoint when programEnrollmentId is provided.</summary>
    public ActivityLearningProgressDto? LearningProgress { get; set; }
}
