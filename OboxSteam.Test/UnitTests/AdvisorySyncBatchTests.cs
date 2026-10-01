using OboxSteam.Application.Notifications;
using OboxSteam.Application.Realtime;
using OboxSteam.Domain.Enums;
using OboxSteam.Test.Helpers;

namespace OboxSteam.Test.UnitTests;

public sealed class AdvisorySyncBatchTests
{
    private readonly Guid _programId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private readonly FakeSyncEventPublisher _publisher = new();

    [Fact]
    public async Task Publish_CollapsesRepeatedEvents_AndTargetsTheAdvisoryGroup()
    {
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        var batch = new AdvisorySyncBatch(_programId);

        batch.StructureChanged(4);
        batch.StructureChanged(3);
        batch.DiscussionChanged(5);
        batch.DiscussionChanged(7);
        batch.PinChanged(first);
        batch.PinChanged(first);
        batch.PinChanged(second);
        batch.ApprovalChanged(ProgramStatus.Approved, 4);
        batch.ApprovalChanged(ProgramStatus.Draft, 4);
        await batch.PublishAsync(_publisher);

        Assert.Equal(
            [
                (SyncScopes.CurriculumStructureChanged, (object?)new CurriculumStructureChangedPayload { CurriculumVersion = 4 }),
                (SyncScopes.AdvisoryDiscussionChanged, new AdvisoryDiscussionChangedPayload { LatestSequence = 7 }),
                (SyncScopes.AdvisoryPinChanged, new AdvisoryPinChangedPayload { MessageId = first }),
                (SyncScopes.AdvisoryPinChanged, new AdvisoryPinChangedPayload { MessageId = second }),
                (SyncScopes.AdvisoryApprovalChanged, new AdvisoryApprovalChangedPayload { Status = "Draft", CurriculumVersion = 4 }),
            ],
            _publisher.Events.Select(e => (e.Scope, e.Payload)).ToList());
        Assert.All(_publisher.Events, e =>
        {
            Assert.Equal(NotificationAudienceKind.AdvisoryParticipants, e.Audience.Kind);
            Assert.Equal(_programId, e.Audience.ProgramId);
            Assert.Equal("Program", e.EntityType);
            Assert.Equal(_programId, e.EntityId);
        });
        Assert.True(batch.IsEmpty);
    }

    [Fact]
    public async Task DiscussionChanged_KeepsMessageIdOnlyWhenEveryChangeIsTheSameMessage()
    {
        var messageId = Guid.NewGuid();
        var same = new AdvisorySyncBatch(_programId);
        same.DiscussionChanged(3, messageId);
        same.DiscussionChanged(3, messageId);

        var mixed = new AdvisorySyncBatch(_programId);
        mixed.DiscussionChanged(3, messageId);
        mixed.DiscussionChanged(4);

        Assert.Equal(
            new AdvisoryDiscussionChangedPayload { LatestSequence = 3, MessageId = messageId },
            await PayloadAsync(same));
        Assert.Equal(new AdvisoryDiscussionChangedPayload { LatestSequence = 4 }, await PayloadAsync(mixed));
    }

    [Fact]
    public async Task Reset_DropsEventsOfAPreviousAttempt()
    {
        var batch = new AdvisorySyncBatch(_programId);
        batch.DiscussionChanged(1);
        batch.Reset();

        await batch.PublishAsync(_publisher);

        Assert.Empty(_publisher.Events);
    }

    private async Task<object?> PayloadAsync(AdvisorySyncBatch batch)
    {
        _publisher.Events.Clear();
        await batch.PublishAsync(_publisher);
        return Assert.Single(_publisher.Events).Payload;
    }
}
