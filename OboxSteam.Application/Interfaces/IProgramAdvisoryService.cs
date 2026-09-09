using OboxSteam.Application.Commons;
using OboxSteam.Application.DTOs.ProgramAdvisoryDTO;
using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.Interfaces;

public interface IProgramAdvisoryService
{
    Task<Pagination<AdvisoryMineItemDto>> GetAdvisoryMineAsync(
        int page,
        int pageSize,
        ProgramStatus? status = null,
        bool unreadOnly = false);

    Task<ProgramAdvisoryWorkspaceDto> GetAdvisoryWorkspaceAsync(Guid programId);

    Task<IReadOnlyList<AdvisoryThreadDto>> GetThreadsAsync(Guid programId);

    Task<AdvisoryThreadDto> CreateThreadAsync(Guid programId, CreateAdvisoryThreadRequest request);

    Task<IReadOnlyList<AdvisoryMessageDto>> GetMessagesAsync(Guid programId, Guid threadId);

    Task<AdvisoryMessageDto> AddMessageAsync(Guid programId, Guid threadId, string message);

    Task<AdvisoryThreadDto> UpdateThreadStatusAsync(
        Guid programId,
        Guid threadId,
        UpdateAdvisoryThreadStatusRequest request);

    Task RecordReadAsync(Guid programId, RecordAdvisoryReadRequest? request);
}
