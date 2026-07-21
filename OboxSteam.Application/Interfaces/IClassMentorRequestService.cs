using OboxSteam.Application.Commons;
using OboxSteam.Application.DTOs.ClassMentorRequestDTO;
using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.Interfaces;

public interface IClassMentorRequestService
{
    Task<Pagination<ClassMentorBoardItemDto>> GetMentorBoardAsync(
        string? search,
        string? sortBy,
        bool isDescending,
        int page,
        int pageSize,
        Guid? programId = null,
        bool matchMySkills = false);

    Task<ClassMentorRequestResponseDto> CreateRequestAsync(CreateClassMentorRequestDto request);

    Task WithdrawRequestAsync(Guid requestId);

    Task<Pagination<ClassMentorRequestResponseDto>> GetMyRequestsAsync(
        ClassMentorRequestStatus? status,
        int page,
        int pageSize);

    Task<Pagination<ClassMentorRequestResponseDto>> GetRequestsForManagerAsync(
        Guid? classId,
        Guid? mentorId,
        ClassMentorRequestStatus? status,
        int page,
        int pageSize);

    Task<ClassMentorRequestResponseDto> ApproveRequestAsync(Guid requestId, DecideClassMentorRequestDto? request);

    Task<ClassMentorRequestResponseDto> RejectRequestAsync(Guid requestId, DecideClassMentorRequestDto? request);
}
