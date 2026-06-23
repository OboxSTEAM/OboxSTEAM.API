using OboxSteam.Application.DTOs.EnrollmentDTO;
using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.DTOs.ActivityProgressDTO;

/// <summary>
/// Student activity completion progress within a module enrollment attempt.
/// </summary>
public class ActivityProgressResponseDto
{
    public Guid Id { get; set; }
    public Guid StudentId { get; set; }
    public Guid ActivityId { get; set; }
    public Guid ModuleEnrollmentId { get; set; }
    public ActivityStatus ActivityStatus { get; set; }
    public bool IsCompleted { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    /// <summary>Updated module progress after marking an activity Done; null on start.</summary>
    public decimal? ModuleProgressPercent { get; set; }

    /// <summary>Updated program progress when a module reaches 100%; null otherwise.</summary>
    public decimal? ProgramProgressPercent { get; set; }

    public string ActivityCode { get; set; } = null!;
    public string ActivityName { get; set; } = null!;
    public ActivityType ActivityType { get; set; }
    public int ActivityOrder { get; set; }

    public ActivityResumeStateDto? ResumeState { get; set; }

    public DateTime? LastAccessedAt { get; set; }
}
