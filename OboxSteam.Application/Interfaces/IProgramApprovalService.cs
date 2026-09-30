using OboxSteam.Application.DTOs.ProgramAdvisoryDTO;
using OboxSteam.Application.DTOs.ProgramDTO;

namespace OboxSteam.Application.Interfaces;

/// <summary>Advisory workspace summary and the versioned approval lifecycle of a program.</summary>
public interface IProgramApprovalService
{
    Task<ProgramAdvisoryWorkspaceDto> GetWorkspaceAsync(Guid programId);

    Task<ProgramAdvisoryWorkspaceDto> RequestApprovalAsync(Guid programId);

    Task<ProgramAdvisoryWorkspaceDto> ApproveAsync(Guid programId, ApproveProgramRequest request);

    Task<ProgramAdvisoryWorkspaceDto> RevokeAsync(Guid programId, RevokeProgramApprovalRequest? request);

    Task<ProgramsResponseDto> PublishAsync(Guid programId);

    Task<ProgramsResponseDto> AssignAdvisorAsync(Guid programId, AssignProgramAdvisorRequest request);
}
