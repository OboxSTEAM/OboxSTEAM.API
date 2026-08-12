using OboxSteam.Application.DTOs.AssessmentRecoveryDTO;

namespace OboxSteam.Application.Interfaces;

public interface IAssessmentRecoveryRequestService
{
    Task<AssessmentRecoveryRequestResponseDto> CreateAsync(CreateAssessmentRecoveryRequestDto request);

    Task<AssessmentRecoveryRequestResponseDto> WithdrawAsync(Guid requestId);

    Task<AssessmentRecoveryRequestResponseDto> ApproveAsync(Guid requestId, DecideAssessmentRecoveryRequestDto dto);

    Task<AssessmentRecoveryRequestResponseDto> RejectAsync(Guid requestId, DecideAssessmentRecoveryRequestDto? dto);

    Task<List<AssessmentRecoveryRequestResponseDto>> GetMineAsync();

    Task<List<AssessmentRecoveryRequestResponseDto>> GetPendingForMentorAsync();
}
