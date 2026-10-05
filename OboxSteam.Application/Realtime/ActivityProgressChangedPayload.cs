namespace OboxSteam.Application.Realtime;

/// <summary>Payload of <see cref="SyncScopes.ActivityProgressChanged"/> sent to the student and parents.</summary>
public sealed record ActivityProgressChangedPayload
{
    public Guid StudentId { get; init; }

    public Guid ProgramId { get; init; }

    public Guid? ProgramEnrollmentId { get; init; }

    public Guid ActivityId { get; init; }

    public Guid? NextActivityId { get; init; }

    /// <summary><see cref="Domain.Enums.ActivityStatus"/> name.</summary>
    public string Status { get; init; } = null!;
}
