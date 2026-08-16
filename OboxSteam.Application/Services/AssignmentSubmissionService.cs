using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using OboxSteam.Application.Commons;
using OboxSteam.Application.DTOs.AssignmentSubmissionDTO;
using OboxSteam.Application.Interfaces;
using OboxSteam.Application.Utils;
using OboxSteam.Application.Validation;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;
using OboxSteam.Domain.Interfaces;

namespace OboxSteam.Application.Services;

/// <summary>
/// Submission and grading flow for FileUpload assignments.
/// A student turns in work; a mentor/manager grades it with a flexible pass score. Grading
/// recalculates module and program progress so the assignment counts toward completion.
/// </summary>
public sealed class AssignmentSubmissionService : IAssignmentSubmissionService
{
    private const string SubmissionFolder = "submissions";

    private const string SubmitForbiddenMessage = "Only students can submit assignment work.";

    private static readonly HashSet<AssignmentType> SupportedTypes =
    [
        AssignmentType.FileUpload
    ];

    private readonly IClaimsService _claimsService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IBlobService _blobService;
    private readonly ICertificateService _certificateService;
    private readonly ILogger<AssignmentSubmissionService> _logger;

    public AssignmentSubmissionService(
        IClaimsService claimsService,
        IUnitOfWork unitOfWork,
        IBlobService blobService,
        ICertificateService certificateService,
        ILogger<AssignmentSubmissionService> logger)
    {
        _claimsService = claimsService;
        _unitOfWork = unitOfWork;
        _blobService = blobService;
        _certificateService = certificateService;
        _logger = logger;
    }

    public async Task<AssignmentSubmissionResponseDto> SubmitAssignment(SubmitAssignmentRequestDto request)
    {
        var student = await EnrollmentAccessValidator.GetCurrentStudentForEnrollAsync(
            _unitOfWork,
            _claimsService,
            SubmitForbiddenMessage);

        if (string.IsNullOrWhiteSpace(request.ContentText) && string.IsNullOrWhiteSpace(request.FileUrl))
        {
            throw ErrorHelper.BadRequest("At least one of ContentText or FileUrl is required.");
        }

        var assignment = await _unitOfWork.Assignments.GetByIdAsync(request.AssignmentId);
        if (assignment == null || assignment.IsDeleted)
        {
            throw ErrorHelper.NotFound($"Assignment with id '{request.AssignmentId}' not found.");
        }

        await EnsureNonResearchAssignmentAsync(assignment);

        var enrollment = await QuizAttemptValidator.ValidateActiveModuleEnrollmentAsync(
            _unitOfWork,
            student.Id,
            assignment);

        var now = DateTime.UtcNow;
        var (_, personalUntil) = await AssessmentAttemptPolicy.GetPersonalWindowAsync(
            _unitOfWork,
            student.Id,
            assignment.Id,
            enrollment.Id);
        ResearchSubmissionValidator.ValidateAssignmentAvailability(assignment, now, personalUntil);

        var submission = await _unitOfWork.Submissions.FirstOrDefaultAsync(
            s => s.AssignmentId == assignment.Id
                 && s.ModuleEnrollmentId == enrollment.Id
                 && !s.IsDeleted);

        if (submission == null)
        {
            submission = new Submission
            {
                Id = Guid.NewGuid(),
                Code = ResearchSubmissionValidator.GenerateSubmissionCode(),
                AssignmentId = assignment.Id,
                StudentId = student.Id,
                ModuleEnrollmentId = enrollment.Id,
                AttemptNumber = 1,
                Status = SubmissionStatus.TurnedIn,
                ContentText = request.ContentText?.Trim(),
                FileUrl = request.FileUrl?.Trim(),
                SubmittedAt = now,
                CreatedAt = now,
                CreatedBy = student.Id,
                IsDeleted = false
            };

            await _unitOfWork.Submissions.AddAsync(submission);
        }
        else
        {
            if (submission.Status == SubmissionStatus.Graded)
            {
                var module = await _unitOfWork.Modules.GetByIdAsync(assignment.ModuleId);
                var passed = submission.AssignedGrade.HasValue
                    && submission.AssignedGrade.Value >= assignment.PassScore;
                if (passed)
                {
                    throw ErrorHelper.Conflict(
                        "This assignment has already been graded for the current module attempt.");
                }

                await ResearchSubmissionValidator.ValidateMaxAttemptsNotExceededAsync(
                    _unitOfWork,
                    assignment,
                    student.Id,
                    ResearchSubmissionValidator.GetNextAttemptNumber(submission),
                    enrollment.Id);
            }
            else if (submission.Status is SubmissionStatus.Pending or SubmissionStatus.ReturnedForRevision)
            {
                await ResearchSubmissionValidator.ValidateMaxAttemptsNotExceededAsync(
                    _unitOfWork,
                    assignment,
                    student.Id,
                    ResearchSubmissionValidator.GetNextAttemptNumber(submission),
                    enrollment.Id);
            }
            else if (submission.Status == SubmissionStatus.TurnedIn)
            {
                throw ErrorHelper.Conflict("Submission is pending mentor review.");
            }

            submission.ContentText = request.ContentText?.Trim();
            submission.FileUrl = request.FileUrl?.Trim();
            submission.AttemptNumber = ResearchSubmissionValidator.GetNextAttemptNumber(submission);
            submission.Status = SubmissionStatus.TurnedIn;
            submission.AssignedGrade = null;
            submission.MentorFeedback = null;
            submission.GradedAt = null;
            submission.VerifiedBy = null;
            submission.SubmittedAt = now;
            submission.UpdatedAt = now;
            submission.UpdatedBy = student.Id;

            await _unitOfWork.Submissions.Update(submission);
        }

        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation(
            "SubmitAssignment turned in. SubmissionId={SubmissionId}, AssignmentId={AssignmentId}, StudentId={StudentId}",
            submission.Id,
            assignment.Id,
            student.Id);

        return MapToDto(submission, assignment);
    }

    public async Task<AssignmentSubmissionResponseDto> GradeAssignment(
        Guid submissionId,
        GradeAssignmentSubmissionRequestDto request)
    {
        var submission = await _unitOfWork.Submissions.GetByIdAsync(submissionId);
        ResearchSubmissionValidator.ValidateSubmissionExists(submission, submissionId);

        if (submission!.ResearchMilestoneId.HasValue)
        {
            throw ErrorHelper.BadRequest(
                "This is a research submission. Use the research submission grading endpoint.");
        }

        ResearchSubmissionValidator.ValidateSubmissionAwaitingGrade(submission);

        var assignment = await _unitOfWork.Assignments.GetByIdAsync(submission.AssignmentId);
        if (assignment == null || assignment.IsDeleted)
        {
            throw ErrorHelper.NotFound($"Assignment with id '{submission.AssignmentId}' not found.");
        }

        var grader = await ResearchSubmissionValidator.EnsureCanGradeSubmissionAsync(
            _unitOfWork,
            _claimsService,
            assignment.ModuleId,
            submission.StudentId);

        if (request.AssignedGrade < 0 || request.AssignedGrade > assignment.MaxPoints)
        {
            throw ErrorHelper.BadRequest($"AssignedGrade must be between 0 and {assignment.MaxPoints}.");
        }

        var now = DateTime.UtcNow;
        submission.AssignedGrade = request.AssignedGrade;
        submission.MentorFeedback = request.MentorFeedback?.Trim();
        submission.VerifiedBy = grader.Id;
        submission.GradedAt = now;
        submission.UpdatedAt = now;
        submission.UpdatedBy = grader.Id;
        submission.Status = request.ReturnForRevision
            ? SubmissionStatus.ReturnedForRevision
            : SubmissionStatus.Graded;

        await _unitOfWork.Submissions.Update(submission);
        await _unitOfWork.SaveChangesAsync();

        await RecalculateEnrollmentProgressAsync(submission);

        _logger.LogInformation(
            "GradeAssignment completed. SubmissionId={SubmissionId}, Status={Status}, GradedBy={GradedBy}",
            submission.Id,
            submission.Status,
            grader.Id);

        return MapToDto(submission, assignment);
    }

    public async Task<AssignmentSubmissionResponseDto?> GetAssignmentSubmission(Guid submissionId)
    {
        var submission = await _unitOfWork.Submissions.GetByIdAsync(submissionId);
        if (submission == null || submission.IsDeleted)
        {
            return null;
        }

        if (submission.ResearchMilestoneId.HasValue)
        {
            throw ErrorHelper.BadRequest(
                "This is a research submission. Use the research submission endpoint.");
        }

        await ResearchSubmissionValidator.EnsureCanViewSubmissionAsync(
            _unitOfWork,
            _claimsService,
            submission);

        var assignment = await _unitOfWork.Assignments.GetByIdAsync(submission.AssignmentId);
        if (assignment == null || assignment.IsDeleted)
        {
            throw ErrorHelper.NotFound($"Assignment with id '{submission.AssignmentId}' not found.");
        }

        return MapToDto(submission, assignment);
    }

    public async Task<string> UploadAssignmentFile(Guid submissionId, IFormFile file)
    {
        var student = await EnrollmentAccessValidator.GetCurrentStudentForEnrollAsync(
            _unitOfWork,
            _claimsService,
            SubmitForbiddenMessage);

        var submission = await _unitOfWork.Submissions.GetByIdAsync(submissionId);
        ResearchSubmissionValidator.ValidateSubmissionExists(submission, submissionId);

        if (submission!.ResearchMilestoneId.HasValue)
        {
            throw ErrorHelper.BadRequest(
                "This is a research submission. Use the research submission upload endpoint.");
        }

        ResearchSubmissionValidator.ValidateSubmissionOwnership(submission, student.Id);
        ResearchSubmissionValidator.ValidateUploadFile(file);

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        var folder = $"{SubmissionFolder}/{submissionId}";
        var fileName = $"{submissionId}_{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}{extension}";
        var s3Key = $"{folder}/{fileName}";

        await using var stream = file.OpenReadStream();
        await _blobService.UploadFileAsync(fileName, stream, folder);
        var fileUrl = await _blobService.GetPreviewUrlAsync(s3Key);

        _logger.LogInformation(
            "UploadAssignmentFile completed. SubmissionId={SubmissionId}, StudentId={StudentId}",
            submissionId,
            student.Id);

        return fileUrl;
    }

    private async Task EnsureNonResearchAssignmentAsync(Assignment assignment)
    {
        if (assignment.AssignmentType == AssignmentType.Quiz)
        {
            throw ErrorHelper.BadRequest("Quiz assignments are submitted through the quiz endpoints.");
        }

        if (assignment.AssignmentType == AssignmentType.Retrospective)
        {
            throw ErrorHelper.BadRequest(
                "Retrospective assignments are submitted through the retrospective endpoints.");
        }

        if (!SupportedTypes.Contains(assignment.AssignmentType))
        {
            throw ErrorHelper.BadRequest(
                $"Assignment type '{assignment.AssignmentType}' is not supported by this endpoint.");
        }

        var researchMilestone = await _unitOfWork.ResearchMilestones.FirstOrDefaultAsync(
            rm => rm.AssignmentId == assignment.Id && !rm.IsDeleted);

        if (researchMilestone != null)
        {
            throw ErrorHelper.BadRequest(
                "This assignment is a research milestone deliverable. Use the research submission endpoints.");
        }
    }

    private async Task RecalculateEnrollmentProgressAsync(Submission submission)
    {
        if (!submission.ModuleEnrollmentId.HasValue)
        {
            return;
        }

        var moduleEnrollment = await _unitOfWork.ModuleEnrollments.GetByIdAsync(submission.ModuleEnrollmentId.Value);
        if (moduleEnrollment == null || moduleEnrollment.IsDeleted)
        {
            return;
        }

        await ActivityProgressCalculationHelper.RecalculateModuleProgressAsync(_unitOfWork, moduleEnrollment);

        if (moduleEnrollment.ProgramEnrollmentId.HasValue)
        {
            await ActivityProgressCalculationHelper.RecalculateProgramProgressAsync(
                _unitOfWork,
                moduleEnrollment.ProgramEnrollmentId.Value,
                moduleEnrollment);
            await TryEnsureProgramCertificateAsync(moduleEnrollment.ProgramEnrollmentId.Value);
        }

        await _unitOfWork.SaveChangesAsync();
    }

    private async Task TryEnsureProgramCertificateAsync(Guid programEnrollmentId)
    {
        try
        {
            // Internal: mentor grading must still be able to issue on program completion.
            await _certificateService.EnsureProgramCertificateInternalAsync(programEnrollmentId);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "[TryEnsureProgramCertificateAsync] Failed for enrollment {EnrollmentId}. Learning progress was not rolled back.",
                programEnrollmentId);
        }
    }

    private static AssignmentSubmissionResponseDto MapToDto(Submission submission, Assignment assignment)
    {
        bool? passed = submission.Status == SubmissionStatus.Graded && submission.AssignedGrade.HasValue
            ? submission.AssignedGrade.Value >= assignment.PassScore
            : null;

        return new AssignmentSubmissionResponseDto
        {
            Id = submission.Id,
            Code = submission.Code,
            AssignmentId = submission.AssignmentId,
            AssignmentType = assignment.AssignmentType,
            ModuleEnrollmentId = submission.ModuleEnrollmentId,
            StudentId = submission.StudentId,
            AttemptNumber = submission.AttemptNumber,
            Status = submission.Status,
            ContentText = submission.ContentText,
            FileUrl = submission.FileUrl,
            AssignedGrade = submission.AssignedGrade,
            PassScore = assignment.PassScore,
            MaxPoints = assignment.MaxPoints,
            Passed = passed,
            MentorFeedback = submission.MentorFeedback,
            VerifiedBy = submission.VerifiedBy,
            SubmittedAt = submission.SubmittedAt,
            GradedAt = submission.GradedAt,
            CreatedAt = submission.CreatedAt,
            UpdatedAt = submission.UpdatedAt
        };
    }
}
