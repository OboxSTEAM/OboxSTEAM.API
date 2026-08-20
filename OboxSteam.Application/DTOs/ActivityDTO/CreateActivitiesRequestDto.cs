using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.DTOs.ActivityDTO;

public class CreateActivitiesRequestDto
{
    public string Code { get; set; } = null!;
    public Guid CourseId { get; set; }
    public string Name { get; set; } = null!;
    public ActivityType ActivityType { get; set; }
    public string? Description { get; set; }
    public int ActivityOrder { get; set; }

    /// <summary>Session length in minutes. Required for LiveOnline/Offline, must be null for SelfPaced.</summary>
    public int? DurationMinutes { get; set; }

    public bool RequireQrCheckin { get; set; }
    public bool RequireMediaEvidence { get; set; }
}
