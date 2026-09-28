using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OboxSteam.Application.Commons;
using OboxSteam.Application.DTOs.ProgramReviewDTO;
using OboxSteam.Application.Interfaces;
using OboxSteam.Application.Utils;
using Swashbuckle.AspNetCore.Annotations;

namespace OboxSteam.API.Controllers;

[Route("api/programs/{programId:guid}/reviews")]
[ApiController]
public class ProgramReviewController : ControllerBase
{
    private readonly IProgramReviewService _reviewService;

    public ProgramReviewController(IProgramReviewService reviewService)
    {
        _reviewService = reviewService;
    }

    [HttpPost]
    [Authorize(Roles = "Student")]
    [SwaggerOperation(
        Summary = "Create a program review",
        Description = "Allows a student who completed the program to submit a star rating (1-5) and an optional plain-text comment (trimmed, max 2000 characters, empty stored as null). One active review per student per program. Error codes: 403 REVIEW_NOT_ELIGIBLE (no Completed enrollment), 409 REVIEW_ALREADY_EXISTS (an active review exists), 403 REVIEW_REMOVED_BY_MODERATOR (a moderator removed the previous review), 400 REVIEW_COMMENT_INVALID (HTML or too long).")]
    [ProducesResponseType(typeof(ApiResult<ProgramReviewResponseDto>), 201)]
    [ProducesResponseType(typeof(ApiResult<object>), 400)]
    [ProducesResponseType(typeof(ApiResult<object>), 401)]
    [ProducesResponseType(typeof(ApiResult<object>), 403)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    [ProducesResponseType(typeof(ApiResult<object>), 409)]
    [ProducesResponseType(typeof(ApiResult<object>), 500)]
    public async Task<IActionResult> CreateReview(
        [FromRoute] Guid programId,
        [FromBody, SwaggerParameter("Review data to submit")] CreateProgramReviewDto dto)
    {
        var result = await _reviewService.CreateReviewAsync(programId, dto);

        return CreatedAtAction(
            nameof(GetReviews),
            new { programId },
            ApiResult<ProgramReviewResponseDto>.Success(result, "201", "Review created successfully."));
    }

    [HttpGet]
    [SwaggerOperation(
        Summary = "Get reviews for a program",
        Description = "Retrieve a paginated list of reviews for the specified program. Supports sorting by createdAt (default) or starRating.")]
    [ProducesResponseType(typeof(ApiResult<Pagination<ProgramReviewResponseDto>>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 400)]
    [ProducesResponseType(typeof(ApiResult<object>), 500)]
    public async Task<IActionResult> GetReviews(
        [FromRoute] Guid programId,
        [FromQuery, SwaggerParameter(Description = "Sort by field: createdAt (default) or starRating")] string? sortBy = null,
        [FromQuery, SwaggerParameter(Description = "Sort in descending order? Default: false")] bool isDescending = false,
        [FromQuery, SwaggerParameter(Description = "Page number, starting from 1")] int page = 1,
        [FromQuery, SwaggerParameter(Description = "Number of items per page")] int pageSize = 10)
    {
        if (page < 1 || pageSize < 1)
            return BadRequest(ApiResult<object>.Failure("400", "Invalid pagination parameters."));

        var result = await _reviewService.GetReviewsByProgramAsync(programId, page, pageSize, sortBy, isDescending);

        return Ok(ApiResult<Pagination<ProgramReviewResponseDto>>.Success(result, "200", "Reviews retrieved successfully."));
    }

    [HttpGet("me")]
    [Authorize(Roles = "Student")]
    [SwaggerOperation(
        Summary = "Get my review state for a program",
        Description = "Returns whether the current student can review the program (canReview), why not (reason: NotEnrolled, NotCompleted, AlreadyReviewed, RemovedByModerator, or null), and the student's active review when one exists.")]
    [ProducesResponseType(typeof(ApiResult<MyProgramReviewResponseDto>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 401)]
    [ProducesResponseType(typeof(ApiResult<object>), 403)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    [ProducesResponseType(typeof(ApiResult<object>), 500)]
    public async Task<IActionResult> GetMyReview([FromRoute] Guid programId)
    {
        var result = await _reviewService.GetMyReviewAsync(programId);
        return Ok(ApiResult<MyProgramReviewResponseDto>.Success(result, "200", "Review state retrieved successfully."));
    }

    [HttpPut("{reviewId:guid}")]
    [Authorize(Roles = "Student")]
    [SwaggerOperation(
        Summary = "Update a program review",
        Description = "Allows the review owner to update their star rating or comment. Both fields are optional (partial update). A null comment leaves it unchanged; an empty or whitespace comment clears it. 400 REVIEW_COMMENT_INVALID when the comment contains HTML or is too long.")]
    [ProducesResponseType(typeof(ApiResult<ProgramReviewResponseDto>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 400)]
    [ProducesResponseType(typeof(ApiResult<object>), 401)]
    [ProducesResponseType(typeof(ApiResult<object>), 403)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    [ProducesResponseType(typeof(ApiResult<object>), 500)]
    public async Task<IActionResult> UpdateReview(
        [FromRoute] Guid programId,
        [FromRoute] Guid reviewId,
        [FromBody, SwaggerParameter("Fields to update (all optional)")] UpdateProgramReviewDto dto)
    {
        var result = await _reviewService.UpdateReviewAsync(programId, reviewId, dto);
        return Ok(ApiResult<ProgramReviewResponseDto>.Success(result, "200", "Review updated successfully."));
    }

    [HttpDelete("{reviewId:guid}")]
    [Authorize(Roles = "Student,Admin,Manager")]
    [SwaggerOperation(
        Summary = "Delete a program review",
        Description = "Soft-deletes a review. The review owner, Admin, or Manager may call this endpoint. After an Admin or Manager removes a review, the student cannot create a new one for the program; after the owner deletes it, they may review again.")]
    [ProducesResponseType(typeof(ApiResult<bool>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 401)]
    [ProducesResponseType(typeof(ApiResult<object>), 403)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    [ProducesResponseType(typeof(ApiResult<object>), 500)]
    public async Task<IActionResult> DeleteReview(
        [FromRoute] Guid programId,
        [FromRoute] Guid reviewId)
    {
        var result = await _reviewService.DeleteReviewAsync(programId, reviewId);

        if (!result)
            return NotFound(ApiResult<object>.Failure("404", $"Review with ID '{reviewId}' not found."));

        return Ok(ApiResult<bool>.Success(result, "200", "Review deleted successfully."));
    }
}
