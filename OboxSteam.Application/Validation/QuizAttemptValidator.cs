using OboxSteam.Application.Commons;
using OboxSteam.Application.DTOs.QuizDTO;
using OboxSteam.Application.Interfaces;
using OboxSteam.Application.Utils;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;
using OboxSteam.Domain.Interfaces;

namespace OboxSteam.Application.Validation;

/// <summary>
/// Student quiz attempt rules for Mode A (question-bank snapshot per submission).
/// </summary>
public static class QuizAttemptValidator
{
    public const string QuizForbiddenMessage = "Only students can take quizzes.";

    public const string ViewQuizForbiddenMessage =
        "You do not have access to this quiz submission.";

    public static void ValidateAssignmentIdRequired(Guid assignmentId)
    {
        if (assignmentId == Guid.Empty)
            throw ErrorHelper.BadRequest("AssignmentId is required.");
    }

    public static Assignment ValidateAssignmentForQuizStart(Assignment? assignment)
    {
        if (assignment == null || assignment.IsDeleted)
            throw ErrorHelper.NotFound("Assignment not found.");

        if (assignment.AssignmentType != AssignmentType.Quiz)
            throw ErrorHelper.BadRequest("This assignment is not a quiz.");

        if (!assignment.QuestionBankId.HasValue)
            throw ErrorHelper.BadRequest("Quiz assignment requires a linked question bank (Mode A).");

        return assignment;
    }

    public static void ValidateAssignmentAvailability(Assignment assignment, DateTime utcNow)
    {
        if (assignment.AvailableFrom.HasValue && utcNow < assignment.AvailableFrom.Value)
            throw ErrorHelper.Forbidden("Assignment is not yet available.");

        if (assignment.AvailableUntil.HasValue && utcNow > assignment.AvailableUntil.Value)
            throw ErrorHelper.Conflict("Assignment is no longer available.");

        if (assignment.DueDate.HasValue && utcNow > assignment.DueDate.Value)
            throw ErrorHelper.Conflict("Assignment is past due date.");
    }

    public static Submission ValidateSubmissionExists(Submission? submission, Guid submissionId)
    {
        if (submission == null || submission.IsDeleted)
            throw ErrorHelper.NotFound($"Submission with id '{submissionId}' not found.");

        return submission;
    }

    public static void ValidateSubmissionOwnership(Submission submission, Guid studentId)
    {
        if (submission.StudentId != studentId)
            throw ErrorHelper.Forbidden("You do not have access to this submission.");
    }

    public static void ValidateSubmissionPending(Submission submission)
    {
        if (submission.Status != SubmissionStatus.Pending)
            throw ErrorHelper.Conflict("This submission is no longer in progress.");
    }

    public static void ValidateSubmissionGraded(Submission submission)
    {
        if (submission.Status != SubmissionStatus.Graded)
            throw ErrorHelper.Conflict("Quiz results are not available until the submission is graded.");
    }

    public static void ValidateSubmissionHasQuizSnapshot(IReadOnlyList<QuizQuestion> snapshotQuestions)
    {
        if (snapshotQuestions.Count == 0)
            throw ErrorHelper.BadRequest("This submission has no quiz questions.");
    }

    public static async Task<ModuleEnrollment> ValidateActiveModuleEnrollmentAsync(
        IUnitOfWork unitOfWork,
        Guid studentId,
        Assignment assignment)
    {
        var activeEnrollment = await unitOfWork.ModuleEnrollments.FirstOrDefaultAsync(
            me => me.StudentId == studentId
                  && me.ModuleId == assignment.ModuleId
                  && me.Status == EnrollmentStatus.Active
                  && !me.IsDeleted);

        if (activeEnrollment == null)
        {
            throw ErrorHelper.Forbidden(
                "You must have an active module enrollment to access this assignment.");
        }

        return activeEnrollment;
    }

    /// <summary>
    /// When the current user is a Student, requires an active enrollment in the assignment's module.
    /// </summary>
    public static async Task ValidateStudentModuleAccessAsync(
        IUnitOfWork unitOfWork,
        IClaimsService claimsService,
        Assignment assignment)
    {
        var userId = claimsService.GetCurrentUserId;
        if (userId == Guid.Empty)
            return;

        var user = await unitOfWork.Users.GetByIdAsync(userId);
        if (user == null || user.IsDeleted || user.Role != RoleType.Student)
            return;

        await ValidateActiveModuleEnrollmentAsync(unitOfWork, userId, assignment);
    }

    /// <summary>
    /// Authorizes viewing an in-progress or graded quiz submission.
    /// Students: own submission only. Mentors: students in their class for the module's program.
    /// Manager / SuperAdmin: unrestricted.
    /// </summary>
    public static async Task EnsureCanViewQuizSubmissionAsync(
        IUnitOfWork unitOfWork,
        IClaimsService claimsService,
        Submission submission,
        Assignment assignment)
    {
        var userId = claimsService.GetCurrentUserId;
        if (userId == Guid.Empty)
            throw ErrorHelper.Unauthorized("Unauthorized access.");

        var user = await unitOfWork.Users.GetByIdAsync(userId);
        if (user == null || user.IsDeleted)
            throw ErrorHelper.NotFound("Current user not found.");

        if (user.Role == RoleType.Student)
        {
            ValidateSubmissionOwnership(submission, user.Id);
            await ValidateActiveModuleEnrollmentAsync(unitOfWork, user.Id, assignment);
            return;
        }

        if (user.Role is RoleType.SuperAdmin or RoleType.Manager)
            return;

        if (user.Role == RoleType.Mentor)
        {
            var module = await unitOfWork.Modules.GetByIdAsync(assignment.ModuleId);
            if (module == null || module.IsDeleted)
                throw ErrorHelper.NotFound($"Module with id '{assignment.ModuleId}' not found.");

            await MentorScopeValidator.EnsureMentorOwnsStudentInProgramAsync(
                unitOfWork,
                user.Id,
                submission.StudentId,
                module.ProgramId);
            return;
        }

        throw ErrorHelper.Forbidden(ViewQuizForbiddenMessage);
    }

    public static async Task ValidateMaxAttemptsForNewStartAsync(
        IUnitOfWork unitOfWork,
        Assignment assignment,
        Guid studentId)
    {
        var completedAttempts = await unitOfWork.Submissions.GetAllAsync(
            s => s.AssignmentId == assignment.Id
                 && s.StudentId == studentId
                 && !s.IsDeleted
                 && (s.Status == SubmissionStatus.Graded || s.Status == SubmissionStatus.TurnedIn));

        if (completedAttempts.Count >= assignment.MaxAttempts)
        {
            throw ErrorHelper.Conflict(
                $"Maximum number of attempts ({assignment.MaxAttempts}) has been reached for this assignment.");
        }
    }

    public static void ValidateBankQuestionsForDraw(
        Assignment assignment,
        IReadOnlyList<BankQuestion> bankQuestions)
    {
        if (bankQuestions.Count == 0)
            throw ErrorHelper.BadRequest("Question bank has no questions.");

        var drawCount = assignment.QuestionCount ?? bankQuestions.Count;

        if (drawCount <= 0)
            throw ErrorHelper.BadRequest("QuestionCount must be greater than 0.");

        if (drawCount > bankQuestions.Count)
        {
            throw ErrorHelper.BadRequest(
                $"QuestionCount ({drawCount}) exceeds the number of questions in the bank ({bankQuestions.Count}).");
        }
    }

    public static void ValidateSaveDraftRequest(SaveDraftAnswersRequestDto? request)
    {
        if (request == null)
            throw ErrorHelper.BadRequest("Request body is required.");

        if (request.Answers == null)
            throw ErrorHelper.BadRequest("Answers are required.");
    }

    public static void ValidateSubmitRequest(SubmitQuizAnswersRequestDto? request)
    {
        if (request?.Answers == null)
            throw ErrorHelper.BadRequest("Answers are required.");
    }

    public static void ValidateAnswersForDraft(
        IReadOnlyList<QuizQuestion> snapshotQuestions,
        IReadOnlyList<QuizAnswerItemDto> answers)
    {
        ValidateAnswerPayloadStructure(snapshotQuestions, answers, requireAllQuestionsAnswered: false);
    }

    public static void ValidateAnswersForSubmit(
        IReadOnlyList<QuizQuestion> snapshotQuestions,
        IReadOnlyList<QuizAnswerItemDto> answers)
    {
        ValidateAnswerPayloadStructure(snapshotQuestions, answers, requireAllQuestionsAnswered: true);
    }

    private static void ValidateAnswerPayloadStructure(
        IReadOnlyList<QuizQuestion> snapshotQuestions,
        IReadOnlyList<QuizAnswerItemDto> answers,
        bool requireAllQuestionsAnswered)
    {
        var questionMap = snapshotQuestions.ToDictionary(q => q.Id);
        var seenQuestionIds = new HashSet<Guid>();

        foreach (var answer in answers)
        {
            if (answer.QuestionId == Guid.Empty)
                throw ErrorHelper.BadRequest("QuestionId is required for each answer.");

            if (!seenQuestionIds.Add(answer.QuestionId))
                throw ErrorHelper.BadRequest("Duplicate question entries are not allowed in the payload.");

            if (!questionMap.TryGetValue(answer.QuestionId, out var question))
                throw ErrorHelper.BadRequest($"Question '{answer.QuestionId}' does not belong to this submission.");

            var selectedIds = answer.SelectedOptionIds ?? [];
            var distinctOptionIds = selectedIds.Distinct().ToList();

            if (distinctOptionIds.Count != selectedIds.Count)
                throw ErrorHelper.BadRequest("Duplicate option selections are not allowed for a question.");

            if (!QuestionTypeConstants.IsValidCanonical(question.QuestionType))
                throw ErrorHelper.BadRequest($"Unsupported question type '{question.QuestionType}'.");

            var optionIds = question.Options.Where(o => !o.IsDeleted).Select(o => o.Id).ToHashSet();

            foreach (var optionId in distinctOptionIds)
            {
                if (optionId == Guid.Empty)
                    throw ErrorHelper.BadRequest("OptionId is required for each selected answer.");

                if (!optionIds.Contains(optionId))
                {
                    throw ErrorHelper.BadRequest(
                        $"Option '{optionId}' does not belong to question '{answer.QuestionId}'.");
                }
            }

            if (question.QuestionType == QuestionTypeConstants.SingleChoice && distinctOptionIds.Count > 1)
                throw ErrorHelper.BadRequest("Single choice questions allow only one selected option.");
        }

        if (!requireAllQuestionsAnswered)
            return;

        foreach (var question in snapshotQuestions)
        {
            var answer = answers.FirstOrDefault(a => a.QuestionId == question.Id);
            var selectedCount = answer?.SelectedOptionIds?.Count ?? 0;

            if (selectedCount == 0)
            {
                throw ErrorHelper.BadRequest(
                    $"All questions must be answered before submit. Missing answer for question '{question.Id}'.");
            }
        }
    }
}
