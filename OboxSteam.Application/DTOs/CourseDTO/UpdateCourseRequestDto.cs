namespace OboxSteam.Application.DTOs.CourseDTO;

public class UpdateCourseRequestDto
{
    public string? Code { get; set; }
    public Guid? ModuleId { get; set; }
    public Guid? MentorId { get; set; }
    public string? Name { get; set; }
    public string? Description { get; set; }
}
