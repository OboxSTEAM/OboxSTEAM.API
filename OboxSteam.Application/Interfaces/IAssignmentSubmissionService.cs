using Microsoft.AspNetCore.Http;
using OboxSteam.Application.DTOs.AssignmentSubmissionDTO;

namespace OboxSteam.Application.Interfaces;

/// <summary>
/// Submission and grading flow for FileUpload assignments.
/// Quizzes use <see cref="IQuizAttemptService"/>, retrospectives use
/// <see cref="IRetrospectiveAttemptService"/>, and research milestones use
/// <see cref="IResearchSubmissionService"/>.
/// </summary>
public interface IAssignmentSubmissionService
{
    Task<AssignmentSubmissionResponseDto> SubmitAssignment(SubmitAssignmentRequestDto request);

    Task<AssignmentSubmissionResponseDto> GradeAssignment(
        Guid submissionId,
        GradeAssignmentSubmissionRequestDto request);

    Task<AssignmentSubmissionResponseDto?> GetAssignmentSubmission(Guid submissionId);

    Task<string> UploadAssignmentFile(Guid submissionId, IFormFile file);
}
