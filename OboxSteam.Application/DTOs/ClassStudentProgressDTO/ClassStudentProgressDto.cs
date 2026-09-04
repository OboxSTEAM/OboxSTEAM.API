using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.DTOs.ClassStudentProgressDTO;

/// <summary>
/// Roster-complete per-student progress for one activity in a class (mentor detail pane).
/// One row per active class enrollment; missing <see cref="ActivityProgress"/> → <c>NotStart</c>.
/// </summary>
public sealed class ClassActivityStudentProgressDto
{
    public Guid ClassId { get; set; }

    public Guid ActivityId { get; set; }

    public ActivityType ActivityType { get; set; }

    /// <summary>Primary class session for LiveOnline/Offline; null for SelfPaced or when unscheduled.</summary>
    public Guid? ClassSessionId { get; set; }

    public ClassSessionStatus? SessionStatus { get; set; }

    public int TotalStudents { get; set; }

    public int CompletedCount { get; set; }

    public int InProgressCount { get; set; }

    public int NotStartedCount { get; set; }

    public List<ClassActivityStudentProgressItemDto> Students { get; set; } = [];
}

public sealed class ClassActivityStudentProgressItemDto
{
    public Guid StudentId { get; set; }

    public string StudentCode { get; set; } = null!;

    public string? StudentName { get; set; }

    public string Email { get; set; } = null!;

    public string? AvatarUrl { get; set; }

    public ActivityStatus ActivityStatus { get; set; }

    public DateTime? CompletedAt { get; set; }

    public DateTime? LastAccessedAt { get; set; }

    public CompletionSource? CompletionSource { get; set; }

    /// <summary>Present for LiveOnline/Offline when a primary session exists; null for SelfPaced.</summary>
    public AttendanceStatus? AttendanceStatus { get; set; }

    public DateTime? CheckedInAt { get; set; }

    public int? ParticipationMinutes { get; set; }
}

/// <summary>
/// Roster-complete per-student progress for one assignment in a class (mentor detail pane).
/// One row per active class enrollment; never-started students have null submission fields.
/// </summary>
public sealed class ClassAssignmentStudentProgressDto
{
    public Guid ClassId { get; set; }

    public Guid AssignmentId { get; set; }

    public AssignmentType AssignmentType { get; set; }

    /// <summary>
    /// Class nav status matching curriculum-progress:
    /// <c>available</c> | <c>submitted</c> | <c>completed</c>.
    /// </summary>
    public string Status { get; set; } = null!;

    public int TotalStudents { get; set; }

    public int SubmittedCount { get; set; }

    public int GradedCount { get; set; }

    public int NotStartedCount { get; set; }

    /// <summary>Mean of graded <c>AssignedGrade</c> values; null until at least one graded submission.</summary>
    public double? AverageScore { get; set; }

    public List<ClassAssignmentStudentProgressItemDto> Students { get; set; } = [];
}

public sealed class ClassAssignmentStudentProgressItemDto
{
    public Guid StudentId { get; set; }

    public string StudentCode { get; set; } = null!;

    public string? StudentName { get; set; }

    public string Email { get; set; } = null!;

    public string? AvatarUrl { get; set; }

    /// <summary>Latest attempt under a class-scoped module enrollment; null if never started.</summary>
    public Guid? SubmissionId { get; set; }

    public int? AttemptNumber { get; set; }

    public SubmissionStatus? SubmissionStatus { get; set; }

    public decimal? AssignedGrade { get; set; }

    /// <summary>Null until graded with a numeric grade.</summary>
    public bool? Passed { get; set; }

    public DateTime? SubmittedAt { get; set; }

    public DateTime? GradedAt { get; set; }
}
