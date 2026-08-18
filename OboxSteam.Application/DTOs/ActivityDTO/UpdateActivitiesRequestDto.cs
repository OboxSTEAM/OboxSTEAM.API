using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.DTOs.ActivityDTO;

public class UpdateActivitiesRequestDto
{
    public string? Code { get; set; }
    public Guid? CourseId { get; set; }
    public string? Name { get; set; }
    public ActivityType? ActivityType { get; set; }
    public string? Description { get; set; }
    public int? ActivityOrder { get; set; }
    public string? Location { get; set; }
    public DateTime? StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public bool? RequireQrCheckin { get; set; }
    public bool? RequireMediaEvidence { get; set; }
}
