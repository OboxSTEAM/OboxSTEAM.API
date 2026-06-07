using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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
