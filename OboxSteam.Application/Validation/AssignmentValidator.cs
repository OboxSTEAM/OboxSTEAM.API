using OboxSteam.Application.Utils;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;
using OboxSteam.Domain.Interfaces;

namespace OboxSteam.Application.Validation;

/// <summary>
/// Assignment-specific business rules and input validation.
/// </summary>
public static class AssignmentValidator
{
    public static void ValidateRequiredFields(string code, string title)
    {
        if (string.IsNullOrWhiteSpace(code))
            throw ErrorHelper.BadRequest("Code is required.");

        if (string.IsNullOrWhiteSpace(title))
            throw ErrorHelper.BadRequest("Title is required.");
    }

    public static void ValidateCommonFields(
        int maxPoints,
        decimal passScore,
        int maxAttempts,
        int? timeLimitMinutes)
    {
        if (maxPoints <= 0)
            throw ErrorHelper.BadRequest("MaxPoints must be greater than 0.");

        if (passScore < 0)
            throw ErrorHelper.BadRequest("PassScore cannot be negative.");

        if (passScore > maxPoints)
            throw ErrorHelper.BadRequest("PassScore cannot exceed MaxPoints.");

        if (maxAttempts < 1)
            throw ErrorHelper.BadRequest("MaxAttempts must be at least 1.");

        if (timeLimitMinutes.HasValue && timeLimitMinutes.Value <= 0)
            throw ErrorHelper.BadRequest("TimeLimitMinutes must be greater than 0.");
    }

    public static Module ValidateModuleExists(Module? module)
    {
        if (module == null || module.IsDeleted)
            throw ErrorHelper.NotFound("Module not found.");

        return module;
    }

    public static async Task ValidateCourseBelongsToModuleAsync(
        IUnitOfWork unitOfWork,
        Guid? courseId,
        Guid moduleId)
    {
        if (!courseId.HasValue)
            return;

        var course = await unitOfWork.Courses.GetByIdAsync(courseId.Value);
        ValidateCourseBelongsToModule(course, courseId.Value, moduleId);
    }

    public static void ValidateCourseBelongsToModule(Course? course, Guid courseId, Guid moduleId)
    {
        if (course == null || course.IsDeleted)
            throw ErrorHelper.NotFound("Course not found.");

        if (course.ModuleId != moduleId)
            throw ErrorHelper.BadRequest("Course does not belong to the specified module.");
    }

    public static void ValidateCanDelete(int submissionCount)
    {
        if (submissionCount > 0)
        {
            throw ErrorHelper.Conflict(
                "Cannot delete an assignment that has existing submissions.");
        }
    }

    public static async Task ValidateQuizConfigAsync(
        IUnitOfWork unitOfWork,
        AssignmentType assignmentType,
        Guid? questionBankId,
        Guid? courseId,
        Guid moduleId,
        int easyPercent,
        int mediumPercent,
        int hardPercent,
        int? questionCount)
    {
        if (assignmentType != AssignmentType.Quiz)
        {
            if (questionBankId.HasValue)
                throw ErrorHelper.BadRequest("Question bank can only be linked to quiz assignments.");

            return;
        }

        if (!questionBankId.HasValue)
            return;

        var questionBank = await unitOfWork.QuestionBanks.GetByIdAsync(questionBankId.Value);
        if (questionBank == null || questionBank.IsDeleted)
            throw ErrorHelper.NotFound("Question bank not found.");

        var bankCourse = await unitOfWork.Courses.GetByIdAsync(questionBank.CourseId);
        if (bankCourse == null || bankCourse.IsDeleted)
            throw ErrorHelper.BadRequest("Question bank course not found.");

        if (bankCourse.ModuleId != moduleId)
            throw ErrorHelper.BadRequest("Question bank does not belong to the assignment module.");

        if (courseId.HasValue && questionBank.CourseId != courseId.Value)
            throw ErrorHelper.BadRequest("Question bank does not belong to the assignment course.");

        ValidateDifficultyPercents(easyPercent, mediumPercent, hardPercent);

        if (!questionCount.HasValue)
            return;

        if (questionCount.Value <= 0)
            throw ErrorHelper.BadRequest("QuestionCount must be greater than 0.");

        var bankQuestions = await unitOfWork.BankQuestions.GetAllAsync(
            q => q.QuestionBankId == questionBankId.Value && !q.IsDeleted);

        if (questionCount.Value > bankQuestions.Count)
        {
            throw ErrorHelper.BadRequest(
                $"QuestionCount ({questionCount.Value}) exceeds the number of questions in the bank ({bankQuestions.Count}).");
        }
    }

    public static void ValidateDifficultyPercents(int easyPercent, int mediumPercent, int hardPercent)
    {
        if (easyPercent < 0 || mediumPercent < 0 || hardPercent < 0)
            throw ErrorHelper.BadRequest("Difficulty percentages cannot be negative.");

        if (easyPercent + mediumPercent + hardPercent != 100)
        {
            throw ErrorHelper.BadRequest(
                "EasyPercent, MediumPercent, and HardPercent must sum to 100.");
        }
    }
}
