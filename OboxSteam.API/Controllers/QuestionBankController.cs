using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OboxSteam.Application.Commons;
using OboxSteam.Application.DTOs.BankQuestionDTO;
using OboxSteam.Application.DTOs.QuestionBankDTO;
using OboxSteam.Application.Interfaces;
using OboxSteam.Application.Utils;
using Swashbuckle.AspNetCore.Annotations;

namespace OboxSteam.API.Controllers;

[Route("api/question-banks")]
[ApiController]
public class QuestionBankController : ControllerBase
{
    private readonly IQuestionBankService _questionBankService;
    private readonly IBankQuestionService _bankQuestionService;

    public QuestionBankController(
        IQuestionBankService questionBankService,
        IBankQuestionService bankQuestionService)
    {
        _questionBankService = questionBankService;
        _bankQuestionService = bankQuestionService;
    }

    // =========================================================================
    // GET ALL  —  GET /api/question-banks
    // =========================================================================

    /// <summary>
    /// Get a paginated list of question banks with program/module/course context.
    /// </summary>
    [HttpGet]
    [SwaggerOperation(
        Summary = "Get all question banks",
        Description = "Retrieve a paginated list of question banks, each carrying program/module/course " +
                      "context for the Edit deep-link. Supports search, filter, and sort options.")]
    [ProducesResponseType(typeof(ApiResult<Pagination<QuestionBankListItemDto>>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 400)]
    [ProducesResponseType(typeof(ApiResult<object>), 500)]
    public async Task<IActionResult> GetAllQuestionBanks(
        [FromQuery, SwaggerParameter(Description = "Search by bank, course, program, or module name (optional)")] string? search = null,
        [FromQuery, SwaggerParameter(Description = "Sort by field: name, createdAt, updatedAt, questionCount, courseName, programName (optional)")] string? sortBy = null,
        [FromQuery, SwaggerParameter(Description = "Sort in descending order? Default: true")] bool isDescending = true,
        [FromQuery, SwaggerParameter(Description = "Page number, starting from 1")] int page = 1,
        [FromQuery, SwaggerParameter(Description = "Number of items per page")] int pageSize = 10,
        [FromQuery, SwaggerParameter(Description = "Filter by course (optional)")] Guid? courseId = null,
        [FromQuery, SwaggerParameter(Description = "Filter by program (optional)")] Guid? programId = null,
        [FromQuery, SwaggerParameter(Description = "Filter by module (optional)")] Guid? moduleId = null)
    {
        if (page < 1 || pageSize < 1)
            return BadRequest(ApiResult<object>.Failure("400", "Invalid pagination parameters."));

        var result = await _questionBankService.GetAllQuestionBanks(
            search, sortBy, isDescending, page, pageSize,
            courseId, programId, moduleId);

        return Ok(ApiResult<Pagination<QuestionBankListItemDto>>.Success(
            result, "200", "Question banks retrieved successfully."));
    }

    [HttpGet("{questionBankId:guid}")]
    [SwaggerOperation(
        Summary = "Get question bank by ID",
        Description = "Retrieve a question bank by its ID.")]
    [ProducesResponseType(typeof(ApiResult<QuestionBankResponseDto>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    public async Task<IActionResult> GetQuestionBankById(Guid questionBankId)
    {
        var result = await _questionBankService.GetQuestionBankById(questionBankId);
        if (result == null)
            return NotFound(ApiResult<object>.Failure("404", "Question bank not found."));

        return Ok(ApiResult<QuestionBankResponseDto>.Success(result, "200", "Question bank retrieved successfully."));
    }

    [HttpPost]
    [Authorize(Roles = "SuperAdmin,Manager")]
    [SwaggerOperation(
        Summary = "Create a question bank",
        Description = "Creates a new question bank for a course. Requires SuperAdmin or Manager role.")]
    [ProducesResponseType(typeof(ApiResult<QuestionBankResponseDto>), 201)]
    [ProducesResponseType(typeof(ApiResult<object>), 400)]
    [ProducesResponseType(typeof(ApiResult<object>), 401)]
    [ProducesResponseType(typeof(ApiResult<object>), 403)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    [ProducesResponseType(typeof(ApiResult<object>), 409)]
    public async Task<IActionResult> CreateQuestionBank(
        [FromBody, SwaggerParameter("New question bank data")] CreateQuestionBankRequestDto request)
    {
        var result = await _questionBankService.CreateQuestionBank(request);

        return CreatedAtAction(
            nameof(GetQuestionBankById),
            new { questionBankId = result.Id },
            ApiResult<QuestionBankResponseDto>.Success(result, "201", "Question bank created successfully."));
    }

    [HttpDelete("{questionBankId:guid}")]
    [Authorize(Roles = "SuperAdmin,Manager")]
    [SwaggerOperation(
        Summary = "Delete a question bank",
        Description = "Soft-deletes a question bank and its questions. Requires SuperAdmin or Manager role.")]
    [ProducesResponseType(typeof(ApiResult<bool>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 401)]
    [ProducesResponseType(typeof(ApiResult<object>), 403)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    [ProducesResponseType(typeof(ApiResult<object>), 409)]
    public async Task<IActionResult> DeleteQuestionBank(Guid questionBankId)
    {
        var result = await _questionBankService.DeleteQuestionBank(questionBankId);
        if (!result)
            return NotFound(ApiResult<object>.Failure("404", "Question bank not found."));

        return Ok(ApiResult<bool>.Success(result, "200", "Question bank deleted successfully."));
    }

    [HttpDelete("{questionBankId:guid}/questions/{questionId:guid}")]
    [Authorize(Roles = "SuperAdmin,Manager")]
    [SwaggerOperation(
        Summary = "Delete a bank question",
        Description = "Soft-deletes a question and its options from a question bank. Requires SuperAdmin or Manager role.")]
    [ProducesResponseType(typeof(ApiResult<bool>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 401)]
    [ProducesResponseType(typeof(ApiResult<object>), 403)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    public async Task<IActionResult> DeleteBankQuestion(Guid questionBankId, Guid questionId)
    {
        var result = await _bankQuestionService.DeleteBankQuestion(questionBankId, questionId);
        if (!result)
            return NotFound(ApiResult<object>.Failure("404", "Bank question not found."));

        return Ok(ApiResult<bool>.Success(result, "200", "Bank question deleted successfully."));
    }

    [HttpPost("{questionBankId:guid}/import")]
    [Authorize(Roles = "SuperAdmin,Manager")]
    [RequestSizeLimit(5 * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 5 * 1024 * 1024)]
    [SwaggerOperation(
        Summary = "Import questions from CSV",
        Description = "Parses a CSV file and imports valid question rows into the specified question bank. " +
                      "Invalid rows are skipped and returned in the error list. Requires SuperAdmin or Manager role.")]
    [ProducesResponseType(typeof(ApiResult<ImportBankQuestionsResultDto>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 400)]
    [ProducesResponseType(typeof(ApiResult<object>), 401)]
    [ProducesResponseType(typeof(ApiResult<object>), 403)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    public async Task<IActionResult> ImportQuestions(Guid questionBankId, IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest(ApiResult<object>.Failure("400", "CSV file is required."));

        var result = await _questionBankService.ImportQuestionsFromCsv(questionBankId, file);

        return Ok(ApiResult<ImportBankQuestionsResultDto>.Success(
            result,
            "200",
            $"Imported {result.ImportedCount}/{result.TotalRows} questions."));
    }
}
