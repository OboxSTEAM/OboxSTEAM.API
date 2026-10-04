namespace OboxSteam.Application.Realtime;

/// <summary>Payload of <see cref="SyncScopes.AttendanceChanged"/> sent to the student.</summary>
public sealed record AttendanceChangedPayload
{
    public Guid StudentId { get; init; }

    /// <summary><see cref="Domain.Enums.AttendanceStatus"/> name.</summary>
    public string Status { get; init; } = null!;
}
