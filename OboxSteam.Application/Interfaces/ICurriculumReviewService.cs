using OboxSteam.Application.Commons;
using OboxSteam.Application.DTOs.CurriculumReviewDTO;
using OboxSteam.Application.DTOs.ProgramDTO;

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
}
