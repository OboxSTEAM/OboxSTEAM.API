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
        Description = "Allows an enrolled student to submit a star rating and optional comment for a program they are enrolled in. One review per student per program.")]
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

    [HttpPut("{reviewId:guid}")]
    [Authorize(Roles = "Student")]
    [SwaggerOperation(
        Summary = "Update a program review",
        Description = "Allows the review owner to update their star rating or comment. Both fields are optional (partial update).")]
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
        Description = "Soft-deletes a review. The review owner, Admin, or Manager may call this endpoint.")]
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
