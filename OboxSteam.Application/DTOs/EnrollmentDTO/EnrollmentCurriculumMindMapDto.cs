using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.DTOs.EnrollmentDTO;

public class EnrollmentCurriculumMindMapDto
{
    public Guid EnrollmentId { get; set; }

    /// <summary>Center hub node (program) with overall progress.</summary>
    public EnrollmentCurriculumMindMapHubDto Hub { get; set; } = null!;

    /// <summary>
    /// Paths from hub to each parallel "current" activity (you-are-here).
    /// Empty when the program is fully complete or nothing is unlocked yet.
    /// </summary>
    public List<EnrollmentCurriculumMindMapPathDto> CurrentPaths { get; set; } = [];

    public List<EnrollmentCurriculumMindMapModuleDto> Modules { get; set; } = [];
}

public class EnrollmentCurriculumMindMapHubDto
{
    public Guid ProgramId { get; set; }

    public string ProgramName { get; set; } = null!;

    public decimal ProgressPercent { get; set; }

    /// <summary>Program hub state: available, in_progress, or completed.</summary>
    public string Status { get; set; } = null!;

    public int CompletedModuleCount { get; set; }

    public int TotalModuleCount { get; set; }

    public EnrollmentCurriculumMindMapNavigationDto Navigation { get; set; } = null!;
}

public class EnrollmentCurriculumMindMapPathDto
{
    public List<EnrollmentCurriculumMindMapPathNodeDto> Nodes { get; set; } = [];
}

public class EnrollmentCurriculumMindMapPathNodeDto
{
    /// <summary>program | module | course | milestone | activity | assignment</summary>
    public string NodeType { get; set; } = null!;

    public Guid NodeId { get; set; }
}

public class EnrollmentCurriculumMindMapNavigationDto
{
    /// <summary>program | module | course | milestone | activity | assignment</summary>
    public string TargetType { get; set; } = null!;

    public Guid TargetId { get; set; }

    /// <summary>Present when the target belongs to a module enrollment context.</summary>
    public Guid? ModuleEnrollmentId { get; set; }
}

public class EnrollmentCurriculumMindMapChildProgressDto
{
    public int TotalCount { get; set; }

    public int CompletedCount { get; set; }

    public decimal ProgressPercent { get; set; }
}

public class EnrollmentCurriculumMindMapModuleDto
{
    public EnrollmentCurriculumMindMapModuleInfoDto ModuleInfo { get; set; } = null!;

    public EnrollmentCurriculumMindMapModuleLearningDto Learning { get; set; } = null!;

    public EnrollmentCurriculumMindMapChildProgressDto ChildProgress { get; set; } = null!;

    public EnrollmentCurriculumMindMapNavigationDto Navigation { get; set; } = null!;

    public List<EnrollmentCurriculumMindMapCourseDto> Courses { get; set; } = [];

    public List<EnrollmentCurriculumMindMapMilestoneDto> Milestones { get; set; } = [];

    /// <summary>Module-scoped assignments (not tied to a specific course).</summary>
    public List<EnrollmentCurriculumMindMapAssignmentDto> Assignments { get; set; } = [];
}

public class EnrollmentCurriculumMindMapModuleInfoDto
{
    public Guid ModuleId { get; set; }

    public string ModuleName { get; set; } = null!;

    public string ModuleCode { get; set; } = null!;

    public int ModuleOrder { get; set; }

    public ModuleType ModuleType { get; set; }

    public Guid? PrerequisiteModuleId { get; set; }

    public Guid? ModuleEnrollmentId { get; set; }

    public bool IsMandatory { get; set; }

    public string[] LearningOutcomes { get; set; } = [];
}

public class EnrollmentCurriculumMindMapModuleLearningDto
{
    /// <summary>Module state: available, in_progress, current, completed, or locked.</summary>
    public string Status { get; set; } = null!;

    public decimal ProgressPercent { get; set; }

    public bool IsLocked { get; set; }

    public string? LockReason { get; set; }
}

public class EnrollmentCurriculumMindMapCourseDto
{
    public EnrollmentCurriculumMindMapCourseInfoDto CourseInfo { get; set; } = null!;

    public EnrollmentCurriculumMindMapContainerLearningDto Learning { get; set; } = null!;

    public EnrollmentCurriculumMindMapChildProgressDto ChildProgress { get; set; } = null!;

    public EnrollmentCurriculumMindMapNavigationDto Navigation { get; set; } = null!;

    public List<EnrollmentCurriculumMindMapActivityDto> Activities { get; set; } = [];

    public List<EnrollmentCurriculumMindMapAssignmentDto> Assignments { get; set; } = [];
}

public class EnrollmentCurriculumMindMapCourseInfoDto
{
    public Guid CourseId { get; set; }

    public string CourseName { get; set; } = null!;

    public int CourseOrder { get; set; }
}

public class EnrollmentCurriculumMindMapMilestoneDto
{
    public EnrollmentCurriculumMindMapMilestoneInfoDto MilestoneInfo { get; set; } = null!;

    public EnrollmentCurriculumMindMapContainerLearningDto Learning { get; set; } = null!;

    public EnrollmentCurriculumMindMapChildProgressDto ChildProgress { get; set; } = null!;

    public EnrollmentCurriculumMindMapNavigationDto Navigation { get; set; } = null!;

    public List<EnrollmentCurriculumMindMapActivityDto> Activities { get; set; } = [];

    /// <summary>The graded deliverable for this milestone.</summary>
    public EnrollmentCurriculumMindMapAssignmentDto? Assignment { get; set; }
}

public class EnrollmentCurriculumMindMapMilestoneInfoDto
{
    public Guid MilestoneId { get; set; }

    public string MilestoneName { get; set; } = null!;

    public int MilestoneOrder { get; set; }

    public bool IsCapstone { get; set; }
}

public class EnrollmentCurriculumMindMapContainerLearningDto
{
    /// <summary>Container state: available, in_progress, current, completed, or locked.</summary>
    public string Status { get; set; } = null!;

    public decimal ProgressPercent { get; set; }

    public bool IsLocked { get; set; }

    public string? LockReason { get; set; }
}

public class EnrollmentCurriculumMindMapActivityDto
{
    public EnrollmentCurriculumMindMapActivityInfoDto ActivityInfo { get; set; } = null!;

    public EnrollmentCurriculumMindMapActivityLearningDto Learning { get; set; } = null!;

    public EnrollmentCurriculumMindMapNavigationDto Navigation { get; set; } = null!;
}

public class EnrollmentCurriculumMindMapActivityInfoDto
{
    public Guid ActivityId { get; set; }

    public string ActivityName { get; set; } = null!;

    public string ActivityCode { get; set; } = null!;

    public int ActivityOrder { get; set; }

    public ActivityType ActivityType { get; set; }

    public string? Description { get; set; }

    public EnrollmentCurriculumMaterialDto? Material { get; set; }
}

public class EnrollmentCurriculumMindMapActivityLearningDto
{
    /// <summary>Nav state: completed, current, in_progress, available, or locked.</summary>
    public string Status { get; set; } = null!;

    public bool IsLocked { get; set; }

    public string? LockReason { get; set; }

    public ActivityResumeStateDto? ResumeState { get; set; }

    public DateTime? LastAccessedAt { get; set; }
}

public class EnrollmentCurriculumMindMapAssignmentDto
{
    public EnrollmentCurriculumMindMapAssignmentInfoDto AssignmentInfo { get; set; } = null!;

    public EnrollmentCurriculumMindMapAssignmentLearningDto Learning { get; set; } = null!;

    public EnrollmentCurriculumMindMapNavigationDto Navigation { get; set; } = null!;
}

public class EnrollmentCurriculumMindMapAssignmentInfoDto
{
    public Guid AssignmentId { get; set; }

    public string AssignmentCode { get; set; } = null!;

    public string Title { get; set; } = null!;

    public AssignmentType AssignmentType { get; set; }

    public int MaxPoints { get; set; }

    public decimal PassScore { get; set; }

    public bool IsRequiredForModulePass { get; set; }

    public DateTime? DueDate { get; set; }

    public DateTime? AvailableFrom { get; set; }
}

public class EnrollmentCurriculumMindMapAssignmentLearningDto
{
    /// <summary>Nav state: completed, submitted, available, or locked.</summary>
    public string Status { get; set; } = null!;

    public bool IsLocked { get; set; }

    public string? LockReason { get; set; }

    /// <summary>
    /// Latest attempt for this assignment under the student's module enrollment.
    /// Used by FE to open quiz/retrospective result routes.
    /// </summary>
    public Guid? LatestSubmissionId { get; set; }
}
