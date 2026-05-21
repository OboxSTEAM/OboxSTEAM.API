using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OboxSteam.Application.Commons;
using OboxSteam.Application.DTOs.CourseDTO;
using OboxSteam.Application.Interfaces;
using OboxSteam.Application.Utils;
using Swashbuckle.AspNetCore.Annotations;

namespace OboxSteam.API.Controllers;

[Route("api/courses")]
[ApiController]
public class CourseController : ControllerBase
{
    private readonly ICourseService _courseService;

    public CourseController(ICourseService courseService)
    {
        _courseService = courseService;
    }

    // =========================================================================
    // GET ALL  —  GET /api/courses
    // =========================================================================

    [HttpGet]
    [SwaggerOperation(
        Summary = "Get all courses",
        Description = "Retrieve a paginated list of courses with optional search, filter, and sort options.")]
    [ProducesResponseType(typeof(ApiResult<Pagination<CourseResponseDto>>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 400)]
    public async Task<IActionResult> GetAllCourses(
        [FromQuery, SwaggerParameter(Description = "Search by name or code (optional)")] string? search = null,
        [FromQuery, SwaggerParameter(Description = "Sort by field: name, code, moduleId, mentorId, createdAt (optional)")] string? sortBy = null,
        [FromQuery, SwaggerParameter(Description = "Sort in descending order? Default: false")] bool isDescending = false,
        [FromQuery, SwaggerParameter(Description = "Page number, starting from 1")] int page = 1,
        [FromQuery, SwaggerParameter(Description = "Number of items per page")] int pageSize = 10,
        [FromQuery, SwaggerParameter(Description = "Filter by course code (optional)")] string? code = null,
        [FromQuery, SwaggerParameter(Description = "Filter by module name (optional)")] string? moduleName = null,
        [FromQuery, SwaggerParameter(Description = "Filter by mentor full name (optional)")] string? mentorName = null)
    {
        if (page < 1 || pageSize < 1)
        {
            return BadRequest(ApiResult<object>.Failure("400", "Invalid pagination parameters."));
        }

        var result = await _courseService.GetAllCoursesAsync(
            search, sortBy, isDescending, page, pageSize, code, moduleName, mentorName);

        return Ok(ApiResult<Pagination<CourseResponseDto>>.Success(result, "200", "Courses retrieved successfully."));
    }

    // =========================================================================
    // GET BY ID  —  GET /api/courses/{id}
    // =========================================================================

    [HttpGet("{id:guid}")]
    [SwaggerOperation(
        Summary = "Get course details",
        Description = "Retrieve detailed information for a specific course by its ID.")]
    [ProducesResponseType(typeof(ApiResult<CourseResponseDto>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    public async Task<IActionResult> GetCourseById([FromRoute] Guid id)
    {
        var result = await _courseService.GetCourseByIdAsync(id);

        if (result == null)
        {
            return NotFound(ApiResult<object>.Failure("404", $"Course with ID '{id}' not found."));
        }

        return Ok(ApiResult<CourseResponseDto>.Success(result, "200", "Course retrieved successfully."));
    }

    // =========================================================================
    // GET BY NAME  —  GET /api/courses/name/{name}
    // =========================================================================

    [HttpGet("name/{name}")]
    [SwaggerOperation(
        Summary = "Get course by name",
        Description = "Retrieve a single course by its exact name (case-insensitive).")]
    [ProducesResponseType(typeof(ApiResult<CourseResponseDto>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    public async Task<IActionResult> GetCourseByName(
        [FromRoute, SwaggerParameter(Description = "The course name to search for")] string name)
    {
        var result = await _courseService.GetCourseByNameAsync(name);

        if (result == null)
        {
            return NotFound(ApiResult<object>.Failure("404", $"Course with name '{name}' not found."));
        }

        return Ok(ApiResult<CourseResponseDto>.Success(result, "200", "Course retrieved successfully."));
    }

    // =========================================================================
    // CREATE  —  POST /api/courses          [Admin only]
    // =========================================================================

    [HttpPost]
    [Authorize(Roles = "SuperAdmin,Manager")]
    [SwaggerOperation(
        Summary = "Create a new course",
        Description = "Creates a new course with the provided information. Requires SuperAdmin or Manager role.")]
    [ProducesResponseType(typeof(ApiResult<CourseResponseDto>), 201)]
    [ProducesResponseType(typeof(ApiResult<object>), 400)]
    [ProducesResponseType(typeof(ApiResult<object>), 401)]
    [ProducesResponseType(typeof(ApiResult<object>), 403)]
    [ProducesResponseType(typeof(ApiResult<object>), 409)]
    public async Task<IActionResult> CreateCourse(
        [FromBody, SwaggerParameter("New course data to be created")] CreateCourseRequestDto dto)
    {
        var result = await _courseService.CreateCourseAsync(dto);

        return CreatedAtAction(
            nameof(GetCourseById),
            new { id = result.Id },
            ApiResult<CourseResponseDto>.Success(result, "201", "Course created successfully."));
    }

    // =========================================================================
    // UPDATE  —  PUT /api/courses/{id}      [Admin only]
    // =========================================================================

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "SuperAdmin,Manager")]
    [SwaggerOperation(
        Summary = "Update course information",
        Description = "Updates the details of a specific course by its ID. Requires SuperAdmin or Manager role.")]
    [ProducesResponseType(typeof(ApiResult<CourseResponseDto>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 400)]
    [ProducesResponseType(typeof(ApiResult<object>), 401)]
    [ProducesResponseType(typeof(ApiResult<object>), 403)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    [ProducesResponseType(typeof(ApiResult<object>), 409)]
    public async Task<IActionResult> UpdateCourse(
        [FromRoute] Guid id,
        [FromBody, SwaggerParameter("Updated course data")] UpdateCourseRequestDto dto)
    {
        if (dto == null)
        {
            return BadRequest(ApiResult<object>.Failure("400", "Course update data is required."));
        }

        var result = await _courseService.UpdateCourseAsync(id, dto);

        if (result == null)
        {
            return NotFound(ApiResult<object>.Failure("404", $"Course with ID '{id}' not found."));
        }

        return Ok(ApiResult<CourseResponseDto>.Success(result, "200", "Course updated successfully."));
    }

    // =========================================================================
    // DELETE  —  DELETE /api/courses/{id}   [Admin only]
    // =========================================================================

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "SuperAdmin,Manager")]
    [SwaggerOperation(
        Summary = "Delete a course",
        Description = "Soft-deletes a course by its ID. Requires SuperAdmin or Manager role.")]
    [ProducesResponseType(typeof(ApiResult<bool>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 401)]
    [ProducesResponseType(typeof(ApiResult<object>), 403)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    public async Task<IActionResult> DeleteCourse([FromRoute] Guid id)
    {
        var result = await _courseService.DeleteCourseAsync(id);

        if (!result)
        {
            return NotFound(ApiResult<object>.Failure("404", $"Course with ID '{id}' not found."));
        }

        return Ok(ApiResult<bool>.Success(result, "200", "Course deleted successfully."));
    }
}
