using Microsoft.AspNetCore.Http;
using OboxSteam.Application.DTOs.ResearchSubmissionDTO;

namespace OboxSteam.Application.Interfaces;

/// <summary>
/// Research deliverable submission flow for milestone-linked assignments.
/// Students submit when milestone unlock, required activities, and availability pass.
/// Window extensions use approved <c>AssessmentRecoveryRequest</c> personal deadlines.
/// </summary>
public interface IResearchSubmissionService
{
    Task<ResearchSubmissionResponseDto?> GetSubmission(Guid submissionId);

    /// <summary>
    /// Uploads a file to S3. Lazy-creates a student-owned <c>Pending</c> draft when unlocked.
    /// Returns URLs for the client to pass into submit.
    /// </summary>
    Task<UploadResearchSubmissionResponseDto> UploadSubmissionFile(
        Guid moduleEnrollmentId,
        Guid researchMilestoneId,
        IFormFile file,
        bool isEvidence = false);

    /// <summary>
    /// Student submits research work in a single action (create or turn in existing draft).
    /// Resubmission after <c>ReturnedForRevision</c> does not require mentor to reopen.
    /// </summary>
    Task<ResearchSubmissionResponseDto> SubmitResearchWork(SubmitResearchWorkRequestDto request);

    /// <summary>
    /// Mentor, Manager, or Admin grades a submission or returns it for revision.
    /// </summary>
    Task<ResearchSubmissionResponseDto> GradeSubmission(
        Guid submissionId,
        GradeResearchSubmissionRequestDto request);
}
