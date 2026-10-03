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
            + "Requires Student role and an active enrollment in the assignment's module. "
            + "A new attempt needs the class AssignmentWindow to be open (StartTime <= now <= EndTime); "
            + "a Pending attempt still inside its time limit resumes even after the window closes. "
            + "409 error codes: ASSIGNMENT_WINDOW_MISSING (no window for the student's class); "
            + "ASSIGNMENT_WINDOW_NOT_OPEN and ASSIGNMENT_WINDOW_CLOSED (data = AssignmentWindowConflictDto with "
            + "startTime/endTime in UTC); ASSIGNMENT_MAX_ATTEMPTS; QUIZ_ATTEMPT_EXPIRED_GRADED (the Pending "
            + "attempt passed ExpiresAt + 60 s, was graded from its saved answers, and data = QuizResultResponseDto; "
            + "call start again to open the next attempt when one is allowed).")]
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
    [Authorize(Roles = "Student, Mentor, Manager, Admin")]
    [SwaggerOperation(
        Summary = "Get in-progress quiz",
        Description = "Returns quiz questions and saved answers for a Pending submission. "
            + "Students may only access their own attempt and must have a module enrollment (any status). "
            + "Mentors may access students in their class. Manager and Admin may access any submission.")]
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
        Description = "Upserts draft answers for a Pending submission. Partial answers are allowed. Requires Student role. "
            + "The class AssignmentWindow closing does not block an attempt already in progress. "
            + "409 error codes: ASSIGNMENT_ATTEMPT_TIME_EXPIRED once ExpiresAt + 60 s grace has passed; "
            + "409 when the submission is no longer Pending.")]
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
            + "Requires Student role. The class AssignmentWindow closing does not block an attempt already in progress. "
            + "409 error codes: ASSIGNMENT_ATTEMPT_TIME_EXPIRED once ExpiresAt + 60 s grace has passed "
            + "(the attempt is later graded from saved answers by start or by the 5-minute background sweep); "
            + "409 when the submission is no longer Pending.")]
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
    [Authorize(Roles = "Student, Mentor, Manager, Admin")]
    [SwaggerOperation(
        Summary = "Get quiz result",
        Description = "Returns the graded result for a submission. "
            + "Students may only access their own result and must have a module enrollment (any status). "
            + "Mentors may access students in their class. Manager and Admin may access any submission. "
            + "409 while the submission is not Graded. A Pending attempt past ExpiresAt + 60 s is graded from its "
            + "saved answers by the next start call or by the background sweep (runs every 5 minutes).")]
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
