using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using OboxSteam.Application.DTOs.ResearchSubmissionDTO;
using OboxSteam.Application.Interfaces;
using OboxSteam.Application.Utils;
using OboxSteam.Application.Validation;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;
using OboxSteam.Domain.Interfaces;

namespace OboxSteam.Application.Services;

public sealed class ResearchSubmissionService : IResearchSubmissionService
{
    private const string SubmissionFolder = "submissions";

    private readonly IClaimsService _claimsService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IBlobService _blobService;
    private readonly ILogger<ResearchSubmissionService> _logger;

    public ResearchSubmissionService(
        IClaimsService claimsService,
        IUnitOfWork unitOfWork,
        IBlobService blobService,
        ILogger<ResearchSubmissionService> logger)
    {
        _claimsService = claimsService;
        _unitOfWork = unitOfWork;
        _blobService = blobService;
        _logger = logger;
    }

    public async Task<ResearchSubmissionResponseDto> StartSubmission(StartResearchSubmissionRequestDto request)
    {
        var enrollment = await _unitOfWork.ModuleEnrollments.GetByIdAsync(request.ModuleEnrollmentId);
        if (enrollment == null || enrollment.IsDeleted)
        {
            throw ErrorHelper.NotFound(
                $"Module enrollment with id '{request.ModuleEnrollmentId}' not found.");
        }

        if (enrollment.Status != EnrollmentStatus.Active)
        {
            throw ErrorHelper.Forbidden("Module enrollment is not active.");
        }

        var milestone = await _unitOfWork.ResearchMilestones.GetByIdAsync(request.ResearchMilestoneId);
        ResearchMilestoneValidator.ValidateMilestoneExists(milestone, request.ResearchMilestoneId);

        if (milestone!.ModuleId != enrollment.ModuleId)
        {
            throw ErrorHelper.BadRequest(
                "Research milestone does not belong to the enrollment module.");
        }

        var module = await _unitOfWork.Modules.GetByIdAsync(enrollment.ModuleId);
        ResearchMilestoneValidator.ValidateResearchModule(module, enrollment.ModuleId);

        var opener = await ResearchSubmissionValidator.EnsureCanStartSubmissionAsync(
            _unitOfWork,
            _claimsService,
            enrollment.ModuleId,
            enrollment.StudentId);

        var assignment = await _unitOfWork.Assignments.GetByIdAsync(milestone.AssignmentId);
        if (assignment == null || assignment.IsDeleted)
        {
            throw ErrorHelper.NotFound($"Assignment with id '{milestone.AssignmentId}' not found.");
        }

        var existingSubmission = await _unitOfWork.Submissions.FirstOrDefaultAsync(
            s => s.ModuleEnrollmentId == request.ModuleEnrollmentId
                 && s.ResearchMilestoneId == request.ResearchMilestoneId
                 && !s.IsDeleted);

        if (existingSubmission != null)
        {
            throw ErrorHelper.Conflict(
                "A research submission already exists for this enrollment and milestone.");
        }

        var now = DateTime.UtcNow;
        ResearchSubmissionValidator.ValidateAssignmentAvailability(assignment, now);

        var milestoneIds = await ResearchSubmissionValidator.LoadModuleMilestoneIdsAsync(
            _unitOfWork,
            enrollment.ModuleId);
        var submissions = await _unitOfWork.Submissions.GetAllAsync(
            s => s.ModuleEnrollmentId == request.ModuleEnrollmentId
                 && s.ResearchMilestoneId.HasValue
                 && milestoneIds.Contains(s.ResearchMilestoneId.Value)
                 && !s.IsDeleted);

        var assignments = await _unitOfWork.Assignments.GetAllAsync(
            a => submissions.Select(s => s.AssignmentId).Contains(a.Id) && !a.IsDeleted);
        var assignmentsById = assignments.ToDictionary(a => a.Id);
        var submissionsByMilestoneId = submissions
            .GroupBy(s => s.ResearchMilestoneId!.Value)
            .ToDictionary(
                group => group.Key,
                group => group.OrderByDescending(s => s.AttemptNumber).ThenByDescending(s => s.CreatedAt).First());

        var activityProgresses = await _unitOfWork.ActivityProgresses.GetAllAsync(
            ap => ap.ModuleEnrollmentId == request.ModuleEnrollmentId && !ap.IsDeleted);
        var completedActivityIds = activityProgresses
            .Where(ap => ap.IsCompleted)
            .Select(ap => ap.ActivityId)
            .ToHashSet();

        await ResearchSubmissionValidator.ValidateMilestoneReadyForOpenAsync(
            _unitOfWork,
            enrollment,
            milestone,
            assignment,
            submissionsByMilestoneId,
            assignmentsById,
            completedActivityIds,
            now);

        var submission = new Submission
        {
            Id = Guid.NewGuid(),
            Code = ResearchSubmissionValidator.GenerateSubmissionCode(),
            AssignmentId = assignment.Id,
            StudentId = enrollment.StudentId,
            ModuleEnrollmentId = enrollment.Id,
            ResearchMilestoneId = milestone.Id,
            AttemptNumber = 0,
            Status = SubmissionStatus.Pending,
            CreatedAt = now,
            CreatedBy = opener.Id,
            IsDeleted = false
        };

        await _unitOfWork.Submissions.AddAsync(submission);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation(
            "StartSubmission opened research submission. SubmissionId={SubmissionId}, MilestoneId={MilestoneId}, StudentId={StudentId}, OpenedBy={OpenedBy}",
            submission.Id,
            milestone.Id,
            enrollment.StudentId,
            opener.Id);

        var evidenceUrls = await ResearchSubmissionValidator.LoadEvidenceUrlsAsync(_unitOfWork, submission.Id);

        return new ResearchSubmissionResponseDto
        {
            Id = submission.Id,
            Code = submission.Code,
            AssignmentId = submission.AssignmentId,
            ResearchMilestoneId = submission.ResearchMilestoneId!.Value,
            ModuleEnrollmentId = submission.ModuleEnrollmentId,
            StudentId = submission.StudentId,
            AttemptNumber = submission.AttemptNumber,
            Status = submission.Status,
            ContentText = submission.ContentText,
            FileUrl = submission.FileUrl,
            EvidenceUrls = evidenceUrls,
            AssignedGrade = submission.AssignedGrade,
            PassScore = assignment.PassScore,
            MaxPoints = assignment.MaxPoints,
            Passed = null,
            MentorFeedback = submission.MentorFeedback,
            VerifiedBy = submission.VerifiedBy,
            SubmittedAt = submission.SubmittedAt,
            GradedAt = submission.GradedAt,
            CreatedAt = submission.CreatedAt,
            UpdatedAt = submission.UpdatedAt
        };
    }

    public async Task<CreateResearchSubmissionRequestDto> UploadSubmissionFile(
        Guid submissionId,
        IFormFile file,
        bool isEvidence = false)
    {
        var student = await EnrollmentAccessValidator.GetCurrentStudentForEnrollAsync(
            _unitOfWork,
            _claimsService,
            ResearchSubmissionValidator.SubmitResearchForbiddenMessage);

        var submission = await _unitOfWork.Submissions.GetByIdAsync(submissionId);
        ResearchSubmissionValidator.ValidateSubmissionExists(submission, submissionId);
        ResearchSubmissionValidator.ValidateResearchSubmission(submission!);
        ResearchSubmissionValidator.ValidateSubmissionOwnership(submission!, student.Id);
        ResearchSubmissionValidator.ValidateSubmissionOpenForSubmit(submission!);
        ResearchSubmissionValidator.ValidateUploadFile(file);

        if (submission!.ModuleEnrollmentId.HasValue)
        {
            var enrollment = await _unitOfWork.ModuleEnrollments.GetByIdAsync(submission.ModuleEnrollmentId.Value);
            if (enrollment == null || enrollment.IsDeleted || enrollment.Status != EnrollmentStatus.Active)
            {
                throw ErrorHelper.Forbidden("Module enrollment is not active.");
            }
        }

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        var folder = $"{SubmissionFolder}/{submissionId}";
        var fileName = $"{submissionId}_{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}{extension}";
        var s3Key = $"{folder}/{fileName}";

        _logger.LogInformation(
            "UploadSubmissionFile uploading to S3. SubmissionId={SubmissionId}, S3Key={S3Key}, IsEvidence={IsEvidence}",
            submissionId,
            s3Key,
            isEvidence);

        await using var stream = file.OpenReadStream();
        await _blobService.UploadFileAsync(fileName, stream, folder);
        var fileUrl = await _blobService.GetPreviewUrlAsync(s3Key);

        _logger.LogInformation(
            "UploadSubmissionFile completed. SubmissionId={SubmissionId}, StudentId={StudentId}",
            submissionId,
            student.Id);

        return isEvidence
            ? new CreateResearchSubmissionRequestDto { EvidenceUrls = [fileUrl] }
            : new CreateResearchSubmissionRequestDto { FileUrl = fileUrl };
    }

    public async Task<ResearchSubmissionResponseDto?> GetSubmission(Guid submissionId)
    {
        var submission = await _unitOfWork.Submissions.GetByIdAsync(submissionId);
        if (submission == null || submission.IsDeleted)
        {
            return null;
        }

        ResearchSubmissionValidator.ValidateResearchSubmission(submission);
        await ResearchSubmissionValidator.EnsureCanViewSubmissionAsync(
            _unitOfWork,
            _claimsService,
            submission);

        var assignment = await _unitOfWork.Assignments.GetByIdAsync(submission.AssignmentId);
        if (assignment == null || assignment.IsDeleted)
        {
            throw ErrorHelper.NotFound($"Assignment with id '{submission.AssignmentId}' not found.");
        }

        var evidenceUrls = await ResearchSubmissionValidator.LoadEvidenceUrlsAsync(_unitOfWork, submission.Id);
        bool? passed = null;
        if (submission.Status == SubmissionStatus.Graded && submission.AssignedGrade.HasValue)
        {
            passed = submission.AssignedGrade.Value >= assignment.PassScore;
        }

        return new ResearchSubmissionResponseDto
        {
            Id = submission.Id,
            Code = submission.Code,
            AssignmentId = submission.AssignmentId,
            ResearchMilestoneId = submission.ResearchMilestoneId!.Value,
            ModuleEnrollmentId = submission.ModuleEnrollmentId,
            StudentId = submission.StudentId,
            AttemptNumber = submission.AttemptNumber,
            Status = submission.Status,
            ContentText = submission.ContentText,
            FileUrl = submission.FileUrl,
            EvidenceUrls = evidenceUrls,
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

    public async Task<ResearchSubmissionResponseDto> SubmitResearchWork(
        Guid submissionId,
        CreateResearchSubmissionRequestDto request)
    {
        var student = await EnrollmentAccessValidator.GetCurrentStudentForEnrollAsync(
            _unitOfWork,
            _claimsService,
            ResearchSubmissionValidator.SubmitResearchForbiddenMessage);

        ResearchSubmissionValidator.ValidateSubmitContent(request);

        var submission = await _unitOfWork.Submissions.GetByIdAsync(submissionId);
        ResearchSubmissionValidator.ValidateSubmissionExists(submission, submissionId);
        ResearchSubmissionValidator.ValidateResearchSubmission(submission!);
        ResearchSubmissionValidator.ValidateSubmissionOwnership(submission!, student.Id);
        ResearchSubmissionValidator.ValidateSubmissionOpenForSubmit(submission!);

        var assignment = await _unitOfWork.Assignments.GetByIdAsync(submission!.AssignmentId);
        if (assignment == null || assignment.IsDeleted)
        {
            throw ErrorHelper.NotFound($"Assignment with id '{submission.AssignmentId}' not found.");
        }

        var now = DateTime.UtcNow;
        ResearchSubmissionValidator.ValidateAssignmentAvailability(assignment, now);

        var nextAttemptNumber = ResearchSubmissionValidator.GetNextAttemptNumber(submission);
        ResearchSubmissionValidator.ValidateMaxAttemptsNotExceeded(assignment, nextAttemptNumber);

        if (submission.ModuleEnrollmentId.HasValue)
        {
            var enrollment = await _unitOfWork.ModuleEnrollments.GetByIdAsync(submission.ModuleEnrollmentId.Value);
            if (enrollment == null || enrollment.IsDeleted || enrollment.Status != EnrollmentStatus.Active)
            {
                throw ErrorHelper.Forbidden("Module enrollment is not active.");
            }
        }

        submission.ContentText = request.ContentText?.Trim();
        submission.FileUrl = request.FileUrl?.Trim();
        submission.AttemptNumber = nextAttemptNumber;
        submission.Status = SubmissionStatus.TurnedIn;
        submission.SubmittedAt = now;
        submission.UpdatedAt = now;
        submission.UpdatedBy = student.Id;

        await ResearchSubmissionValidator.ReplaceEvidenceUrlsAsync(
            _unitOfWork,
            submission,
            request.EvidenceUrls,
            student.Id,
            now);
        await _unitOfWork.Submissions.Update(submission);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation(
            "SubmitResearchWork completed. SubmissionId={SubmissionId}, AttemptNumber={AttemptNumber}, StudentId={StudentId}",
            submission.Id,
            submission.AttemptNumber,
            student.Id);

        var evidenceUrls = await ResearchSubmissionValidator.LoadEvidenceUrlsAsync(_unitOfWork, submission.Id);

        return new ResearchSubmissionResponseDto
        {
            Id = submission.Id,
            Code = submission.Code,
            AssignmentId = submission.AssignmentId,
            ResearchMilestoneId = submission.ResearchMilestoneId!.Value,
            ModuleEnrollmentId = submission.ModuleEnrollmentId,
            StudentId = submission.StudentId,
            AttemptNumber = submission.AttemptNumber,
            Status = submission.Status,
            ContentText = submission.ContentText,
            FileUrl = submission.FileUrl,
            EvidenceUrls = evidenceUrls,
            AssignedGrade = submission.AssignedGrade,
            PassScore = assignment.PassScore,
            MaxPoints = assignment.MaxPoints,
            Passed = null,
            MentorFeedback = submission.MentorFeedback,
            VerifiedBy = submission.VerifiedBy,
            SubmittedAt = submission.SubmittedAt,
            GradedAt = submission.GradedAt,
            CreatedAt = submission.CreatedAt,
            UpdatedAt = submission.UpdatedAt
        };
    }

    public async Task<ResearchSubmissionResponseDto> GradeSubmission(
        Guid submissionId,
        GradeResearchSubmissionRequestDto request)
    {
        var submission = await _unitOfWork.Submissions.GetByIdAsync(submissionId);
        ResearchSubmissionValidator.ValidateSubmissionExists(submission, submissionId);
        ResearchSubmissionValidator.ValidateResearchSubmission(submission!);
        ResearchSubmissionValidator.ValidateSubmissionAwaitingGrade(submission!);

        var assignment = await _unitOfWork.Assignments.GetByIdAsync(submission!.AssignmentId);
        if (assignment == null || assignment.IsDeleted)
        {
            throw ErrorHelper.NotFound($"Assignment with id '{submission.AssignmentId}' not found.");
        }

        var grader = await ResearchSubmissionValidator.EnsureCanGradeSubmissionAsync(
            _unitOfWork,
            _claimsService,
            assignment.ModuleId,
            submission!.StudentId);

        if (request.AssignedGrade < 0 || request.AssignedGrade > assignment.MaxPoints)
        {
            throw ErrorHelper.BadRequest(
                $"AssignedGrade must be between 0 and {assignment.MaxPoints}.");
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

        _logger.LogInformation(
            "GradeSubmission completed. SubmissionId={SubmissionId}, Status={Status}, GradedBy={GradedBy}",
            submission.Id,
            submission.Status,
            grader.Id);

        var evidenceUrls = await ResearchSubmissionValidator.LoadEvidenceUrlsAsync(_unitOfWork, submission.Id);
        bool? passed = submission.Status == SubmissionStatus.Graded && submission.AssignedGrade.HasValue
            ? submission.AssignedGrade.Value >= assignment.PassScore
            : null;

        return new ResearchSubmissionResponseDto
        {
            Id = submission.Id,
            Code = submission.Code,
            AssignmentId = submission.AssignmentId,
            ResearchMilestoneId = submission.ResearchMilestoneId!.Value,
            ModuleEnrollmentId = submission.ModuleEnrollmentId,
            StudentId = submission.StudentId,
            AttemptNumber = submission.AttemptNumber,
            Status = submission.Status,
            ContentText = submission.ContentText,
            FileUrl = submission.FileUrl,
            EvidenceUrls = evidenceUrls,
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
