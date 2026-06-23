using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.DTOs.EnrollmentDTO;

public class EnrollmentCurriculumDto
{
    public Guid EnrollmentId { get; set; }

    public Guid ProgramId { get; set; }

    public string ProgramName { get; set; } = null!;

    public decimal ProgressPercent { get; set; }

    public Guid? CurrentActivityId { get; set; }

    public List<EnrollmentCurriculumModuleDto> Modules { get; set; } = [];
}

public class EnrollmentCurriculumModuleDto
{
    public Guid ModuleId { get; set; }

    public string ModuleName { get; set; } = null!;

    public int ModuleOrder { get; set; }

    public ModuleType ModuleType { get; set; }

    public Guid? PrerequisiteModuleId { get; set; }

    public bool IsLocked { get; set; }

    public string? LockReason { get; set; }

    public Guid? ModuleEnrollmentId { get; set; }

    public List<EnrollmentCurriculumCourseDto> Courses { get; set; } = [];

    public List<EnrollmentCurriculumMilestoneDto> Milestones { get; set; } = [];
}

public class EnrollmentCurriculumCourseDto
{
    public Guid CourseId { get; set; }

    public string CourseName { get; set; } = null!;

    public int CourseOrder { get; set; }

    public List<EnrollmentCurriculumActivityDto> Activities { get; set; } = [];
}

public class EnrollmentCurriculumMilestoneDto
{
    public Guid MilestoneId { get; set; }

    public string MilestoneName { get; set; } = null!;

    public int MilestoneOrder { get; set; }

    public List<EnrollmentCurriculumActivityDto> Activities { get; set; } = [];
}

public class EnrollmentCurriculumActivityDto
{
    public Guid ActivityId { get; set; }

    public string ActivityName { get; set; } = null!;

    public int ActivityOrder { get; set; }

    public ActivityType ActivityType { get; set; }

    /// <summary>Nav state: completed, current, available, or locked.</summary>
    public string Status { get; set; } = null!;

    public EnrollmentCurriculumMaterialDto? Material { get; set; }
}

public class EnrollmentCurriculumMaterialDto
{
    public Guid MaterialId { get; set; }

    public string MaterialName { get; set; } = null!;

    public MaterialType MaterialType { get; set; }
}
