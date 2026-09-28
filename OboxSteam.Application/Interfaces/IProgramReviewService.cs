using OboxSteam.Application.Commons;
using OboxSteam.Application.DTOs.ProgramReviewDTO;

namespace OboxSteam.Application.Interfaces;

public interface IProgramReviewService
{
    /// <summary>
    /// Creates a new review for a program. The caller must have a Completed, non-superseded
    /// enrollment, must not already have an active review, and must not have had their
    /// latest review removed by a moderator.
    /// </summary>
    Task<ProgramReviewResponseDto> CreateReviewAsync(Guid programId, CreateProgramReviewDto dto);

    /// <summary>
    /// Returns whether the current student may review the program, why not, and their active review.
    /// </summary>
    Task<MyProgramReviewResponseDto> GetMyReviewAsync(Guid programId);

    /// <summary>
    /// Returns a paginated list of reviews for the given program.
    /// Publicly accessible. Supports sorting by <c>createdAt</c> (default) or <c>starRating</c>.
    /// </summary>
    Task<Pagination<ProgramReviewResponseDto>> GetReviewsByProgramAsync(
        Guid programId,
        int page,
        int pageSize,
        string? sortBy,
        bool isDescending);

    /// <summary>
    /// Updates an existing review. Only the review owner may call this.
    /// Both fields are optional (partial update).
    /// </summary>
    Task<ProgramReviewResponseDto> UpdateReviewAsync(Guid programId, Guid reviewId, UpdateProgramReviewDto dto);

    /// <summary>
    /// Soft-deletes a review. The owner, Admin, or Manager may call this.
    /// </summary>
    Task<bool> DeleteReviewAsync(Guid programId, Guid reviewId);
}
