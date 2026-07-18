using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.DTOs.EnrollmentDTO;

public class EnrollmentCurriculumMindMapDto
{
    public Guid EnrollmentId { get; set; }

    public Guid ProgramId { get; set; }

    public string ProgramName { get; set; } = null!;

    public decimal ProgressPercent { get; set; }

    public List<EnrollmentCurriculumMindMapModuleDto> Modules { get; set; } = [];
}

public class EnrollmentCurriculumMindMapModuleDto
{
    /// <summary>Static catalog information about the module.</summary>
    public EnrollmentCurriculumMindMapModuleInfoDto ModuleInfo { get; set; } = null!;

    /// <summary>The student's learning state for this module.</summary>
    public EnrollmentCurriculumMindMapModuleLearningDto Learning { get; set; } = null!;

    public List<EnrollmentCurriculumMindMapCourseDto> Courses { get; set; } = [];

    public List<EnrollmentCurriculumMindMapMilestoneDto> Milestones { get; set; } = [];
}

public class EnrollmentCurriculumMindMapModuleInfoDto
{
    public Guid ModuleId { get; set; }

    public string ModuleName { get; set; } = null!;

    public int ModuleOrder { get; set; }

    public ModuleType ModuleType { get; set; }

    public Guid? PrerequisiteModuleId { get; set; }

    public Guid? ModuleEnrollmentId { get; set; }
}

public class EnrollmentCurriculumMindMapModuleLearningDto
{
    /// <summary>Module state: available, in_progress, completed, or locked.</summary>
    public string Status { get; set; } = null!;

    public decimal ProgressPercent { get; set; }

    public bool IsLocked { get; set; }

    public string? LockReason { get; set; }
}

public class EnrollmentCurriculumMindMapCourseDto
{
    public Guid CourseId { get; set; }

    public string CourseName { get; set; } = null!;

    public int CourseOrder { get; set; }

    public List<EnrollmentCurriculumMindMapActivityDto> Activities { get; set; } = [];
}

public class EnrollmentCurriculumMindMapMilestoneDto
{
    public Guid MilestoneId { get; set; }

    public string MilestoneName { get; set; } = null!;

    public int MilestoneOrder { get; set; }

    public bool IsCapstone { get; set; }

    public List<EnrollmentCurriculumMindMapActivityDto> Activities { get; set; } = [];
}

public class EnrollmentCurriculumMindMapActivityDto
{
    /// <summary>Static catalog information about the activity.</summary>
    public EnrollmentCurriculumMindMapActivityInfoDto ActivityInfo { get; set; } = null!;

    /// <summary>The student's learning state for this activity.</summary>
    public EnrollmentCurriculumMindMapActivityLearningDto Learning { get; set; } = null!;
}

public class EnrollmentCurriculumMindMapActivityInfoDto
{
    public Guid ActivityId { get; set; }

    public string ActivityName { get; set; } = null!;

    public int ActivityOrder { get; set; }

    public ActivityType ActivityType { get; set; }

    public EnrollmentCurriculumMaterialDto? Material { get; set; }
}

public class EnrollmentCurriculumMindMapActivityLearningDto
{
    /// <summary>Nav state: completed, current, in_progress, available, or locked.</summary>
    public string Status { get; set; } = null!;

    public ActivityResumeStateDto? ResumeState { get; set; }

    public DateTime? LastAccessedAt { get; set; }
}
