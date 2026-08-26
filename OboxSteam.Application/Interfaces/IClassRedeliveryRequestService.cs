using OboxSteam.Application.DTOs.ClassRedeliveryDTO;

namespace OboxSteam.Application.Interfaces;

public interface IClassRedeliveryRequestService
{
    Task<ClassRedeliveryRequestResponseDto> CreateAsync(CreateClassRedeliveryRequestDto request);

    /// <summary>Eligible Standard cohorts the student may pick for tier-1 re-delivery.</summary>
    Task<List<ClassRedeliveryCandidateDto>> GetCandidatesAsync(Guid requestId);

    /// <summary>Tier 1: the student picks one of the eligible Standard cohorts.</summary>
    Task<ClassRedeliveryRequestResponseDto> SelectClassAsync(Guid requestId, Guid classId);

    Task<ClassRedeliveryRequestResponseDto> WithdrawAsync(Guid requestId);

    Task<ClassRedeliveryRequestResponseDto> ManagerAssignTargetAsync(
        Guid requestId,
        DecideClassRedeliveryRequestDto dto);

    Task<ClassRedeliveryRequestResponseDto> RejectAsync(Guid requestId, DecideClassRedeliveryRequestDto? dto);

    Task<List<ClassRedeliveryRequestResponseDto>> GetMineAsync();

    Task<List<ClassRedeliveryRequestResponseDto>> GetPendingManagerAsync();

    /// <summary>Waitlisted PendingManager requests grouped by program then module.</summary>
    Task<List<RedeliveryWaitlistProgramGroupDto>> GetWaitlistGroupedAsync();

    /// <summary>
    /// Tier 2: a manager opens an intensive Remedial class for one module and offers it to
    /// every waitlisted request of that module.
    /// </summary>
    Task<OpenRemedialClassResponseDto> OpenRemedialClassAsync(OpenRemedialClassRequestDto dto);

    /// <summary>Student accepts the compressed remedial schedule; moves to payment.</summary>
    Task<ClassRedeliveryRequestResponseDto> AcceptIntensiveAsync(Guid requestId);

    /// <summary>Student declines the remedial offer; progress is kept and the request is withdrawn.</summary>
    Task<ClassRedeliveryRequestResponseDto> DeclineIntensiveAsync(Guid requestId);

    /// <summary>
    /// Notifies waitlisted students when a newly opened Standard class would qualify as a
    /// re-delivery candidate. Called by class management after a class opens with a schedule.
    /// </summary>
    Task NotifyPendingManagerForNewClassAsync(Guid classId);

    /// <summary>Called after retake payment succeeds to place the student and complete the request.</summary>
    Task CompleteAfterPaymentAsync(Guid paymentId);
}
