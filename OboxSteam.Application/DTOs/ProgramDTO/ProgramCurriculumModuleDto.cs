using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.DTOs.ProgramDTO;

public class ProgramCurriculumModuleDto
{
    public Guid ModuleId { get; set; }

    public string ModuleName { get; set; } = null!;

    public int ModuleOrder { get; set; }

    public ModuleType ModuleType { get; set; }

    public Guid? PrerequisiteModuleId { get; set; }

    /// <summary>Theory and Experiential modules: course → activity → material.</summary>
    public List<ProgramCurriculumCourseDto> Courses { get; set; } = new();

    /// <summary>Research modules: milestone → activity → material.</summary>
    public List<ProgramCurriculumMilestoneDto> Milestones { get; set; } = new();
}
