using OboxSteam.Application.DTOs.ActivityDTO;

namespace OboxSteam.Application.DTOs.CourseDTO;

public class CourseResponseDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = null!;
    public Guid ModuleId { get; set; }
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public int CourseOrder { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public List<ActivitiesResponseDto> Activities { get; set; } = new();
}
