using OboxSteam.Application.DTOs.ClassDTO;
using OboxSteam.Application.DTOs.ClassRedeliveryDTO;

namespace OboxSteam.Application.Interfaces;

public interface IClassRedeliveryRequestService
{
    Task<ClassRedeliveryRequestResponseDto> CreateAsync(CreateClassRedeliveryRequestDto request);

    /// <summary>
    /// Continuity catalog for the request's module: the same shape as rebuy, with
    /// per-class eligibility the student picks from.
    /// </summary>
    Task<RebuyClassCatalogDto> GetCandidatesAsync(Guid requestId);

    /// <summary>The student picks one eligible Standard class from the continuity catalog.</summary>
    Task<ClassRedeliveryRequestResponseDto> SelectClassAsync(Guid requestId, Guid classId);

    Task<ClassRedeliveryRequestResponseDto> WithdrawAsync(Guid requestId);

    /// <summary>Gone: the manager waitlist tier was removed.</summary>
    Task<ClassRedeliveryRequestResponseDto> ManagerAssignTargetAsync(
        Guid requestId,
        DecideClassRedeliveryRequestDto dto);

    /// <summary>Gone: the manager waitlist tier was removed.</summary>
    Task<ClassRedeliveryRequestResponseDto> RejectAsync(Guid requestId, DecideClassRedeliveryRequestDto? dto);

    Task<List<ClassRedeliveryRequestResponseDto>> GetMineAsync();

    /// <summary>Gone: the manager waitlist tier was removed.</summary>
    Task<List<ClassRedeliveryRequestResponseDto>> GetPendingManagerAsync();

    /// <summary>Gone: the manager waitlist tier was removed.</summary>
    Task<List<RedeliveryWaitlistProgramGroupDto>> GetWaitlistGroupedAsync();

    /// <summary>Gone: intensive Remedial classes are no longer opened for redelivery.</summary>
    Task<OpenRemedialClassResponseDto> OpenRemedialClassAsync(OpenRemedialClassRequestDto dto);

    /// <summary>Gone: intensive Remedial classes are no longer offered.</summary>
    Task<ClassRedeliveryRequestResponseDto> AcceptIntensiveAsync(Guid requestId);

    /// <summary>Gone: intensive Remedial classes are no longer offered.</summary>
    Task<ClassRedeliveryRequestResponseDto> DeclineIntensiveAsync(Guid requestId);

    /// <summary>
    /// No-op: continuity has no waitlist, so a newly opened class needs no fan-out.
    /// Kept so class management keeps compiling against a stable hook.
    /// </summary>
    Task NotifyPendingManagerForNewClassAsync(Guid classId);

    /// <summary>Called after retake payment succeeds to place the student and complete the request.</summary>
    Task CompleteAfterPaymentAsync(Guid paymentId);
}
