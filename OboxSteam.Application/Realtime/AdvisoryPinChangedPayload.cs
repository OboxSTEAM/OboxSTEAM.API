namespace OboxSteam.Application.Realtime;

/// <summary>Payload of <see cref="SyncScopes.AdvisoryPinChanged"/>.</summary>
public sealed record AdvisoryPinChangedPayload
{
    public Guid MessageId { get; init; }
}
