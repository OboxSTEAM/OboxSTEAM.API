using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.DTOs.ParentProgressionDTO;

public sealed class ParentEnrollmentProgressionDto
{
    public Guid StudentId { get; set; }

    public ParentEnrollmentHeaderDto Enrollment { get; set; } = null!;

    public ParentClassInfoDto? ClassInfo { get; set; }

    public List<ParentModuleProgressDto> Modules { get; set; } = [];
}

public sealed class ParentEnrollmentHeaderDto
{
    public Guid EnrollmentId { get; set; }

    public Guid ProgramId { get; set; }

    public string? ProgramName { get; set; }

    public string? ProgramCode { get; set; }

    public string? ThumbnailUrl { get; set; }

    public EnrollmentStatus Status { get; set; }

    public decimal ProgressPercent { get; set; }

    public DateTime? EnrolledAt { get; set; }

    public DateTime? StartedAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    public DateTime? LastAccessedAt { get; set; }
}

public sealed class ParentClassInfoDto
{
    public Guid ClassId { get; set; }

    public string? ClassName { get; set; }

    public string? MentorName { get; set; }
}

public sealed class ParentModuleProgressDto
{
    public Guid ModuleId { get; set; }

    public Guid? ModuleEnrollmentId { get; set; }

    public string? ModuleName { get; set; }

    public int ModuleOrder { get; set; }

    public ModuleType ModuleType { get; set; }

    public bool IsLocked { get; set; }

    public string? LockReason { get; set; }

    public EnrollmentStatus? Status { get; set; }

    public decimal ProgressPercent { get; set; }

    public int? AttemptNumber { get; set; }

    public decimal? FinalGrade { get; set; }

    public ParentModuleOutcomeLabel? OutcomeLabel { get; set; }

    public DateTime? StartedAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    public ParentActivityStatsDto ActivityStats { get; set; } = new();

    public List<ParentAssignmentOutcomeDto> Assignments { get; set; } = [];
}

public sealed class ParentActivityStatsDto
{
    public int Total { get; set; }

    public int Completed { get; set; }
}

public sealed class ParentAssignmentOutcomeDto
{
    public Guid AssignmentId { get; set; }

    public string? Title { get; set; }

    public AssignmentType AssignmentType { get; set; }

    public bool IsRequiredForModulePass { get; set; }

    public DateTime? DueDate { get; set; }

    /// <summary>locked | available | submitted | completed | overdue</summary>
    public string Status { get; set; } = null!;

    public decimal? Score { get; set; }

    public int? MaxPoints { get; set; }

    public decimal? PassScore { get; set; }

    public bool? Passed { get; set; }

    public DateTime? SubmittedAt { get; set; }

    public DateTime? GradedAt { get; set; }

    public int? AttemptUsed { get; set; }

    public int? MaxAttempts { get; set; }
}
