using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.DTOs.ClassCurriculumProgressDTO;

/// <summary>
/// Class-scoped curriculum rollup for the assigned mentor.
/// Counts are over active class enrollments; missing progress is treated as not started.
/// Modules/activities/assignments are always returned (zeros when no progress) for a stable shape.
/// Activity <c>Status</c> is class nav (hybrid Live/Offline session vs SelfPaced roster / moved-past),
/// not student lock semantics — mentors may open any node.
/// </summary>
public sealed class ClassCurriculumProgressDto
{
    public Guid ClassId { get; set; }

    /// <summary>Active class enrollments (denominator for rates on the FE).</summary>
    public int TotalStudents { get; set; }

    /// <summary>Single class cursor activity with nav status <c>current</c>; null when none.</summary>
    public Guid? CurrentActivityId { get; set; }

    public List<ClassCurriculumModuleProgressDto> Modules { get; set; } = [];
}

public sealed class ClassCurriculumModuleProgressDto
{
    public Guid ModuleId { get; set; }

    public List<ClassCurriculumActivityProgressDto> Activities { get; set; } = [];

    public List<ClassCurriculumAssignmentProgressDto> Assignments { get; set; } = [];
}

public sealed class ClassCurriculumActivityProgressDto
{
    public Guid ActivityId { get; set; }

    /// <summary>
    /// Class nav status: <c>completed</c>, <c>current</c>, or <c>available</c>.
    /// </summary>
    public string Status { get; set; } = null!;

    /// <summary>Primary class session used for LiveOnline/Offline status; null for SelfPaced or when unscheduled.</summary>
    public Guid? ClassSessionId { get; set; }

    /// <summary>Status of <see cref="ClassSessionId"/>; null when no primary session.</summary>
    public ClassSessionStatus? SessionStatus { get; set; }

    /// <summary>Active students with <c>ActivityStatus.Done</c> on the latest module attempt.</summary>
    public int CompletedCount { get; set; }

    /// <summary>Active students with <c>ActivityStatus.InProgress</c> on the latest module attempt.</summary>
    public int InProgressCount { get; set; }
}

public sealed class ClassCurriculumAssignmentProgressDto
{
    public Guid AssignmentId { get; set; }

    /// <summary>
    /// Class nav status: <c>completed</c> (all active students graded),
    /// <c>submitted</c> (handed-in work awaiting grade), or <c>available</c>.
    /// </summary>
    public string Status { get; set; } = null!;

    /// <summary>
    /// Distinct active students with a handed-in submission
    /// (<c>TurnedIn</c>, <c>Graded</c>, or <c>ReturnedForRevision</c>).
    /// </summary>
    public int SubmittedCount { get; set; }

    /// <summary>Distinct active students with <c>SubmissionStatus.Graded</c>.</summary>
    public int GradedCount { get; set; }

    /// <summary>Mean of graded <c>AssignedGrade</c> values; null until at least one graded submission.</summary>
    public double? AverageScore { get; set; }
}
