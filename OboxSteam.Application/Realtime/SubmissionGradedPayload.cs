namespace OboxSteam.Application.Realtime;

/// <summary>Payload of <see cref="SyncScopes.SubmissionGraded"/> sent to the student and parents.</summary>
public sealed record SubmissionGradedPayload
{
    public Guid StudentId { get; init; }

    public Guid AssignmentId { get; init; }

    public Guid ProgramId { get; init; }

    public Guid? ProgramEnrollmentId { get; init; }

    public Guid? ResearchMilestoneId { get; init; }

    /// <summary><see cref="Domain.Enums.SubmissionStatus"/> name: <c>Graded</c> or <c>ReturnedForRevision</c>.</summary>
    public string Status { get; init; } = null!;

    public decimal? AssignedGrade { get; init; }

    public int MaxPoints { get; init; }

    /// <summary>Null unless <see cref="Status"/> is <c>Graded</c>.</summary>
    public bool? Passed { get; init; }
}
