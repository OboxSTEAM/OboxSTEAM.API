using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using OboxSteam.Application.Commons;
using OboxSteam.Application.DTOs.BankQuestionDTO;
using OboxSteam.Application.DTOs.QuestionBankDTO;
using OboxSteam.Application.Interfaces;
using OboxSteam.Application.Utils;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Interfaces;

namespace OboxSteam.Application.Services;

public sealed class QuestionBankService : IQuestionBankService
{
    private const long MaxCsvFileSize = 5L * 1024 * 1024;

    private readonly IClaimsService _claimsService;
    private readonly ICsvQuestionParserService _csvParser;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<QuestionBankService> _logger;

    public QuestionBankService(
        IClaimsService claimsService,
        ICsvQuestionParserService csvParser,
        IUnitOfWork unitOfWork,
        ILogger<QuestionBankService> logger)
    {
        _claimsService = claimsService;
        _csvParser = csvParser;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<QuestionBankResponseDto> CreateQuestionBank(CreateQuestionBankRequestDto request)
    {
        var userId = _claimsService.GetCurrentUserId;
        _logger.LogInformation(
            "CreateQuestionBank started by UserId={UserId} for CourseId={CourseId}",
            userId, request.CourseId);

        if (string.IsNullOrWhiteSpace(request.Name))
            throw ErrorHelper.BadRequest("Name is required.");

        var course = await _unitOfWork.Courses.GetByIdAsync(request.CourseId);
        if (course == null || course.IsDeleted)
            throw ErrorHelper.NotFound("Course not found.");

        var duplicate = await _unitOfWork.QuestionBanks.FirstOrDefaultAsync(
            qb => qb.CourseId == request.CourseId
                  && qb.Name.ToLower() == request.Name.Trim().ToLower()
                  && !qb.IsDeleted);

        if (duplicate != null)
            throw ErrorHelper.Conflict("A question bank with this name already exists in the course.");

        var now = DateTime.UtcNow;
        var questionBank = new QuestionBank
        {
            Id = Guid.NewGuid(),
            CourseId = request.CourseId,
            Name = request.Name.Trim(),
            Description = request.Description?.Trim(),
            CreatedAt = now,
            CreatedBy = userId,
            IsDeleted = false
        };

        await _unitOfWork.QuestionBanks.AddAsync(questionBank);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation(
            "CreateQuestionBank completed. QuestionBankId={QuestionBankId}",
            questionBank.Id);

        return MapToResponseDto(questionBank);
    }

    public async Task<QuestionBankResponseDto?> GetQuestionBankById(Guid questionBankId)
    {
        var questionBank = await _unitOfWork.QuestionBanks.GetByIdAsync(questionBankId);
        if (questionBank == null || questionBank.IsDeleted)
            return null;

        return MapToResponseDto(questionBank);
    }

    public async Task<bool> DeleteQuestionBank(Guid questionBankId)
    {
        var userId = _claimsService.GetCurrentUserId;
        _logger.LogInformation(
            "DeleteQuestionBank started by UserId={UserId} for QuestionBankId={QuestionBankId}",
            userId, questionBankId);

        var questionBank = await _unitOfWork.QuestionBanks.GetByIdAsync(questionBankId);
        if (questionBank == null || questionBank.IsDeleted)
            return false;

        var linkedAssignments = await _unitOfWork.Assignments.GetAllAsync(
            a => a.QuestionBankId == questionBankId && !a.IsDeleted);

        if (linkedAssignments.Count > 0)
        {
            throw ErrorHelper.Conflict(
                "Cannot delete a question bank that is linked to active assignments.");
        }

        var questions = await _unitOfWork.BankQuestions.GetAllAsync(
            q => q.QuestionBankId == questionBankId && !q.IsDeleted,
            q => q.Options);

        await _unitOfWork.QuestionBanks.SoftRemove(questionBank);

        if (questions.Count > 0)
        {
            await _unitOfWork.BankQuestions.SoftRemoveRange(questions);

            var options = questions
                .SelectMany(q => q.Options)
                .Where(o => !o.IsDeleted)
                .ToList();

            if (options.Count > 0)
                await _unitOfWork.BankQuestionOptions.SoftRemoveRange(options);
        }

        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation(
            "DeleteQuestionBank completed. QuestionBankId={QuestionBankId}",
            questionBankId);

        return true;
    }

    public async Task<ImportBankQuestionsResultDto> ImportQuestionsFromCsv(Guid questionBankId, IFormFile file)    {
        var userId = _claimsService.GetCurrentUserId;
        _logger.LogInformation(
            "ImportQuestionsFromCsv started by UserId={UserId} for QuestionBankId={QuestionBankId}",
            userId, questionBankId);

        ValidateFile(file);

        var questionBank = await _unitOfWork.QuestionBanks.GetByIdAsync(questionBankId);
        if (questionBank == null || questionBank.IsDeleted)
            throw ErrorHelper.NotFound("Question bank not found.");

        await using var stream = file.OpenReadStream();
        var parsedRows = await _csvParser.ParseAsync(stream);

        var result = new ImportBankQuestionsResultDto
        {
            TotalRows = parsedRows.Count
        };

        var existingQuestions = await _unitOfWork.BankQuestions.GetAllAsync(
            q => q.QuestionBankId == questionBankId && !q.IsDeleted);

        var nextOrderIndex = existingQuestions.Count > 0
            ? existingQuestions.Max(q => q.OrderIndex) + 1
            : 1;

        var questionsToInsert = new List<BankQuestion>();
        var optionsToInsert = new List<BankQuestionOption>();
        var now = DateTime.UtcNow;

        foreach (var row in parsedRows)
        {
            if (row.ParseErrors.Count > 0)
            {
                result.Errors.Add(new ImportRowErrorDto
                {
                    RowNumber = row.RowNumber,
                    QuestionText = TruncatePreview(row.QuestionText),
                    Error = string.Join(" ", row.ParseErrors)
                });
                continue;
            }

            var validationError = ValidateRow(row);
            if (validationError != null)
            {
                result.Errors.Add(new ImportRowErrorDto
                {
                    RowNumber = row.RowNumber,
                    QuestionText = TruncatePreview(row.QuestionText),
                    Error = validationError
                });
                continue;
            }

            if (!QuestionTypeConstants.TryNormalizeFromCsv(row.QuestionType, out var normalizedType))
            {
                result.Errors.Add(new ImportRowErrorDto
                {
                    RowNumber = row.RowNumber,
                    QuestionText = TruncatePreview(row.QuestionText),
                    Error = "QuestionType must be singlechoice, multichoice, or truefalse."
                });
                continue;
            }

            if (!DifficultyLevelMapper.TryMapFromCsv(row.Difficulty, out var difficultyLevel))
            {
                result.Errors.Add(new ImportRowErrorDto
                {
                    RowNumber = row.RowNumber,
                    QuestionText = TruncatePreview(row.QuestionText),
                    Error = "Difficulty must be easy, medium, or hard."
                });
                continue;
            }

            var questionId = Guid.NewGuid();
            var question = new BankQuestion
            {
                Id = questionId,
                QuestionBankId = questionBankId,
                QuestionText = row.QuestionText.Trim(),
                QuestionType = normalizedType,
                Points = row.Points,
                DifficultyLevel = difficultyLevel,
                OrderIndex = nextOrderIndex++,
                CreatedAt = now,
                CreatedBy = userId,
                IsDeleted = false
            };

            questionsToInsert.Add(question);

            var questionOptions = new List<BankQuestionOption>();
            foreach (var option in row.Options)
            {
                var optionEntity = new BankQuestionOption
                {
                    Id = Guid.NewGuid(),
                    BankQuestionId = questionId,
                    OptionText = option.OptionText.Trim(),
                    IsCorrect = option.IsCorrect,
                    CreatedAt = now,
                    CreatedBy = userId,
                    IsDeleted = false
                };

                questionOptions.Add(optionEntity);
                optionsToInsert.Add(optionEntity);
            }

            result.ImportedQuestions.Add(MapToResponseDto(question, questionOptions));
        }

        if (questionsToInsert.Count > 0)
        {
            await _unitOfWork.BankQuestions.AddRangeAsync(questionsToInsert);
            await _unitOfWork.BankQuestionOptions.AddRangeAsync(optionsToInsert);
            await _unitOfWork.SaveChangesAsync();
        }

        result.ImportedCount = questionsToInsert.Count;
        result.FailedCount = result.Errors.Count;

        _logger.LogInformation(
            "ImportQuestionsFromCsv completed for QuestionBankId={QuestionBankId}. Imported={Imported}, Failed={Failed}",
            questionBankId, result.ImportedCount, result.FailedCount);

        return result;
    }

    private static void ValidateFile(IFormFile file)
    {
        if (file == null || file.Length == 0)
            throw ErrorHelper.BadRequest("CSV file is required.");

        if (file.Length > MaxCsvFileSize)
            throw ErrorHelper.BadRequest("CSV file size must not exceed 5 MB.");

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (extension != ".csv")
            throw ErrorHelper.BadRequest("Only .csv files are supported.");
    }

    private static string? ValidateRow(CsvBankQuestionRowDto row)
    {
        if (string.IsNullOrWhiteSpace(row.QuestionText))
            return "QuestionText is required.";

        if (string.IsNullOrWhiteSpace(row.QuestionType))
            return "QuestionType is required.";

        if (string.IsNullOrWhiteSpace(row.Difficulty))
            return "Difficulty is required.";

        if (row.Points <= 0)
            return "Points must be greater than 0.";

        if (row.Options.Count < 2)
            return "At least 2 options are required.";

        var correctCount = row.Options.Count(o => o.IsCorrect);

        if (QuestionTypeConstants.IsTrueFalseCsvType(row.QuestionType))
        {
            if (row.Options.Count != 2)
                return "True/false questions must have exactly 2 options.";

            if (correctCount != 1)
                return "True/false questions must have exactly 1 correct option.";

            return null;
        }

        if (!QuestionTypeConstants.TryNormalizeFromCsv(row.QuestionType, out var normalizedType))
            return "QuestionType must be singlechoice, multichoice, or truefalse.";

        return BankQuestionValidationHelper.ValidateQuestionRules(
            normalizedType,
            row.Options.Select(o => (o.OptionText, o.IsCorrect)).ToList());
    }

    private static string TruncatePreview(string text, int maxLength = 80)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        var trimmed = text.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength] + "...";
    }

    private static BankQuestionResponseDto MapToResponseDto(
        BankQuestion question,
        List<BankQuestionOption> options)
    {
        return new BankQuestionResponseDto
        {
            Id = question.Id,
            QuestionBankId = question.QuestionBankId,
            QuestionText = question.QuestionText,
            QuestionType = question.QuestionType,
            Points = question.Points,
            DifficultyLevel = question.DifficultyLevel,
            OrderIndex = question.OrderIndex,
            CreatedAt = question.CreatedAt,
            UpdatedAt = question.UpdatedAt,
            Options = options
                .Select(o => new BankQuestionOptionResponseDto
                {
                    Id = o.Id,
                    BankQuestionId = o.BankQuestionId,
                    OptionText = o.OptionText,
                    IsCorrect = o.IsCorrect,
                    CreatedAt = o.CreatedAt,
                    UpdatedAt = o.UpdatedAt
                })
                .ToList()
        };
    }

    private static QuestionBankResponseDto MapToResponseDto(QuestionBank questionBank)
    {
        return new QuestionBankResponseDto
        {
            Id = questionBank.Id,
            CourseId = questionBank.CourseId,
            Name = questionBank.Name,
            Description = questionBank.Description,
            CreatedAt = questionBank.CreatedAt,
            UpdatedAt = questionBank.UpdatedAt
        };
    }
}
