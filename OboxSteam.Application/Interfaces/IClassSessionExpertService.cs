using OboxSteam.Application.Commons;
using OboxSteam.Application.DTOs.ClassSessionExpertDTO;
using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.Interfaces;

public interface IClassSessionExpertService
{
    Task<ClassSessionExpertResponseDto> InviteAsync(InviteClassSessionExpertDto request);

    Task<ClassSessionExpertResponseDto> GetByIdAsync(Guid id);

    Task<Pagination<ClassSessionExpertResponseDto>> GetMineAsync(
        ClassSessionExpertStatus? status,
        int page,
        int pageSize);

    Task<Pagination<ClassSessionExpertResponseDto>> GetForManagerAsync(
        Guid? classId,
        Guid? sessionId,
        Guid? expertId,
        ClassSessionExpertStatus? status,
        int page,
        int pageSize);

    Task<ClassSessionExpertResponseDto> AcceptAsync(Guid id);

    Task<ClassSessionExpertResponseDto> DeclineAsync(Guid id);

    Task WithdrawAsync(Guid id);

    Task<ClassSessionExpertResponseDto> ApproveRescheduleAsync(Guid id);

    Task<ClassSessionExpertResponseDto> DeclineRescheduleAsync(Guid id);
}
