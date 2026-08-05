using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OboxSteam.Application.DTOs.QuizDTO;
using OboxSteam.Application.Interfaces;
using OboxSteam.Application.Utils;
using Swashbuckle.AspNetCore.Annotations;

namespace OboxSteam.API.Controllers;

[Route("api")]
[ApiController]
public class QuizController : ControllerBase
{
    private readonly IQuizAttemptService _quizAttemptService;

    public QuizController(IQuizAttemptService quizAttemptService)
    {
        _quizAttemptService = quizAttemptService;
    }

    [HttpPost("assignments/{assignmentId:guid}/quiz/start")]
    [Authorize(Roles = "Student")]
    [SwaggerOperation(
        Summary = "Start a quiz attempt",
        Description = "Starts a new Mode A quiz attempt or resumes an existing Pending submission. "
            + "Requires Student role and an active enrollment in the assignment's module.")]
    [ProducesResponseType(typeof(ApiResult<QuizAttemptResponseDto>), 201)]
    [ProducesResponseType(typeof(ApiResult<object>), 400)]
    [ProducesResponseType(typeof(ApiResult<object>), 401)]
    [ProducesResponseType(typeof(ApiResult<object>), 403)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    [ProducesResponseType(typeof(ApiResult<object>), 409)]
    public async Task<IActionResult> StartQuiz(Guid assignmentId)
    {
        var result = await _quizAttemptService.StartQuiz(assignmentId);

        return CreatedAtAction(
            nameof(GetQuiz),
            new { submissionId = result.SubmissionId },
            ApiResult<QuizAttemptResponseDto>.Success(result, "201", "Quiz attempt started successfully."));
    }

    [HttpGet("submissions/{submissionId:guid}/quiz")]
    [Authorize(Roles = "Student, Mentor, Manager, SuperAdmin")]
    [SwaggerOperation(
        Summary = "Get in-progress quiz",
        Description = "Returns quiz questions and saved answers for a Pending submission. "
            + "Students may only access their own attempt. Mentors may access students in their class. "
            + "Manager and SuperAdmin may access any submission.")]
    [ProducesResponseType(typeof(ApiResult<QuizAttemptResponseDto>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 401)]
    [ProducesResponseType(typeof(ApiResult<object>), 403)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    [ProducesResponseType(typeof(ApiResult<object>), 409)]
    public async Task<IActionResult> GetQuiz(Guid submissionId)
    {
        var result = await _quizAttemptService.GetQuiz(submissionId);
        if (result == null)
            return NotFound(ApiResult<object>.Failure("404", "Quiz not found."));

        return Ok(ApiResult<QuizAttemptResponseDto>.Success(result, "200", "Quiz retrieved successfully."));
    }

    [HttpPut("submissions/{submissionId:guid}/quiz/answers")]
    [Authorize(Roles = "Student")]
    [SwaggerOperation(
        Summary = "Save draft quiz answers",
        Description = "Upserts draft answers for a Pending submission. Partial answers are allowed. Requires Student role.")]
    [ProducesResponseType(typeof(ApiResult<SaveDraftAnswersResponseDto>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 400)]
    [ProducesResponseType(typeof(ApiResult<object>), 401)]
    [ProducesResponseType(typeof(ApiResult<object>), 403)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    [ProducesResponseType(typeof(ApiResult<object>), 409)]
    public async Task<IActionResult> SaveDraftAnswers(
        Guid submissionId,
        [FromBody, SwaggerParameter("Draft answers")] SaveDraftAnswersRequestDto request)
    {
        var result = await _quizAttemptService.SaveDraftAnswers(submissionId, request);

        return Ok(ApiResult<SaveDraftAnswersResponseDto>.Success(
            result,
            "200",
            "Draft answers saved successfully."));
    }

    [HttpPost("submissions/{submissionId:guid}/quiz/submit")]
    [Authorize(Roles = "Student")]
    [SwaggerOperation(
        Summary = "Submit quiz",
        Description = "Final submit: merges request answers with saved drafts, validates all questions are answered, "
            + "auto-grades, and sets submission to Graded. An empty answers array is allowed when all drafts are saved. "
            + "Requires Student role.")]
    [ProducesResponseType(typeof(ApiResult<QuizResultResponseDto>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 400)]
    [ProducesResponseType(typeof(ApiResult<object>), 401)]
    [ProducesResponseType(typeof(ApiResult<object>), 403)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    [ProducesResponseType(typeof(ApiResult<object>), 409)]
    public async Task<IActionResult> SubmitQuiz(
        Guid submissionId,
        [FromBody, SwaggerParameter("Final answers")] SubmitQuizAnswersRequestDto request)
    {
        var result = await _quizAttemptService.SubmitQuiz(submissionId, request);

        return Ok(ApiResult<QuizResultResponseDto>.Success(result, "200", "Quiz submitted successfully."));
    }

    [HttpGet("submissions/{submissionId:guid}/quiz/result")]
    [Authorize(Roles = "Student, Mentor, Manager, SuperAdmin")]
    [SwaggerOperation(
        Summary = "Get quiz result",
        Description = "Returns the graded result for a submission. "
            + "Students may only access their own result. Mentors may access students in their class. "
            + "Manager and SuperAdmin may access any submission.")]
    [ProducesResponseType(typeof(ApiResult<QuizResultResponseDto>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 401)]
    [ProducesResponseType(typeof(ApiResult<object>), 403)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    [ProducesResponseType(typeof(ApiResult<object>), 409)]
    public async Task<IActionResult> GetQuizResult(Guid submissionId)
    {
        var result = await _quizAttemptService.GetQuizResult(submissionId);
        if (result == null)
            return NotFound(ApiResult<object>.Failure("404", "Quiz result not found."));

        return Ok(ApiResult<QuizResultResponseDto>.Success(result, "200", "Quiz result retrieved successfully."));
    }
}
