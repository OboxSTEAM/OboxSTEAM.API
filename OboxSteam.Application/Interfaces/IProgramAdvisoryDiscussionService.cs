using OboxSteam.Application.DTOs.ProgramAdvisoryDTO;

namespace OboxSteam.Application.Interfaces;

public interface IProgramAdvisoryDiscussionService
{
    Task<AdvisoryDiscussionPageDto> GetMessagesAsync(
        Guid programId,
        string? before,
        string? after,
        int pageSize);

    Task<AdvisoryDiscussionMessageDto> GetMessageAsync(Guid programId, Guid messageId);

    Task<AdvisoryDiscussionMessageDto> AddMessageAsync(
        Guid programId,
        PostAdvisoryDiscussionMessageRequest request);

    Task RecordThreadReadAsync(
        Guid programId,
        Guid threadId,
        RecordAdvisoryThreadReadRequest request);

    Task RecordDiscussionReadAsync(
        Guid programId,
        RecordAdvisoryDiscussionReadRequest request);
}
