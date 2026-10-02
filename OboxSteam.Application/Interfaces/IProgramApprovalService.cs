using OboxSteam.Application.Commons;
using OboxSteam.Application.DTOs.ProgramAdvisoryDTO;
using OboxSteam.Application.DTOs.ProgramDTO;
using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.Interfaces;

/// <summary>Advisory workspace summary and the versioned approval lifecycle of a program.</summary>
public interface IProgramApprovalService
{
    Task<ProgramAdvisoryWorkspaceDto> GetWorkspaceAsync(Guid programId);

    Task<Pagination<AdvisoryMineItemDto>> GetAdvisoryMineAsync(
        int page,
        int pageSize,
        ProgramStatus? status = null,
        bool unreadOnly = false);

    Task<FrameworkCheckDto> GetFrameworkCheckAsync(Guid programId);

    Task<ProgramAdvisoryWorkspaceDto> RequestApprovalAsync(Guid programId);

    Task<ProgramAdvisoryWorkspaceDto> ApproveAsync(Guid programId, ApproveProgramRequest request);

    Task<ProgramAdvisoryWorkspaceDto> RevokeAsync(Guid programId, RevokeProgramApprovalRequest? request);

    Task<ProgramsResponseDto> PublishAsync(Guid programId);

    Task<ProgramsResponseDto> AssignAdvisorAsync(Guid programId, AssignProgramAdvisorRequest request);

    /// <summary>
    /// Moves the program to a newer published version of its framework. Treated like a curriculum
    /// edit: the approval is revoked and Approved/Active/Inactive programs return to Draft.
    /// </summary>
    Task<ProgramAdvisoryWorkspaceDto> UpgradeFrameworkVersionAsync(
        Guid programId,
        UpgradeProgramFrameworkVersionRequest request);
}
