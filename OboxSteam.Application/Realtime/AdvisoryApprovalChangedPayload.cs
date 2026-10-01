namespace OboxSteam.Application.Realtime;

/// <summary>Payload of <see cref="SyncScopes.AdvisoryApprovalChanged"/>.</summary>
public sealed record AdvisoryApprovalChangedPayload
{
    /// <summary>Program status name after the change, e.g. <c>Approved</c>.</summary>
    public string Status { get; init; } = null!;

    public long CurriculumVersion { get; init; }
}
