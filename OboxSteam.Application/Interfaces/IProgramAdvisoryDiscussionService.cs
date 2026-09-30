using OboxSteam.Application.DTOs.ProgramAdvisoryDTO;
using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.Interfaces;

public interface IProgramAdvisoryDiscussionService
{
    Task<IReadOnlyList<MentionTargetDto>> GetMentionTargetsAsync(Guid programId);

    Task<AdvisoryDiscussionPageDto> GetMessagesAsync(
        Guid programId,
        string? before,
        string? after,
        int pageSize,
        ProgramAdvisoryTargetType? targetType = null,
        Guid? targetId = null);

    Task<AdvisoryDiscussionMessageDto> GetMessageAsync(Guid programId, Guid messageId);

    Task<AdvisoryDiscussionMessageDto> AddMessageAsync(
        Guid programId,
        PostAdvisoryDiscussionMessageRequest request);

    Task<AdvisoryDiscussionMessageDto> EditMessageAsync(
        Guid programId,
        Guid messageId,
        EditAdvisoryDiscussionMessageRequest request);

    Task RemoveMessageAsync(Guid programId, Guid messageId);

    Task<AdvisoryDiscussionMessageDto> PinMessageAsync(Guid programId, Guid messageId);

    Task<AdvisoryDiscussionMessageDto> UnpinMessageAsync(Guid programId, Guid messageId);

    Task<AdvisoryDiscussionMessageDto> PerformPinActionAsync(
        Guid programId,
        Guid messageId,
        AdvisoryDiscussionPinActionRequest request);

    Task<IReadOnlyList<AdvisoryDiscussionMessageDto>> GetPinsAsync(
        Guid programId,
        DiscussionPinStatus? status = null);

    Task<IReadOnlyList<AdvisoryMentionCountDto>> GetMentionCountsAsync(Guid programId);

    Task RecordThreadReadAsync(
        Guid programId,
        Guid threadId,
        RecordAdvisoryThreadReadRequest request);

    Task RecordDiscussionReadAsync(
        Guid programId,
        RecordAdvisoryDiscussionReadRequest request);
}
