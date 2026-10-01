namespace OboxSteam.Application.Realtime;

/// <summary>Payload of <see cref="SyncScopes.CurriculumStructureChanged"/> sent to the advisory group.</summary>
public sealed record CurriculumStructureChangedPayload
{
    public long CurriculumVersion { get; init; }
}
