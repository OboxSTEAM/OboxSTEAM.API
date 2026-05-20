namespace OboxSteam.Application.DTOs.CourseDTO;

public class CourseResponseDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = null!;
    public Guid ModuleId { get; set; }
    public Guid MentorId { get; set; }
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
