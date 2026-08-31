using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OboxSteam.Application.Commons;
using OboxSteam.Application.DTOs.ExpertDTO;
using OboxSteam.Application.Interfaces;
using OboxSteam.Application.Utils;
using Swashbuckle.AspNetCore.Annotations;

namespace OboxSteam.API.Controllers;

[Route("api/experts")]
[ApiController]
public class ExpertController : ControllerBase
{
    private readonly IExpertService _expertService;

    public ExpertController(IExpertService expertService)
    {
        _expertService = expertService;
    }

    // =========================================================================
    // GET ALL  —  GET /api/experts
    // =========================================================================

    [HttpGet]
    [SwaggerOperation(
        Summary = "Get all experts",
        Description = "Retrieve a paginated list of experts with optional search, filter, and sort options.")]
    [ProducesResponseType(typeof(ApiResult<Pagination<ExpertResponseDto>>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 400)]
    public async Task<IActionResult> GetAllExperts(
        [FromQuery, SwaggerParameter(Description = "Search by name or code (optional)")] string? search = null,
        [FromQuery, SwaggerParameter(Description = "Sort by field: fullName, code, createdAt (optional)")] string? sortBy = null,
        [FromQuery, SwaggerParameter(Description = "Sort in descending order? Default: false")] bool isDescending = false,
        [FromQuery, SwaggerParameter(Description = "Page number, starting from 1")] int page = 1,
        [FromQuery, SwaggerParameter(Description = "Number of items per page")] int pageSize = 10,
        [FromQuery, SwaggerParameter(Description = "Filter by expert code (optional)")] string? code = null)
    {
        if (page < 1 || pageSize < 1)
        {
            return BadRequest(ApiResult<object>.Failure("400", "Invalid pagination parameters."));
        }

        var result = await _expertService.GetAllExpertsAsync(search, sortBy, isDescending, page, pageSize, code);

        return Ok(ApiResult<Pagination<ExpertResponseDto>>.Success(result, "200", "Experts retrieved successfully."));
    }

    // =========================================================================
    // GET BY ID  —  GET /api/experts/{id}
    // =========================================================================

    [HttpGet("{id:guid}")]
    [SwaggerOperation(
        Summary = "Get expert details",
        Description = "Public expert profile including specialization, degrees, publications, and program board membership.")]
    [ProducesResponseType(typeof(ApiResult<ExpertResponseDto>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    public async Task<IActionResult> GetExpertById([FromRoute] Guid id)
    {
        var result = await _expertService.GetExpertByIdAsync(id);
        return Ok(ApiResult<ExpertResponseDto>.Success(result, "200", "Expert retrieved successfully."));
    }

    // =========================================================================
    // CREATE  —  POST /api/experts          [Admin only]
    // =========================================================================

    [HttpPost]
    [Authorize(Roles = "Admin,Manager")]
    [SwaggerOperation(
        Summary = "Create a new expert",
        Description = "Creates an expert profile and a dedicated Expert login. Email and password are required; the expert can sign in immediately. Password reset uses the existing forgot-password OTP flow. Requires Admin or Manager role.")]
    [ProducesResponseType(typeof(ApiResult<ExpertResponseDto>), 201)]
    [ProducesResponseType(typeof(ApiResult<object>), 400)]
    [ProducesResponseType(typeof(ApiResult<object>), 401)]
    [ProducesResponseType(typeof(ApiResult<object>), 403)]
    [ProducesResponseType(typeof(ApiResult<object>), 409)]
    public async Task<IActionResult> AddExpert(
        [FromBody, SwaggerParameter("New expert data to be created")] CreateExpertRequest dto)
    {
        var result = await _expertService.AddExpertAsync(dto);

        return CreatedAtAction(
            nameof(GetExpertById),
            new { id = result.Id },
            ApiResult<ExpertResponseDto>.Success(result, "201", "Expert created successfully."));
    }

    // =========================================================================
    // ADD PROGRAM  —  POST /api/experts/{expertId}/programs/{programId}
    // =========================================================================

    [HttpPost("{expertId:guid}/programs/{programId:guid}")]
    [Authorize(Roles = "Admin,Manager")]
    [SwaggerOperation(
        Summary = "Add program to expert",
        Description = "Assigns a program to an expert. Optional request body may include RoleInBoard. Requires Admin or Manager role.")]
    [ProducesResponseType(typeof(ApiResult<ExpertProgramSummaryDto>), 201)]
    [ProducesResponseType(typeof(ApiResult<object>), 400)]
    [ProducesResponseType(typeof(ApiResult<object>), 401)]
    [ProducesResponseType(typeof(ApiResult<object>), 403)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    [ProducesResponseType(typeof(ApiResult<object>), 409)]
    public async Task<IActionResult> AddProgramToExpert(
        [FromRoute] Guid expertId,
        [FromRoute] Guid programId,
        [FromBody, SwaggerParameter("Optional role in program board")] AddProgramToExpertDto? dto = null)
    {
        var result = await _expertService.AddProgramToExpertAsync(expertId, programId, dto);

        return CreatedAtAction(
            nameof(GetExpertById),
            new { id = expertId },
            ApiResult<ExpertProgramSummaryDto>.Success(result, "201", "Program assigned to expert successfully."));
    }

    // =========================================================================
    // UPDATE  —  PUT /api/experts/{id}      [Admin only]
    // =========================================================================

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Admin,Manager")]
    [SwaggerOperation(
        Summary = "Update expert information",
        Description = "Updates the details of a specific expert by its ID. Requires Admin or Manager role.")]
    [ProducesResponseType(typeof(ApiResult<ExpertResponseDto>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 400)]
    [ProducesResponseType(typeof(ApiResult<object>), 401)]
    [ProducesResponseType(typeof(ApiResult<object>), 403)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    [ProducesResponseType(typeof(ApiResult<object>), 409)]
    public async Task<IActionResult> UpdateExpert(
        [FromRoute] Guid id,
        [FromBody, SwaggerParameter("Updated expert data")] UpdateExpertRequest dto)
    {
        if (dto == null)
        {
            return BadRequest(ApiResult<object>.Failure("400", "Expert update data is required."));
        }

        var result = await _expertService.UpdateExpertAsync(id, dto);
        return Ok(ApiResult<ExpertResponseDto>.Success(result, "200", "Expert updated successfully."));
    }

    /// <summary>
    /// Upload avatar for a specific expert.
    /// </summary>
    /// <param name="id">Expert ID.</param>
    /// <param name="file">Image file (jpg, jpeg, png, gif). Max 5 MB.</param>
    /// <returns>Updated expert profile with new avatar URL.</returns>
    [HttpPost("{id:guid}/avatar")]
    [Authorize(Roles = "Admin,Manager")]
    [SwaggerOperation(
        Summary = "Upload expert avatar",
        Description = "Uploads a new avatar image for the specified expert. Replaces the existing avatar if one exists.")]
    [ProducesResponseType(typeof(ApiResult<ExpertResponseDto>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 400)]
    [ProducesResponseType(typeof(ApiResult<object>), 401)]
    [ProducesResponseType(typeof(ApiResult<object>), 403)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    public async Task<IActionResult> UploadAvatar([FromRoute] Guid id, IFormFile file)
    {
        var result = await _expertService.UploadAvatarAsync(id, file);
        return Ok(ApiResult<ExpertResponseDto>.Success(result, "200", "Avatar uploaded successfully."));
    }

    // =========================================================================
    // UPDATE PROGRAM  —  PUT /api/experts/{expertId}/programs/{programId}
    // =========================================================================

    [HttpPut("{expertId:guid}/programs/{programId:guid}")]
    [Authorize(Roles = "Admin,Manager")]
    [SwaggerOperation(
        Summary = "Update expert program assignment",
        Description = "Updates the program assignment for an expert. Requires Admin or Manager role.")]
    [ProducesResponseType(typeof(ApiResult<ExpertProgramSummaryDto>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 400)]
    [ProducesResponseType(typeof(ApiResult<object>), 401)]
    [ProducesResponseType(typeof(ApiResult<object>), 403)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    public async Task<IActionResult> UpdateProgramOfExpert([FromRoute] Guid expertId, [FromRoute] Guid programId)
    {
        var result = await _expertService.UpdateProgramOfExpertAsync(expertId, programId);
        return Ok(ApiResult<ExpertProgramSummaryDto>.Success(result, "200", "Expert program updated successfully."));
    }

    // =========================================================================
    // DELETE  —  DELETE /api/experts/{id}   [Admin only]
    // =========================================================================

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Admin,Manager")]
    [SwaggerOperation(
        Summary = "Delete an expert",
        Description = "Soft-deletes an expert by its ID. Requires Admin or Manager role.")]
    [ProducesResponseType(typeof(ApiResult<bool>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 401)]
    [ProducesResponseType(typeof(ApiResult<object>), 403)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    public async Task<IActionResult> DeleteExpert([FromRoute] Guid id)
    {
        var result = await _expertService.DeleteExpertAsync(id);

        if (!result)
        {
            return NotFound(ApiResult<object>.Failure("404", $"Expert with ID '{id}' not found."));
        }

        return Ok(ApiResult<bool>.Success(result, "200", "Expert deleted successfully."));
    }

    // =========================================================================
    // REMOVE PROGRAM  —  DELETE /api/experts/{expertId}/programs/{programId}
    // =========================================================================

    [HttpDelete("{expertId:guid}/programs/{programId:guid}")]
    [Authorize(Roles = "Admin,Manager")]
    [SwaggerOperation(
        Summary = "Remove program from expert",
        Description = "Removes a program assignment from an expert. Requires Admin or Manager role.")]
    [ProducesResponseType(typeof(ApiResult<bool>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 401)]
    [ProducesResponseType(typeof(ApiResult<object>), 403)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    public async Task<IActionResult> RemoveProgramFromExpert([FromRoute] Guid expertId, [FromRoute] Guid programId)
    {
        var result = await _expertService.RemoveProgramFromExpertAsync(expertId, programId);
        return Ok(ApiResult<bool>.Success(result, "200", "Program removed from expert successfully."));
    }

    [HttpGet("{id:guid}/profile")]
    [SwaggerOperation(
        Summary = "Get public expert profile",
        Description = "Public profile including specialization, degrees, publications, and program board membership.")]
    [ProducesResponseType(typeof(ApiResult<ExpertResponseDto>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    public async Task<IActionResult> GetPublicProfile([FromRoute] Guid id)
    {
        var result = await _expertService.GetExpertByIdAsync(id);
        return Ok(ApiResult<ExpertResponseDto>.Success(result, "200", "Expert profile retrieved successfully."));
    }

    [HttpPost("{expertId:guid}/degrees")]
    [Authorize(Roles = "Admin,Manager")]
    [SwaggerOperation(
        Summary = "Add expert degree",
        Description = "Adds an academic credential to the expert profile. Requires Admin or Manager role.")]
    [ProducesResponseType(typeof(ApiResult<ExpertDegreeResponseDto>), 201)]
    [ProducesResponseType(typeof(ApiResult<object>), 400)]
    [ProducesResponseType(typeof(ApiResult<object>), 401)]
    [ProducesResponseType(typeof(ApiResult<object>), 403)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    public async Task<IActionResult> AddDegree(
        [FromRoute] Guid expertId,
        [FromBody] ExpertDegreeRequestDto dto)
    {
        var result = await _expertService.AddDegreeAsync(expertId, dto);
        return CreatedAtAction(
            nameof(GetExpertById),
            new { id = expertId },
            ApiResult<ExpertDegreeResponseDto>.Success(result, "201", "Degree added successfully."));
    }

    [HttpPut("{expertId:guid}/degrees/{degreeId:guid}")]
    [Authorize(Roles = "Admin,Manager")]
    [SwaggerOperation(
        Summary = "Update expert degree",
        Description = "Updates an academic credential. Requires Admin or Manager role.")]
    [ProducesResponseType(typeof(ApiResult<ExpertDegreeResponseDto>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 400)]
    [ProducesResponseType(typeof(ApiResult<object>), 401)]
    [ProducesResponseType(typeof(ApiResult<object>), 403)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    public async Task<IActionResult> UpdateDegree(
        [FromRoute] Guid expertId,
        [FromRoute] Guid degreeId,
        [FromBody] ExpertDegreeRequestDto dto)
    {
        var result = await _expertService.UpdateDegreeAsync(expertId, degreeId, dto);
        return Ok(ApiResult<ExpertDegreeResponseDto>.Success(result, "200", "Degree updated successfully."));
    }

    [HttpDelete("{expertId:guid}/degrees/{degreeId:guid}")]
    [Authorize(Roles = "Admin,Manager")]
    [SwaggerOperation(
        Summary = "Delete expert degree",
        Description = "Soft-deletes an academic credential. Requires Admin or Manager role.")]
    [ProducesResponseType(typeof(ApiResult<bool>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 401)]
    [ProducesResponseType(typeof(ApiResult<object>), 403)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    public async Task<IActionResult> DeleteDegree([FromRoute] Guid expertId, [FromRoute] Guid degreeId)
    {
        var result = await _expertService.DeleteDegreeAsync(expertId, degreeId);
        return Ok(ApiResult<bool>.Success(result, "200", "Degree deleted successfully."));
    }

    [HttpPost("{expertId:guid}/publications")]
    [Authorize(Roles = "Admin,Manager")]
    [SwaggerOperation(
        Summary = "Add expert publication",
        Description = "Adds a publication to the expert profile. Requires Admin or Manager role.")]
    [ProducesResponseType(typeof(ApiResult<ExpertPublicationResponseDto>), 201)]
    [ProducesResponseType(typeof(ApiResult<object>), 400)]
    [ProducesResponseType(typeof(ApiResult<object>), 401)]
    [ProducesResponseType(typeof(ApiResult<object>), 403)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    public async Task<IActionResult> AddPublication(
        [FromRoute] Guid expertId,
        [FromBody] ExpertPublicationRequestDto dto)
    {
        var result = await _expertService.AddPublicationAsync(expertId, dto);
        return CreatedAtAction(
            nameof(GetExpertById),
            new { id = expertId },
            ApiResult<ExpertPublicationResponseDto>.Success(result, "201", "Publication added successfully."));
    }

    [HttpPut("{expertId:guid}/publications/{publicationId:guid}")]
    [Authorize(Roles = "Admin,Manager")]
    [SwaggerOperation(
        Summary = "Update expert publication",
        Description = "Updates a publication. Requires Admin or Manager role.")]
    [ProducesResponseType(typeof(ApiResult<ExpertPublicationResponseDto>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 400)]
    [ProducesResponseType(typeof(ApiResult<object>), 401)]
    [ProducesResponseType(typeof(ApiResult<object>), 403)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    public async Task<IActionResult> UpdatePublication(
        [FromRoute] Guid expertId,
        [FromRoute] Guid publicationId,
        [FromBody] ExpertPublicationRequestDto dto)
    {
        var result = await _expertService.UpdatePublicationAsync(expertId, publicationId, dto);
        return Ok(ApiResult<ExpertPublicationResponseDto>.Success(result, "200", "Publication updated successfully."));
    }

    [HttpDelete("{expertId:guid}/publications/{publicationId:guid}")]
    [Authorize(Roles = "Admin,Manager")]
    [SwaggerOperation(
        Summary = "Delete expert publication",
        Description = "Soft-deletes a publication. Requires Admin or Manager role.")]
    [ProducesResponseType(typeof(ApiResult<bool>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 401)]
    [ProducesResponseType(typeof(ApiResult<object>), 403)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    public async Task<IActionResult> DeletePublication(
        [FromRoute] Guid expertId,
        [FromRoute] Guid publicationId)
    {
        var result = await _expertService.DeletePublicationAsync(expertId, publicationId);
        return Ok(ApiResult<bool>.Success(result, "200", "Publication deleted successfully."));
    }
}
