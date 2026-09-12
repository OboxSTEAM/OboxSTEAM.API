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

    Task<IReadOnlyList<AdvisoryThreadDto>> GetThreadsAsync(
        Guid programId,
        Guid? submissionId = null,
        ProgramAdvisoryTargetType? targetType = null,
        Guid? targetId = null,
        ProgramAdvisoryThreadStatus? status = null,
        ProgramAdvisoryThreadType? type = null,
        string? scope = null);

    Task<AdvisoryThreadDto> GetThreadAsync(Guid programId, Guid threadId);

    Task<AdvisoryBoardDto> GetBoardAsync(Guid programId, Guid submissionId);

    Task<IReadOnlyList<AdvisoryThreadPinSummaryDto>> GetPinSummariesAsync(
        Guid programId,
        Guid submissionId);

    Task<AdvisoryThreadDto> CreateThreadAsync(Guid programId, CreateAdvisoryThreadRequest request);

    Task<IReadOnlyList<AdvisoryMessageDto>> GetMessagesAsync(Guid programId, Guid threadId);

    Task<AdvisoryMessageDto> AddMessageAsync(Guid programId, Guid threadId, string message);

    Task<AdvisoryThreadDto> UpdateThreadStatusAsync(
        Guid programId,
        Guid threadId,
        UpdateAdvisoryThreadStatusRequest request);

    Task RecordReadAsync(Guid programId, RecordAdvisoryReadRequest? request);

    Task<AdvisoryReferenceDto> CreateReferenceAsync(
        Guid programId,
        CreateAdvisoryReferenceRequest request);

    Task<AdvisoryReferenceDto> GetReferenceAsync(Guid programId, Guid referenceId);

}
