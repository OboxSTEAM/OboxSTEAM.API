using OboxSteam.Application.Commons;
using OboxSteam.Application.DTOs.CurriculumReviewDTO;
using OboxSteam.Application.DTOs.ProgramAdvisoryDTO;
using OboxSteam.Application.DTOs.ProgramDTO;
using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.Interfaces;

public interface ICurriculumReviewService
{
    Task<ProgramsResponseDto> SubmitForReviewAsync(Guid programId);

    Task<ProgramsResponseDto> WithdrawReviewAsync(Guid programId);

    Task<ProgramsResponseDto> PublishAsync(Guid programId);

    Task<Pagination<ProgramReviewQueueItemDto>> GetReviewQueueAsync(int page, int pageSize);

    Task<IReadOnlyList<CurriculumReviewResponseDto>> GetReviewsAsync(Guid programId);

    Task<CurriculumReviewResponseDto> ApproveAsync(Guid programId, ApproveCurriculumReviewRequest? request);

    Task<CurriculumReviewResponseDto> RequestChangesAsync(Guid programId, RequestCurriculumChangesRequest request);

    Task<FrameworkCheckDto> GetFrameworkCheckAsync(Guid programId);

    Task<IReadOnlyList<ProgramReviewSubmissionSummaryDto>> GetSubmissionsAsync(Guid programId);

    Task<ProgramReviewSubmissionDetailDto> GetSubmissionAsync(Guid programId, Guid submissionId);

    Task<SubmissionChangesDto> GetSubmissionChangesAsync(Guid programId, Guid submissionId);

    Task<ProgramReviewDraftDto> GetDraftAsync(Guid programId, Guid submissionId);

    Task<ProgramReviewDraftDto> SaveDraftAsync(Guid programId, Guid submissionId, SaveProgramReviewDraftRequest request);
}
