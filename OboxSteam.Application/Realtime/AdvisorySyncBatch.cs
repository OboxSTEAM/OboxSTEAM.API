using OboxSteam.Application.Interfaces;
using OboxSteam.Application.Notifications;
using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.Realtime;

/// <summary>
/// Collects the advisory sync events of one program while its data changes, so they can be
/// published to the <c>advisory:{programId}</c> group only after the transaction commits.
/// Repeated discussion, approval, and structure events collapse into one event each.
/// </summary>
public sealed class AdvisorySyncBatch
{
    private const string EntityType = "Program";

    private readonly List<(string Scope, object Payload)> _events = [];

    public AdvisorySyncBatch(Guid programId)
    {
        ProgramId = programId;
    }

    public Guid ProgramId { get; }

    public bool IsEmpty => _events.Count == 0;

    /// <summary>Drops events of a previous attempt; the execution strategy may re-run a transaction.</summary>
    public void Reset() => _events.Clear();

    public void DiscussionChanged(long latestSequence, Guid? messageId = null)
    {
        var index = IndexOf(SyncScopes.AdvisoryDiscussionChanged);
        if (index < 0)
        {
            _events.Add((SyncScopes.AdvisoryDiscussionChanged, new AdvisoryDiscussionChangedPayload
            {
                LatestSequence = latestSequence,
                MessageId = messageId,
            }));
            return;
        }

        var existing = (AdvisoryDiscussionChangedPayload)_events[index].Payload;
        _events[index] = (SyncScopes.AdvisoryDiscussionChanged, new AdvisoryDiscussionChangedPayload
        {
            LatestSequence = Math.Max(existing.LatestSequence, latestSequence),
            MessageId = existing.MessageId == messageId ? messageId : null,
        });
    }

    public void PinChanged(Guid messageId)
    {
        var duplicate = _events.Any(e => e.Scope == SyncScopes.AdvisoryPinChanged
                                         && ((AdvisoryPinChangedPayload)e.Payload).MessageId == messageId);
        if (!duplicate)
        {
            _events.Add((SyncScopes.AdvisoryPinChanged, new AdvisoryPinChangedPayload { MessageId = messageId }));
        }
    }

    public void ApprovalChanged(ProgramStatus status, long curriculumVersion)
        => Replace(SyncScopes.AdvisoryApprovalChanged, new AdvisoryApprovalChangedPayload
        {
            Status = status.ToString(),
            CurriculumVersion = curriculumVersion,
        });

    public void StructureChanged(long curriculumVersion)
    {
        var index = IndexOf(SyncScopes.CurriculumStructureChanged);
        var version = index < 0
            ? curriculumVersion
            : Math.Max(((CurriculumStructureChangedPayload)_events[index].Payload).CurriculumVersion, curriculumVersion);
        Replace(SyncScopes.CurriculumStructureChanged, new CurriculumStructureChangedPayload { CurriculumVersion = version });
    }

    public async Task PublishAsync(ISyncEventPublisher publisher)
    {
        ArgumentNullException.ThrowIfNull(publisher);
        var events = _events.ToList();
        _events.Clear();
        var audience = NotificationAudience.ForAdvisoryParticipants(ProgramId);
        foreach (var (scope, payload) in events)
        {
            await publisher.PublishAsync(scope, audience, EntityType, ProgramId, payload);
        }
    }

    private int IndexOf(string scope) => _events.FindIndex(e => e.Scope == scope);

    private void Replace(string scope, object payload)
    {
        var index = IndexOf(scope);
        if (index < 0)
        {
            _events.Add((scope, payload));
            return;
        }

        _events[index] = (scope, payload);
    }
}
