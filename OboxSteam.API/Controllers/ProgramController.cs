using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using OboxSteam.Application.Commons;
using OboxSteam.Application.DTOs.ClassDTO;
using OboxSteam.Application.DTOs.CurriculumReviewDTO;
using OboxSteam.Application.DTOs.PaymentDTO;
using OboxSteam.Application.DTOs.ProgramDTO;
using OboxSteam.Application.Interfaces;
using OboxSteam.Application.Utils;
using OboxSteam.Domain.Enums;
using Swashbuckle.AspNetCore.Annotations;

namespace OboxSteam.API.Controllers;

[Route("api/programs")]
[ApiController]
public class ProgramController : ControllerBase
{
    private readonly IProgramService _programService;
    private readonly ICurriculumReviewService _curriculumReviewService;
    private readonly IEnrollmentCurriculumService _enrollmentCurriculumService;
    private readonly IClassService _classService;
    private readonly IClassSeatHoldService _classSeatHoldService;
    private readonly IRebuyClassCatalogService _rebuyClassCatalogService;

    public ProgramController(
        IProgramService programService,
        ICurriculumReviewService curriculumReviewService,
        IEnrollmentCurriculumService enrollmentCurriculumService,
        IClassService classService,
        IClassSeatHoldService classSeatHoldService,
        IRebuyClassCatalogService rebuyClassCatalogService)
    {
        _programService = programService;
        _curriculumReviewService = curriculumReviewService;
        _enrollmentCurriculumService = enrollmentCurriculumService;
        _classService = classService;
        _classSeatHoldService = classSeatHoldService;
        _rebuyClassCatalogService = rebuyClassCatalogService;
    }

    // =========================================================================
    // GET ALL  —  GET /api/programs
    // =========================================================================

    [HttpGet]
    [SwaggerOperation(
        Summary = "Get all programs",
        Description = "Retrieve a paginated list of program information without modules. Supports search, filter, and sort options.")]
    [ProducesResponseType(typeof(ApiResult<Pagination<ProgramListItemDto>>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 400)]
    [ProducesResponseType(typeof(ApiResult<object>), 500)]
    public async Task<IActionResult> GetAllPrograms(
        [FromQuery, SwaggerParameter(Description = "Search by name or code (optional)")] string? search = null,
        [FromQuery, SwaggerParameter(Description = "Sort by field: name, code, level, rating, price, createdAt (optional)")] string? sortBy = null,
        [FromQuery, SwaggerParameter(Description = "Sort in descending order? Default: false")] bool isDescending = false,
        [FromQuery, SwaggerParameter(Description = "Page number, starting from 1")] int page = 1,
        [FromQuery, SwaggerParameter(Description = "Number of items per page")] int pageSize = 10,
        [FromQuery, SwaggerParameter(Description = "Filter by program code (optional)")] string? code = null,
        [FromQuery, SwaggerParameter(Description = "Filter by difficulty level (optional)")] DifficultyLevel? level = null,
        [FromQuery, SwaggerParameter(Description = "Filter by minimum rating (optional)")] decimal? rating = null,
        [FromQuery, SwaggerParameter(Description = "Filter by skills gained keyword (optional)")] string? skillsGained = null,
        [FromQuery, SwaggerParameter(Description = "Filter by program status: Draft, PendingReview, Approved, Active, Inactive (optional)")] ProgramStatus? status = null,
        [FromQuery, SwaggerParameter(Description = "Filter by category (optional)")] ProgramCategory? category = null)
    {
        if (page < 1 || pageSize < 1)
            return BadRequest(ApiResult<object>.Failure("400", "Invalid pagination parameters."));

        var result = await _programService.GetAllProgramsAsync(
            search, sortBy, isDescending, page, pageSize,
            code, level, rating, skillsGained, status, category);

        return Ok(ApiResult<Pagination<ProgramListItemDto>>.Success(result, "200", "Programs retrieved successfully."));
    }

    // =========================================================================
    // GET ALL WITH MODULES  —  GET /api/programs/with-modules
    // =========================================================================

    [HttpGet("with-modules")]
    [SwaggerOperation(
        Summary = "Get all programs with modules",
        Description = "Retrieve a paginated list of programs including their modules. Supports search, filter, and sort options.")]
    [ProducesResponseType(typeof(ApiResult<Pagination<ProgramsResponseDto>>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 400)]
    [ProducesResponseType(typeof(ApiResult<object>), 500)]
    public async Task<IActionResult> GetAllProgramsWithModules(
        [FromQuery, SwaggerParameter(Description = "Search by name or code (optional)")] string? search = null,
        [FromQuery, SwaggerParameter(Description = "Sort by field: name, code, level, rating, price, createdAt (optional)")] string? sortBy = null,
        [FromQuery, SwaggerParameter(Description = "Sort in descending order? Default: false")] bool isDescending = false,
        [FromQuery, SwaggerParameter(Description = "Page number, starting from 1")] int page = 1,
        [FromQuery, SwaggerParameter(Description = "Number of items per page")] int pageSize = 10,
        [FromQuery, SwaggerParameter(Description = "Filter by program code (optional)")] string? code = null,
        [FromQuery, SwaggerParameter(Description = "Filter by difficulty level (optional)")] DifficultyLevel? level = null,
        [FromQuery, SwaggerParameter(Description = "Filter by minimum rating (optional)")] decimal? rating = null,
        [FromQuery, SwaggerParameter(Description = "Filter by skills gained keyword (optional)")] string? skillsGained = null,
        [FromQuery, SwaggerParameter(Description = "Filter by program status: Draft, PendingReview, Approved, Active, Inactive (optional)")] ProgramStatus? status = null,
        [FromQuery, SwaggerParameter(Description = "Filter by category (optional)")] ProgramCategory? category = null)
    {
        if (page < 1 || pageSize < 1)
            return BadRequest(ApiResult<object>.Failure("400", "Invalid pagination parameters."));

        var result = await _programService.GetAllProgramsWithModulesAsync(
            search, sortBy, isDescending, page, pageSize,
            code, level, rating, skillsGained, status, category);

        return Ok(ApiResult<Pagination<ProgramsResponseDto>>.Success(result, "200", "Programs with modules retrieved successfully."));
    }

    // =========================================================================
    // GET CURRICULUM  —  GET /api/programs/{id}/curriculum
    // =========================================================================

    [HttpGet("{id:guid}/curriculum")]
    [SwaggerOperation(
        Summary = "Get program curriculum tree",
        Description = "Retrieve a compact curriculum outline for a program: modules, courses or milestones, activities, and materials.")]
    [ProducesResponseType(typeof(ApiResult<ProgramCurriculumDto>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    [ProducesResponseType(typeof(ApiResult<object>), 500)]
    public async Task<IActionResult> GetProgramCurriculum([FromRoute] Guid id)
    {
        await _enrollmentCurriculumService.EnsureStudentEnrolledInProgramAsync(id);
        var result = await _programService.GetProgramCurriculumAsync(id);
        return Ok(ApiResult<ProgramCurriculumDto>.Success(result, "200", "Program curriculum retrieved successfully."));
    }

    // =========================================================================
    // GET BY ID  —  GET /api/programs/{id}
    // =========================================================================

    [HttpGet("{id:guid}")]
    [SwaggerOperation(
        Summary = "Get program details",
        Description = "Retrieve detailed information for a specific program by its ID.")]
    [ProducesResponseType(typeof(ApiResult<ProgramsResponseDto>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    [ProducesResponseType(typeof(ApiResult<object>), 500)]
    public async Task<IActionResult> GetProgramById([FromRoute] Guid id)
    {
        var result = await _programService.GetProgramByIdAsync(id);
        return Ok(ApiResult<ProgramsResponseDto>.Success(result, "200", "Program retrieved successfully."));
    }

    // =========================================================================
    // OPEN CLASSES FOR ENROLLMENT  —  GET /api/programs/{id}/open-classes
    // =========================================================================

    [HttpGet("{id:guid}/open-classes")]
    [SwaggerOperation(
        Summary = "List open classes available for enrollment",
        Description = "Public preview of Standard classes that are Open and still have seats, "
            + "including schedule sessions and seat counts. Use before checkout to show recruiting "
            + "cohorts. Call select-class when the learner picks a class to soft-hold a seat for 5 minutes.")]
    [ProducesResponseType(typeof(ApiResult<IReadOnlyList<OpenEnrollmentClassDto>>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 400)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    public async Task<IActionResult> GetOpenEnrollmentClasses(
        [FromRoute] Guid id,
        [FromQuery, SwaggerParameter(
            Description = "Optional class the learner viewed — soft-sorted first when still enrollable")]
        Guid? preferredClassId = null)
    {
        var result = await _classService.GetOpenEnrollmentClassesAsync(id, preferredClassId);
        return Ok(ApiResult<IReadOnlyList<OpenEnrollmentClassDto>>.Success(
            result,
            "200",
            "Open enrollment classes retrieved successfully."));
    }

    // =========================================================================
    // REBUY CLASSES  —  GET /api/programs/{id}/rebuy-classes  [Student]
    // =========================================================================

    [HttpGet("{id:guid}/rebuy-classes")]
    [Authorize(Roles = "Student")]
    [SwaggerOperation(
        Summary = "List classes for first purchase or rebuy",
        Description = "Student picker for this program. First purchase, a Completed (100%) source, "
            + "and Failed/Dropped after the 3-month window return Open Standard classes with seats "
            + "(same join rule as open-classes; credit copy does not run after the window). Failed or "
            + "Dropped sources inside the window return Open and InProgress Standard classes with "
            + "per-module session progress and isEligible (stop-module / source-class rules). "
            + "IsRebuy is true only for Failed/Dropped inside the window. Active enrollment returns 409. "
            + "Public browse of recruiting cohorts still uses GET .../open-classes.")]
    [ProducesResponseType(typeof(ApiResult<RebuyClassCatalogDto>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 400)]
    [ProducesResponseType(typeof(ApiResult<object>), 401)]
    [ProducesResponseType(typeof(ApiResult<object>), 403)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    [ProducesResponseType(typeof(ApiResult<object>), 409)]
    public async Task<IActionResult> GetRebuyClasses([FromRoute] Guid id)
    {
        var result = await _rebuyClassCatalogService.GetRebuyClassesAsync(id);
        return Ok(ApiResult<RebuyClassCatalogDto>.Success(
            result,
            "200",
            "Rebuy classes retrieved successfully."));
    }

    // =========================================================================
    // SELECT CLASS  —  POST /api/programs/{id}/select-class
    // =========================================================================

    [HttpPost("{id:guid}/select-class")]
    [Authorize(Roles = "Student")]
    [SwaggerOperation(
        Summary = "Select a class and hold a seat",
        Description = "Starts a 5-minute soft seat hold when the student selects a class. "
            + "Checkout and parent-pay require this step first. Publishes seats.changed over SignalR.")]
    [ProducesResponseType(typeof(ApiResult<SelectProgramClassResponseDto>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 400)]
    [ProducesResponseType(typeof(ApiResult<object>), 401)]
    [ProducesResponseType(typeof(ApiResult<object>), 403)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    [ProducesResponseType(typeof(ApiResult<object>), 409)]
    public async Task<IActionResult> SelectClassForCheckout(
        [FromRoute] Guid id,
        [FromBody] SelectProgramClassRequestDto dto)
    {
        var result = await _classSeatHoldService.SelectClassForCheckoutAsync(id, dto.ClassId);
        return Ok(ApiResult<SelectProgramClassResponseDto>.Success(
            result,
            "200",
            "Class selected and seat held for checkout."));
    }

    // =========================================================================
    // RELEASE CLASS HOLD  —  POST /api/programs/{id}/release-class-hold
    // =========================================================================

    [HttpPost("{id:guid}/release-class-hold")]
    [Authorize(Roles = "Student")]
    [SwaggerOperation(
        Summary = "Release checkout seat hold",
        Description = "Releases the student's soft seat hold and abandons the PendingPayment program enrollment "
            + "for this program. Call when the learner leaves the checkout page or reloads. Idempotent.")]
    [ProducesResponseType(typeof(ApiResult<object>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 401)]
    [ProducesResponseType(typeof(ApiResult<object>), 403)]
    public async Task<IActionResult> ReleaseClassHoldForCheckout([FromRoute] Guid id)
    {
        await _classSeatHoldService.ReleaseClassHoldForCheckoutAsync(id);
        return Ok(ApiResult<object>.Success(null, "200", "Checkout seat hold released."));
    }

    // =========================================================================
    // GET BY NAME  —  GET /api/programs/name/{name}
    // =========================================================================

    [HttpGet("name/{name}")]
    [SwaggerOperation(
        Summary = "Get program by name",
        Description = "Retrieve a single program by its exact name (case-insensitive).")]
    [ProducesResponseType(typeof(ApiResult<ProgramsResponseDto>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    [ProducesResponseType(typeof(ApiResult<object>), 500)]
    public async Task<IActionResult> GetProgramByName(
        [FromRoute, SwaggerParameter(Description = "The program name to search for")] string name)
    {
        var result = await _programService.GetProgramByNameAsync(name);
        return Ok(ApiResult<ProgramsResponseDto>.Success(result, "200", "Program retrieved successfully."));
    }

    // =========================================================================
    // REVIEW QUEUE  —  GET /api/programs/review-queue
    // =========================================================================

    [HttpGet("review-queue")]
    [Authorize(Roles = "Expert,Manager,Admin")]
    [SwaggerOperation(
        Summary = "Curriculum review queue",
        Description = "Experts see PendingReview programs attached to their own frameworks. Manager and Admin see all pending programs.")]
    [ProducesResponseType(typeof(ApiResult<Pagination<ProgramReviewQueueItemDto>>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 400)]
    [ProducesResponseType(typeof(ApiResult<object>), 401)]
    [ProducesResponseType(typeof(ApiResult<object>), 403)]
    public async Task<IActionResult> GetReviewQueue(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        if (page < 1 || pageSize < 1)
        {
            return BadRequest(ApiResult<object>.Failure("400", "Invalid pagination parameters."));
        }

        var result = await _curriculumReviewService.GetReviewQueueAsync(page, pageSize);
        return Ok(ApiResult<Pagination<ProgramReviewQueueItemDto>>.Success(
            result, "200", "Review queue retrieved successfully."));
    }

    [HttpGet("{id:guid}/curriculum-reviews")]
    [Authorize(Roles = "Expert,Manager,Admin")]
    [SwaggerOperation(Summary = "List curriculum review rounds for a program")]
    [ProducesResponseType(typeof(ApiResult<IReadOnlyList<CurriculumReviewResponseDto>>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 401)]
    [ProducesResponseType(typeof(ApiResult<object>), 403)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    public async Task<IActionResult> GetCurriculumReviews([FromRoute] Guid id)
    {
        var result = await _curriculumReviewService.GetReviewsAsync(id);
        return Ok(ApiResult<IReadOnlyList<CurriculumReviewResponseDto>>.Success(
            result, "200", "Curriculum reviews retrieved successfully."));
    }

    [HttpPost("{id:guid}/submit-review")]
    [Authorize(Roles = "Admin,Manager")]
    [SwaggerOperation(
        Summary = "Submit a draft program for review",
        Description = "Runs framework pre-check. Attached framework moves the program to PendingReview; no framework moves it to Approved.")]
    [ProducesResponseType(typeof(ApiResult<ProgramsResponseDto>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 400)]
    [ProducesResponseType(typeof(ApiResult<object>), 401)]
    [ProducesResponseType(typeof(ApiResult<object>), 403)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    [ProducesResponseType(typeof(ApiResult<object>), 409)]
    public async Task<IActionResult> SubmitForReview([FromRoute] Guid id)
    {
        var result = await _curriculumReviewService.SubmitForReviewAsync(id);
        return Ok(ApiResult<ProgramsResponseDto>.Success(result, "200", "Program submitted for review."));
    }

    [HttpPost("{id:guid}/withdraw-review")]
    [Authorize(Roles = "Admin,Manager")]
    [SwaggerOperation(
        Summary = "Withdraw a pending review",
        Description = "Moves PendingReview back to Draft. No curriculum review row is created.")]
    [ProducesResponseType(typeof(ApiResult<ProgramsResponseDto>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 401)]
    [ProducesResponseType(typeof(ApiResult<object>), 403)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    [ProducesResponseType(typeof(ApiResult<object>), 409)]
    public async Task<IActionResult> WithdrawReview([FromRoute] Guid id)
    {
        var result = await _curriculumReviewService.WithdrawReviewAsync(id);
        return Ok(ApiResult<ProgramsResponseDto>.Success(result, "200", "Program review withdrawn."));
    }

    [HttpPost("{id:guid}/publish")]
    [Authorize(Roles = "Admin,Manager")]
    [SwaggerOperation(
        Summary = "Publish an approved program",
        Description = "Moves Approved to Active. Enrollment and class creation require Active.")]
    [ProducesResponseType(typeof(ApiResult<ProgramsResponseDto>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 401)]
    [ProducesResponseType(typeof(ApiResult<object>), 403)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    [ProducesResponseType(typeof(ApiResult<object>), 409)]
    public async Task<IActionResult> PublishProgram([FromRoute] Guid id)
    {
        var result = await _curriculumReviewService.PublishAsync(id);
        return Ok(ApiResult<ProgramsResponseDto>.Success(result, "200", "Program published successfully."));
    }

    [HttpPost("{id:guid}/approve-review")]
    [Authorize(Roles = "Expert")]
    [SwaggerOperation(
        Summary = "Approve a program as the framework owner",
        Description = "PendingReview → Approved. Scores are required when the framework has rubric criteria.")]
    [ProducesResponseType(typeof(ApiResult<CurriculumReviewResponseDto>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 400)]
    [ProducesResponseType(typeof(ApiResult<object>), 401)]
    [ProducesResponseType(typeof(ApiResult<object>), 403)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    [ProducesResponseType(typeof(ApiResult<object>), 409)]
    public async Task<IActionResult> ApproveReview(
        [FromRoute] Guid id,
        [FromBody] ApproveCurriculumReviewRequest? request = null)
    {
        var result = await _curriculumReviewService.ApproveAsync(id, request);
        return Ok(ApiResult<CurriculumReviewResponseDto>.Success(result, "200", "Program approved."));
    }

    [HttpPost("{id:guid}/request-changes")]
    [Authorize(Roles = "Expert")]
    [SwaggerOperation(
        Summary = "Request curriculum changes",
        Description = "PendingReview → Draft. Comment is required so the manager knows what to fix.")]
    [ProducesResponseType(typeof(ApiResult<CurriculumReviewResponseDto>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 400)]
    [ProducesResponseType(typeof(ApiResult<object>), 401)]
    [ProducesResponseType(typeof(ApiResult<object>), 403)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    [ProducesResponseType(typeof(ApiResult<object>), 409)]
    public async Task<IActionResult> RequestChanges(
        [FromRoute] Guid id,
        [FromBody] RequestCurriculumChangesRequest request)
    {
        if (request == null)
        {
            return BadRequest(ApiResult<object>.Failure("400", "Request body is required."));
        }

        var result = await _curriculumReviewService.RequestChangesAsync(id, request);
        return Ok(ApiResult<CurriculumReviewResponseDto>.Success(
            result, "200", "Changes requested."));
    }

    // =========================================================================
    // CREATE  —  POST /api/programs          [Admin only]
    // =========================================================================

    [HttpPost]
    [Authorize(Roles = "Admin,Manager")]
    [Consumes("multipart/form-data")]
    [SwaggerOperation(
        Summary = "Create a new program",
        Description = "Creates a new program as Draft. Use submit-review and publish to change status. Requires Admin or Manager role.")]
    [ProducesResponseType(typeof(ApiResult<ProgramsResponseDto>), 201)]
    [ProducesResponseType(typeof(ApiResult<object>), 400)]
    [ProducesResponseType(typeof(ApiResult<object>), 401)]
    [ProducesResponseType(typeof(ApiResult<object>), 403)]
    [ProducesResponseType(typeof(ApiResult<object>), 409)]
    [ProducesResponseType(typeof(ApiResult<object>), 500)]
    public async Task<IActionResult> AddProgram(
        [FromForm, SwaggerParameter("Program data to be created (multipart field prefix: data.<PropertyName>)")] CreateProgramRequestDto data,
        IFormFile file)
    {
        var result = await _programService.CreateProgramAsync(data, file);

        return CreatedAtAction(
            nameof(GetProgramById),
            new { id = result.Id },
            ApiResult<ProgramsResponseDto>.Success(result, "201", "Program created successfully."));
    }

    // =========================================================================
    // UPDATE  —  PUT /api/programs/{id}      [Admin only]
    // =========================================================================

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Admin,Manager")]
    [SwaggerOperation(
        Summary = "Update program information",
        Description = "Updates program details. Status may only toggle Active ↔ Inactive. Requires Admin or Manager role.")]
    [ProducesResponseType(typeof(ApiResult<ProgramsResponseDto>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 400)]
    [ProducesResponseType(typeof(ApiResult<object>), 401)]
    [ProducesResponseType(typeof(ApiResult<object>), 403)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    [ProducesResponseType(typeof(ApiResult<object>), 409)]
    [ProducesResponseType(typeof(ApiResult<object>), 500)]
    public async Task<IActionResult> UpdateProgram(
        [FromRoute] Guid id,
        [FromBody, SwaggerParameter("Updated program data")] UpdateProgramRequestDto dto)
    {
        if (dto == null)
            return BadRequest(ApiResult<object>.Failure("400", "Program update data is required."));

        var result = await _programService.UpdateProgramAsync(id, dto);
        return Ok(ApiResult<ProgramsResponseDto>.Success(result, "200", "Program updated successfully."));
    }

    /// <summary>
    /// Upload thumbnail for a specific program.
    /// </summary>
    /// <param name="id">Program ID.</param>
    /// <param name="file">Image file (jpg, jpeg, png, webp). Max 5 MB.</param>
    /// <returns>Updated program with new thumbnail URL.</returns>
    [HttpPost("{id:guid}/thumbnail")]
    [Authorize(Roles = "Admin,Manager")]
    [SwaggerOperation(
        Summary = "Upload program thumbnail",
        Description = "Uploads a new thumbnail image for the specified program. Replaces the existing thumbnail if one exists.")]
    [ProducesResponseType(typeof(ApiResult<ProgramsResponseDto>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 400)]
    [ProducesResponseType(typeof(ApiResult<object>), 401)]
    [ProducesResponseType(typeof(ApiResult<object>), 403)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    public async Task<IActionResult> UploadProgramThumbnail([FromRoute] Guid id, IFormFile file)
    {
        var result = await _programService.UploadProgramThumbnailAsync(id, file);
        return Ok(ApiResult<ProgramsResponseDto>.Success(result, "200", "Program thumbnail uploaded successfully."));
    }

    // =========================================================================
    // DELETE  —  DELETE /api/programs/{id}   [Admin only]
    // =========================================================================

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Admin,Manager")]
    [SwaggerOperation(
        Summary = "Delete a program",
        Description = "Soft-deletes a program by its ID. Requires Admin or Manager role.")]
    [ProducesResponseType(typeof(ApiResult<bool>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 401)]
    [ProducesResponseType(typeof(ApiResult<object>), 403)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    [ProducesResponseType(typeof(ApiResult<object>), 500)]
    public async Task<IActionResult> DeleteProgram([FromRoute] Guid id)
    {
        var result = await _programService.DeleteProgramAsync(id);

        if (!result)
            return NotFound(ApiResult<object>.Failure("404", $"Program with ID '{id}' not found."));

        return Ok(ApiResult<bool>.Success(result, "200", "Program deleted successfully."));
    }
}
