namespace OboxSteam.Application.DTOs.ProgramDTO;

public class ProgramCurriculumCourseDto
{
    public Guid CourseId { get; set; }

    public string CourseName { get; set; } = null!;

    public int CourseOrder { get; set; }

    public List<ProgramCurriculumActivityDto> Activities { get; set; } = new();
}
