namespace OboxSteam.Application.DTOs.CourseDTO;

public class CreateCourseRequestDto
{
    public string Code { get; set; } = null!;
    public Guid ModuleId { get; set; }
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public int CourseOrder { get; set; }
}
