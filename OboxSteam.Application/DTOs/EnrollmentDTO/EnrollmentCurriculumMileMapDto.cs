using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.DTOs.EnrollmentDTO;

public class EnrollmentCurriculumMileMapDto
{
    public Guid EnrollmentId { get; set; }

    public Guid ProgramId { get; set; }

    public string ProgramName { get; set; } = null!;

    public decimal ProgressPercent { get; set; }

    public List<EnrollmentCurriculumMileMapModuleDto> Modules { get; set; } = [];
}

public class EnrollmentCurriculumMileMapModuleDto
{
    /// <summary>Static catalog information about the module.</summary>
    public EnrollmentCurriculumMileMapModuleInfoDto ModuleInfo { get; set; } = null!;

    /// <summary>The student's learning state for this module.</summary>
    public EnrollmentCurriculumMileMapModuleLearningDto Learning { get; set; } = null!;

    public List<EnrollmentCurriculumMileMapCourseDto> Courses { get; set; } = [];

    public List<EnrollmentCurriculumMileMapMilestoneDto> Milestones { get; set; } = [];
}

public class EnrollmentCurriculumMileMapModuleInfoDto
{
    public Guid ModuleId { get; set; }

    public string ModuleName { get; set; } = null!;

    public int ModuleOrder { get; set; }

    public ModuleType ModuleType { get; set; }

    public Guid? PrerequisiteModuleId { get; set; }

    public Guid? ModuleEnrollmentId { get; set; }
}

public class EnrollmentCurriculumMileMapModuleLearningDto
{
    /// <summary>Module state: available, in_progress, completed, or locked.</summary>
    public string Status { get; set; } = null!;

    public decimal ProgressPercent { get; set; }

    public bool IsLocked { get; set; }

    public string? LockReason { get; set; }
}

public class EnrollmentCurriculumMileMapCourseDto
{
    public Guid CourseId { get; set; }

    public string CourseName { get; set; } = null!;

    public int CourseOrder { get; set; }

    public List<EnrollmentCurriculumMileMapActivityDto> Activities { get; set; } = [];
}

public class EnrollmentCurriculumMileMapMilestoneDto
{
    public Guid MilestoneId { get; set; }

    public string MilestoneName { get; set; } = null!;

    public int MilestoneOrder { get; set; }

    public bool IsCapstone { get; set; }

    public List<EnrollmentCurriculumMileMapActivityDto> Activities { get; set; } = [];
}

public class EnrollmentCurriculumMileMapActivityDto
{
    /// <summary>Static catalog information about the activity.</summary>
    public EnrollmentCurriculumMileMapActivityInfoDto ActivityInfo { get; set; } = null!;

    /// <summary>The student's learning state for this activity.</summary>
    public EnrollmentCurriculumMileMapActivityLearningDto Learning { get; set; } = null!;
}

public class EnrollmentCurriculumMileMapActivityInfoDto
{
    public Guid ActivityId { get; set; }

    public string ActivityName { get; set; } = null!;

    public int ActivityOrder { get; set; }

    public ActivityType ActivityType { get; set; }

    public EnrollmentCurriculumMaterialDto? Material { get; set; }
}

public class EnrollmentCurriculumMileMapActivityLearningDto
{
    /// <summary>Nav state: completed, current, in_progress, available, or locked.</summary>
    public string Status { get; set; } = null!;

    public ActivityResumeStateDto? ResumeState { get; set; }

    public DateTime? LastAccessedAt { get; set; }
}
