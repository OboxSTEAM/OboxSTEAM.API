using OboxSteam.Application.DTOs.QuizDTO;

namespace OboxSteam.Application.Interfaces;

/// <summary>
/// Student quiz attempt flow for Mode A (question-bank snapshot per submission).
/// </summary>
public interface IQuizAttemptService
{
    /// <summary>
    /// Starts a new attempt or resumes an existing <c>Pending</c> submission.
    /// Draws questions from the linked bank and creates per-submission snapshots.
    /// </summary>
    Task<QuizAttemptResponseDto> StartQuiz(Guid assignmentId);

    /// <summary>
    /// Returns the in-progress quiz for a submission (questions + saved answers).
    /// </summary>
    Task<QuizAttemptResponseDto?> GetQuiz(Guid submissionId);

    /// <summary>
    /// Upserts draft answers while submission status is <c>Pending</c>.
    /// </summary>
    Task<SaveDraftAnswersResponseDto> SaveDraftAnswers(
        Guid submissionId,
        SaveDraftAnswersRequestDto request);

    /// <summary>
    /// Final submit: validates answers, auto-grades, sets submission to <c>Graded</c>.
    /// </summary>
    Task<QuizResultResponseDto> SubmitQuiz(
        Guid submissionId,
        SubmitQuizAnswersRequestDto request);

    /// <summary>
    /// Returns the graded result for a submission.
    /// </summary>
    Task<QuizResultResponseDto?> GetQuizResult(Guid submissionId);
}
