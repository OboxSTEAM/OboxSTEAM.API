using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.DTOs.ParentProgressionDTO;

public sealed class ParentChildProgressionDto
{
    public ParentLinkedStudentDto Student { get; set; } = null!;

    public ParentProgressionSummaryDto Summary { get; set; } = null!;

    public List<ParentEnrollmentBriefDto> Enrollments { get; set; } = [];

    public List<ParentProgressEventDto> RecentMilestones { get; set; } = [];
}

public sealed class ParentLinkedStudentDto
{
    public Guid LinkedUserId { get; set; }

    public string? Code { get; set; }

    public string? FullName { get; set; }

    public string Email { get; set; } = null!;

    public string? Phone { get; set; }

    public string? AvatarUrl { get; set; }

    public bool IsVerified { get; set; }

    public DateTime LinkedAt { get; set; }
}

public sealed class ParentProgressionSummaryDto
{
    public int ActiveEnrollmentCount { get; set; }

    public int CompletedEnrollmentCount { get; set; }

    public DateTime? LastAccessedAt { get; set; }
}

public sealed class ParentEnrollmentBriefDto
{
    public Guid EnrollmentId { get; set; }

    public Guid ProgramId { get; set; }

    public string? ProgramName { get; set; }

    public string? ProgramCode { get; set; }

    public string? ThumbnailUrl { get; set; }

    public DifficultyLevel? Level { get; set; }

    public EnrollmentStatus Status { get; set; }

    public decimal ProgressPercent { get; set; }

    public DateTime? EnrolledAt { get; set; }

    public DateTime? StartedAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    public ParentCurrentModuleDto? CurrentModule { get; set; }

    public ParentCurrentActivityDto? CurrentActivity { get; set; }

    public DateTime? LastAccessedAt { get; set; }

    public List<ParentBlockerDto> Blockers { get; set; } = [];
}

public sealed class ParentCurrentModuleDto
{
    public Guid ModuleId { get; set; }

    public Guid? ModuleEnrollmentId { get; set; }

    public string? ModuleName { get; set; }

    public int ModuleOrder { get; set; }

    public ModuleType ModuleType { get; set; }

    public EnrollmentStatus? Status { get; set; }

    public decimal? ProgressPercent { get; set; }
}

public sealed class ParentCurrentActivityDto
{
    public Guid ActivityId { get; set; }

    public string? ActivityName { get; set; }

    public ActivityType ActivityType { get; set; }
}

public sealed class ParentBlockerDto
{
    public ParentBlockerCode Code { get; set; }

    public string Message { get; set; } = null!;

    public Guid? ModuleId { get; set; }

    public Guid? EnrollmentId { get; set; }
}

public sealed class ParentProgressEventDto
{
    public string Id { get; set; } = null!;

    public DateTime OccurredAt { get; set; }

    public ParentProgressEventType Type { get; set; }

    public string Title { get; set; } = null!;

    public string? Subtitle { get; set; }

    public Guid? EnrollmentId { get; set; }

    public Guid? ModuleId { get; set; }
}
