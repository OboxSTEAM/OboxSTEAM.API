using OboxSteam.Application.DTOs.ClassRedeliveryDTO;

namespace OboxSteam.Application.Interfaces;

public interface IClassRedeliveryRequestService
{
    Task<ClassRedeliveryRequestResponseDto> CreateAsync(CreateClassRedeliveryRequestDto request);

    Task<ClassRedeliveryRequestResponseDto> WithdrawAsync(Guid requestId);

    Task<ClassRedeliveryRequestResponseDto> ManagerAssignTargetAsync(
        Guid requestId,
        DecideClassRedeliveryRequestDto dto);

    Task<ClassRedeliveryRequestResponseDto> RejectAsync(Guid requestId, DecideClassRedeliveryRequestDto? dto);

    Task<List<ClassRedeliveryRequestResponseDto>> GetMineAsync();

    Task<List<ClassRedeliveryRequestResponseDto>> GetPendingManagerAsync();

    /// <summary>Called after retake payment succeeds to transfer the student and complete the request.</summary>
    Task CompleteAfterPaymentAsync(Guid paymentId);
}
