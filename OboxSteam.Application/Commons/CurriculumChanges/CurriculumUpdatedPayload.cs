namespace OboxSteam.Application.Commons.CurriculumChanges;

/// <summary>Payload of the <c>CurriculumUpdated</c> discussion system message.</summary>
public sealed class CurriculumUpdatedPayload
{
    public Guid? ActorUserId { get; set; }
    public string? ActorName { get; set; }
    public long FromVersion { get; set; }
    public long ToVersion { get; set; }
    public int ChangeCount { get; set; }
}
