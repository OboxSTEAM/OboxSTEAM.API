namespace OboxSteam.Application.Realtime;

/// <summary>Payload of <see cref="SyncScopes.AdvisoryDiscussionChanged"/>.</summary>
public sealed record AdvisoryDiscussionChangedPayload
{
    /// <summary>Highest message sequence in the program chat after the change.</summary>
    public long LatestSequence { get; init; }

    /// <summary>The edited or removed message; null when new messages were appended.</summary>
    public Guid? MessageId { get; init; }
}
