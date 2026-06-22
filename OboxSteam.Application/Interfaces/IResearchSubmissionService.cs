using Microsoft.AspNetCore.Http;
using OboxSteam.Application.DTOs.ResearchSubmissionDTO;

namespace OboxSteam.Application.Interfaces;

/// <summary>
/// Research deliverable submission flow for milestone-linked assignments.
/// </summary>
public interface IResearchSubmissionService
{
    /// <summary>
    /// Mentor, Manager, or SuperAdmin opens a <c>Pending</c> submission for a student on a milestone.
    /// </summary>
    Task<ResearchSubmissionResponseDto> StartSubmission(StartResearchSubmissionRequestDto request);

    Task<ResearchSubmissionResponseDto?> GetSubmission(Guid submissionId);

    /// <summary>
    /// Uploads a file to S3 only. Returns <see cref="CreateResearchSubmissionRequestDto"/> fields
    /// (<c>FileUrl</c> or <c>EvidenceUrls</c>) for the client to pass into submit.
    /// </summary>
    Task<CreateResearchSubmissionRequestDto> UploadSubmissionFile(
        Guid submissionId,
        IFormFile file,
        bool isEvidence = false);

    /// <summary>
    /// Student submits research work in a single action (no draft saving).
    /// Resubmission after <c>ReturnedForRevision</c> does not require mentor to reopen.
    /// </summary>
    Task<ResearchSubmissionResponseDto> SubmitResearchWork(
        Guid submissionId,
        CreateResearchSubmissionRequestDto request);

    /// <summary>
    /// Mentor, Manager, or SuperAdmin grades a submission or returns it for revision.
    /// </summary>
    Task<ResearchSubmissionResponseDto> GradeSubmission(
        Guid submissionId,
        GradeResearchSubmissionRequestDto request);
}
