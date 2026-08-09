using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OboxSteam.Application.DTOs.ClassQuizQuestionSetDTO;
using OboxSteam.Application.Interfaces;
using OboxSteam.Application.Utils;
using Swashbuckle.AspNetCore.Annotations;

namespace OboxSteam.API.Controllers;

[Route("api/assignments/{assignmentId:guid}/classes/{classId:guid}/quiz-set")]
[ApiController]
public class ClassQuizQuestionSetController : ControllerBase
{
    private readonly IClassQuizQuestionSetService _service;

    public ClassQuizQuestionSetController(IClassQuizQuestionSetService service)
    {
        _service = service;
    }

    [HttpPost("pull")]
    [Authorize(Roles = "Mentor")]
    [SwaggerOperation(
        Summary = "Pull a fixed quiz question set for a class",
        Description = "Draws questions from the assignment's question bank into a class-scoped editable copy. Re-pull replaces any prior unlocked set.")]
    [ProducesResponseType(typeof(ApiResult<ClassQuizQuestionSetResponseDto>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 400)]
    [ProducesResponseType(typeof(ApiResult<object>), 403)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    [ProducesResponseType(typeof(ApiResult<object>), 409)]
    public async Task<IActionResult> Pull(Guid assignmentId, Guid classId)
    {
        var result = await _service.PullAsync(assignmentId, classId);
        return Ok(ApiResult<ClassQuizQuestionSetResponseDto>.Success(
            result, "200", "Quiz question set pulled successfully."));
    }

    [HttpGet]
    [Authorize(Roles = "Mentor,Manager,Admin")]
    [SwaggerOperation(Summary = "Get the pulled quiz question set for a class")]
    [ProducesResponseType(typeof(ApiResult<ClassQuizQuestionSetResponseDto>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 403)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    public async Task<IActionResult> Get(Guid assignmentId, Guid classId)
    {
        var result = await _service.GetAsync(assignmentId, classId);
        return Ok(ApiResult<ClassQuizQuestionSetResponseDto>.Success(
            result, "200", "Quiz question set retrieved successfully."));
    }

    [HttpPut("questions/{questionId:guid}")]
    [Authorize(Roles = "Mentor")]
    [SwaggerOperation(
        Summary = "Update a question in the class quiz set",
        Description = "Edits the class-scoped copy only. Locked after any student submission exists for this assignment in the class.")]
    [ProducesResponseType(typeof(ApiResult<ClassQuizQuestionResponseDto>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 400)]
    [ProducesResponseType(typeof(ApiResult<object>), 403)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    [ProducesResponseType(typeof(ApiResult<object>), 409)]
    public async Task<IActionResult> UpdateQuestion(
        Guid assignmentId,
        Guid classId,
        Guid questionId,
        [FromBody] UpdateClassQuizQuestionRequestDto request)
    {
        if (request == null)
            return BadRequest(ApiResult<object>.Failure("400", "Question update data is required."));

        var result = await _service.UpdateQuestionAsync(assignmentId, classId, questionId, request);
        return Ok(ApiResult<ClassQuizQuestionResponseDto>.Success(
            result, "200", "Quiz question updated successfully."));
    }
}
