namespace OboxSteam.Application.Realtime;

/// <summary>Payload of <see cref="SyncScopes.SubmissionTurnedIn"/> sent to the class mentor.</summary>
public sealed record SubmissionTurnedInPayload
{
    public Guid AssignmentId { get; init; }

    public Guid StudentId { get; init; }

    public Guid ClassId { get; init; }

    /// <summary><see cref="Domain.Enums.SubmissionStatus"/> name.</summary>
    public string Status { get; init; } = null!;
}
