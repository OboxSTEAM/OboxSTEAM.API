using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using OboxSteam.Application.Commons;
using OboxSteam.Application.DTOs.ResearchSubmissionDTO;
using OboxSteam.Application.Interfaces;
using OboxSteam.Application.Notifications;
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
    private readonly IMediaService _mediaService;
    private readonly ICertificateService _certificateService;
    private readonly INotificationPublisher _notificationPublisher;
    private readonly ILogger<ResearchSubmissionService> _logger;

    public ResearchSubmissionService(
        IClaimsService claimsService,
        IUnitOfWork unitOfWork,
        IBlobService blobService,
        IMediaService mediaService,
        ICertificateService certificateService,
        INotificationPublisher notificationPublisher,
        ILogger<ResearchSubmissionService> logger)
    {
        _claimsService = claimsService;
        _unitOfWork = unitOfWork;
        _blobService = blobService;
        _mediaService = mediaService;
        _certificateService = certificateService;
        _notificationPublisher = notificationPublisher;
        _logger = logger;
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

        bool? passed = null;
        if (submission.Status == SubmissionStatus.Graded && submission.AssignedGrade.HasValue)
        {
            passed = submission.AssignedGrade.Value >= assignment.PassScore;
        }

        return await MapSubmissionToResponseDtoAsync(submission, assignment, passed);
    }

    public async Task<UploadResearchSubmissionResponseDto> UploadSubmissionFile(
        Guid moduleEnrollmentId,
        Guid researchMilestoneId,
        IFormFile file,
        bool isEvidence = false)
    {
        var student = await EnrollmentAccessValidator.GetCurrentStudentForEnrollAsync(
            _unitOfWork,
            _claimsService,
            ResearchSubmissionValidator.SubmitResearchForbiddenMessage);

        if (isEvidence)
            ResearchSubmissionValidator.ValidateEvidenceUploadFile(file);
        else
            ResearchSubmissionValidator.ValidateUploadFile(file);

        var (enrollment, milestone, assignment, now, personalUntil) =
            await ResolveStudentMilestoneContextAsync(student.Id, moduleEnrollmentId, researchMilestoneId);

        var submission = await EnsurePendingDraftAsync(
            student.Id,
            enrollment,
            milestone,
            assignment,
            now,
            personalUntil);

        if (isEvidence)
        {
            var classId = await ResearchSubmissionValidator.ResolveEvidenceClassIdAsync(
                _unitOfWork,
                submission);

            _logger.LogInformation(
                "UploadSubmissionFile evidence via media pipeline. SubmissionId={SubmissionId}, ClassId={ClassId}",
                submission.Id,
                classId);

            var media = await _mediaService.UploadMediaAsync(file, classId, classSessionId: null);

            var alreadyLinked = await _unitOfWork.SubmissionEvidences.FirstOrDefaultAsync(
                se => se.SubmissionId == submission.Id
                      && se.MediaId == media.Id
                      && !se.IsDeleted);
            if (alreadyLinked == null)
            {
                await _unitOfWork.SubmissionEvidences.AddAsync(new SubmissionEvidence
                {
                    SubmissionId = submission.Id,
                    MediaId = media.Id,
                    CreatedAt = now,
                    CreatedBy = student.Id,
                    IsDeleted = false
                });
                await _unitOfWork.SaveChangesAsync();
            }

            _logger.LogInformation(
                "UploadSubmissionFile evidence completed. SubmissionId={SubmissionId}, MediaAssetId={MediaAssetId}, StudentId={StudentId}",
                submission.Id,
                media.Id,
                student.Id);

            return new UploadResearchSubmissionResponseDto
            {
                SubmissionId = submission.Id,
                MediaAssetId = media.Id,
                EvidenceUrls = string.IsNullOrWhiteSpace(media.FileUrl) ? null : [media.FileUrl]
            };
        }

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        var folder = $"{SubmissionFolder}/{submission.Id}";
        var fileName = $"{submission.Id}_{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}{extension}";
        var s3Key = $"{folder}/{fileName}";

        _logger.LogInformation(
            "UploadSubmissionFile uploading primary to S3. SubmissionId={SubmissionId}, S3Key={S3Key}",
            submission.Id,
            s3Key);

        await using var stream = file.OpenReadStream();
        await _blobService.UploadFileAsync(fileName, stream, folder);
        var fileUrl = await _blobService.GetPreviewUrlAsync(s3Key);

        _logger.LogInformation(
            "UploadSubmissionFile primary completed. SubmissionId={SubmissionId}, StudentId={StudentId}",
            submission.Id,
            student.Id);

        return new UploadResearchSubmissionResponseDto
        {
            SubmissionId = submission.Id,
            FileUrl = fileUrl
        };
    }

    public async Task<ResearchSubmissionResponseDto> SubmitResearchWork(SubmitResearchWorkRequestDto request)
    {
        var student = await EnrollmentAccessValidator.GetCurrentStudentForEnrollAsync(
            _unitOfWork,
            _claimsService,
            ResearchSubmissionValidator.SubmitResearchForbiddenMessage);

        ResearchSubmissionValidator.ValidateSubmitContent(request);

        var (enrollment, milestone, assignment, now, personalUntil) =
            await ResolveStudentMilestoneContextAsync(
                student.Id,
                request.ModuleEnrollmentId,
                request.ResearchMilestoneId);

        var submission = await _unitOfWork.Submissions.FirstOrDefaultAsync(
            s => s.ModuleEnrollmentId == enrollment.Id
                 && s.ResearchMilestoneId == milestone.Id
                 && !s.IsDeleted);

        if (submission == null)
        {
            await ValidateMilestoneReadyAsync(enrollment, milestone, assignment, now, personalUntil);

            var nextAttemptNumber = 1;
            await ResearchSubmissionValidator.ValidateMaxAttemptsNotExceededAsync(
                _unitOfWork,
                assignment,
                student.Id,
                nextAttemptNumber,
                enrollment.Id);

            submission = new Submission
            {
                Id = Guid.NewGuid(),
                Code = ResearchSubmissionValidator.GenerateSubmissionCode(),
                AssignmentId = assignment.Id,
                StudentId = student.Id,
                ModuleEnrollmentId = enrollment.Id,
                ResearchMilestoneId = milestone.Id,
                AttemptNumber = nextAttemptNumber,
                Status = SubmissionStatus.TurnedIn,
                ContentText = request.ContentText?.Trim(),
                FileUrl = request.FileUrl?.Trim(),
                SubmittedAt = now,
                CreatedAt = now,
                CreatedBy = student.Id,
                IsDeleted = false
            };

            await ResearchSubmissionValidator.ReplaceEvidenceMediaAsync(
                _unitOfWork,
                submission,
                request.EvidenceMediaAssetIds,
                student.Id,
                now);
            await _unitOfWork.Submissions.AddAsync(submission);
        }
        else
        {
            ResearchSubmissionValidator.ValidateResearchSubmission(submission);
            ResearchSubmissionValidator.ValidateSubmissionOwnership(submission, student.Id);
            ResearchSubmissionValidator.ValidateSubmissionOpenForSubmit(submission);
            ResearchSubmissionValidator.ValidateAssignmentAvailability(assignment, now, personalUntil);

            var nextAttemptNumber = ResearchSubmissionValidator.GetNextAttemptNumber(submission);
            await ResearchSubmissionValidator.ValidateMaxAttemptsNotExceededAsync(
                _unitOfWork,
                assignment,
                student.Id,
                nextAttemptNumber,
                enrollment.Id);

            submission.ContentText = request.ContentText?.Trim();
            submission.FileUrl = request.FileUrl?.Trim();
            submission.AttemptNumber = nextAttemptNumber;
            submission.Status = SubmissionStatus.TurnedIn;
            submission.AssignedGrade = null;
            submission.MentorFeedback = null;
            submission.GradedAt = null;
            submission.VerifiedBy = null;
            submission.SubmittedAt = now;
            submission.UpdatedAt = now;
            submission.UpdatedBy = student.Id;

            await ResearchSubmissionValidator.ReplaceEvidenceMediaAsync(
                _unitOfWork,
                submission,
                request.EvidenceMediaAssetIds,
                student.Id,
                now);
            await _unitOfWork.Submissions.Update(submission);
        }

        await _unitOfWork.SaveChangesAsync();

        var module = await _unitOfWork.Modules.GetByIdAsync(assignment.ModuleId);
        Guid? classId = null;
        if (enrollment.ProgramEnrollmentId.HasValue)
        {
            var classEnrollment = await _unitOfWork.ClassEnrollments.FirstOrDefaultAsync(
                ce => ce.ProgramEnrollmentId == enrollment.ProgramEnrollmentId.Value
                      && ce.Status == ClassEnrollmentStatus.Active
                      && !ce.IsDeleted);
            classId = classEnrollment?.ClassId;
        }

        await _notificationPublisher.PublishAsync(NotificationCatalog.ResearchWorkSubmitted(
            student.Id,
            submission.Id,
            assignment.Id,
            classId,
            module?.ProgramId,
            assignment.Title));

        _logger.LogInformation(
            "SubmitResearchWork completed. SubmissionId={SubmissionId}, AttemptNumber={AttemptNumber}, StudentId={StudentId}",
            submission.Id,
            submission.AttemptNumber,
            student.Id);

        return await MapSubmissionToResponseDtoAsync(submission, assignment);
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

        if (request.ReturnForRevision)
        {
            submission.Status = SubmissionStatus.ReturnedForRevision;
        }
        else
        {
            var passedGrade = request.AssignedGrade >= assignment.PassScore;
            if (passedGrade)
            {
                submission.Status = SubmissionStatus.Graded;
            }
            else
            {
                // Reopen last failed milestone for another attempt when budget remains.
                var nextAttempt = ResearchSubmissionValidator.GetNextAttemptNumber(submission);
                var effectiveMax = await AssessmentAttemptPolicy.GetEffectiveMaxAttemptsAsync(
                    _unitOfWork,
                    assignment,
                    submission.StudentId,
                    submission.ModuleEnrollmentId);

                submission.Status = nextAttempt <= effectiveMax
                    ? SubmissionStatus.ReturnedForRevision
                    : SubmissionStatus.Graded;
            }
        }

        await _unitOfWork.Submissions.Update(submission);
        await _unitOfWork.SaveChangesAsync();

        await RecalculateEnrollmentProgressAsync(submission);

        var module = await _unitOfWork.Modules.GetByIdAsync(assignment.ModuleId);
        if (submission.Status == SubmissionStatus.ReturnedForRevision)
        {
            await _notificationPublisher.PublishAsync(NotificationCatalog.ResearchReturnedForRevision(
                submission.StudentId,
                submission.Id,
                assignment.Id,
                module?.ProgramId,
                assignment.Title,
                grader.Id));
        }
        else
        {
            await _notificationPublisher.PublishAsync(NotificationCatalog.ResearchGraded(
                submission.StudentId,
                submission.Id,
                assignment.Id,
                submission.AssignedGrade!.Value >= assignment.PassScore,
                module?.ProgramId,
                assignment.Title));
        }

        _logger.LogInformation(
            "GradeSubmission completed. SubmissionId={SubmissionId}, Status={Status}, GradedBy={GradedBy}",
            submission.Id,
            submission.Status,
            grader.Id);

        bool? passed = submission.Status == SubmissionStatus.Graded && submission.AssignedGrade.HasValue
            ? submission.AssignedGrade.Value >= assignment.PassScore
            : null;

        return await MapSubmissionToResponseDtoAsync(submission, assignment, passed);
    }

    private async Task<(
        ModuleEnrollment Enrollment,
        ResearchMilestone Milestone,
        Assignment Assignment,
        DateTime Now,
        DateTime? PersonalUntil)> ResolveStudentMilestoneContextAsync(
        Guid studentId,
        Guid moduleEnrollmentId,
        Guid researchMilestoneId)
    {
        var enrollment = await _unitOfWork.ModuleEnrollments.GetByIdAsync(moduleEnrollmentId);
        if (enrollment == null || enrollment.IsDeleted)
        {
            throw ErrorHelper.NotFound($"Module enrollment with id '{moduleEnrollmentId}' not found.");
        }

        if (enrollment.StudentId != studentId)
        {
            throw ErrorHelper.Forbidden("Module enrollment does not belong to the current student.");
        }

        if (enrollment.Status != EnrollmentStatus.Active)
        {
            throw ErrorHelper.Forbidden("Module enrollment is not active.");
        }

        var milestone = await _unitOfWork.ResearchMilestones.GetByIdAsync(researchMilestoneId);
        ResearchMilestoneValidator.ValidateMilestoneExists(milestone, researchMilestoneId);

        if (milestone!.ModuleId != enrollment.ModuleId)
        {
            throw ErrorHelper.BadRequest(
                "Research milestone does not belong to the enrollment module.");
        }

        var module = await _unitOfWork.Modules.GetByIdAsync(enrollment.ModuleId);
        ResearchMilestoneValidator.ValidateResearchModule(module, enrollment.ModuleId);

        var assignment = await _unitOfWork.Assignments.GetByIdAsync(milestone.AssignmentId);
        if (assignment == null || assignment.IsDeleted)
        {
            throw ErrorHelper.NotFound($"Assignment with id '{milestone.AssignmentId}' not found.");
        }

        var now = DateTime.UtcNow;
        var (_, personalUntil) = await AssessmentAttemptPolicy.GetPersonalWindowAsync(
            _unitOfWork,
            studentId,
            assignment.Id,
            enrollment.Id);

        return (enrollment, milestone, assignment, now, personalUntil);
    }

    private async Task ValidateMilestoneReadyAsync(
        ModuleEnrollment enrollment,
        ResearchMilestone milestone,
        Assignment assignment,
        DateTime now,
        DateTime? personalUntil)
    {
        var milestoneIds = await ResearchSubmissionValidator.LoadModuleMilestoneIdsAsync(
            _unitOfWork,
            enrollment.ModuleId);
        var submissions = await _unitOfWork.Submissions.GetAllAsync(
            s => s.ModuleEnrollmentId == enrollment.Id
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
            ap => ap.ModuleEnrollmentId == enrollment.Id && !ap.IsDeleted);
        var completedActivityIds = activityProgresses
            .Where(ap => ap.IsCompleted)
            .Select(ap => ap.ActivityId)
            .ToHashSet();

        await ResearchSubmissionValidator.ValidateMilestoneReadyForSubmitAsync(
            _unitOfWork,
            enrollment,
            milestone,
            assignment,
            submissionsByMilestoneId,
            assignmentsById,
            completedActivityIds,
            now,
            personalUntil);
    }

    private async Task<Submission> EnsurePendingDraftAsync(
        Guid studentId,
        ModuleEnrollment enrollment,
        ResearchMilestone milestone,
        Assignment assignment,
        DateTime now,
        DateTime? personalUntil)
    {
        var existing = await _unitOfWork.Submissions.FirstOrDefaultAsync(
            s => s.ModuleEnrollmentId == enrollment.Id
                 && s.ResearchMilestoneId == milestone.Id
                 && !s.IsDeleted);

        if (existing != null)
        {
            ResearchSubmissionValidator.ValidateResearchSubmission(existing);
            ResearchSubmissionValidator.ValidateSubmissionOwnership(existing, studentId);
            ResearchSubmissionValidator.ValidateSubmissionOpenForSubmit(existing);
            ResearchSubmissionValidator.ValidateAssignmentAvailability(assignment, now, personalUntil);
            return existing;
        }

        await ValidateMilestoneReadyAsync(enrollment, milestone, assignment, now, personalUntil);

        var submission = new Submission
        {
            Id = Guid.NewGuid(),
            Code = ResearchSubmissionValidator.GenerateSubmissionCode(),
            AssignmentId = assignment.Id,
            StudentId = studentId,
            ModuleEnrollmentId = enrollment.Id,
            ResearchMilestoneId = milestone.Id,
            AttemptNumber = 0,
            Status = SubmissionStatus.Pending,
            CreatedAt = now,
            CreatedBy = studentId,
            IsDeleted = false
        };

        await _unitOfWork.Submissions.AddAsync(submission);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation(
            "EnsurePendingDraftAsync created draft. SubmissionId={SubmissionId}, MilestoneId={MilestoneId}, StudentId={StudentId}",
            submission.Id,
            milestone.Id,
            studentId);

        return submission;
    }

    /// <summary>
    /// Recomputes module and program progress for the enrollment behind a graded submission,
    /// so passing a research milestone immediately advances the curriculum progress.
    /// </summary>
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
            await _certificateService.EnsureProgramCertificateAsync(programEnrollmentId);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "[TryEnsureProgramCertificateAsync] Failed for enrollment {EnrollmentId}. Learning progress was not rolled back.",
                programEnrollmentId);
        }
    }

    private async Task<ResearchSubmissionResponseDto> MapSubmissionToResponseDtoAsync(
        Submission submission,
        Assignment assignment,
        bool? passed = null)
    {
        var evidenceUrls = await ResearchSubmissionValidator.LoadEvidenceUrlsAsync(_unitOfWork, submission.Id);
        var resolvedEvidenceUrls = new List<string>(evidenceUrls.Count);
        foreach (var evidenceUrl in evidenceUrls)
        {
            var resolved = await ResolvePresignedFileUrlAsync(evidenceUrl);
            if (!string.IsNullOrWhiteSpace(resolved))
            {
                resolvedEvidenceUrls.Add(resolved);
            }
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
            FileUrl = await ResolvePresignedFileUrlAsync(submission.FileUrl),
            EvidenceUrls = resolvedEvidenceUrls,
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

    /// <summary>
    /// Resolves a stored S3 public URL or raw key into a time-limited presigned URL
    /// so FE can open private-bucket seed/submission files (same pattern as materials).
    /// </summary>
    private async Task<string?> ResolvePresignedFileUrlAsync(string? fileUrl)
    {
        if (string.IsNullOrWhiteSpace(fileUrl))
        {
            return fileUrl;
        }

        var s3Key = ExtractS3Key(fileUrl, _blobService.BucketName);
        if (string.IsNullOrWhiteSpace(s3Key))
        {
            return fileUrl;
        }

        var presigned = await _blobService.GetFileUrlAsync(s3Key);
        return string.IsNullOrWhiteSpace(presigned) ? fileUrl : presigned;
    }

    private static string? ExtractS3Key(string fileUrl, string? bucketName = null)
    {
        if (string.IsNullOrWhiteSpace(fileUrl))
        {
            return fileUrl;
        }

        if (!fileUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase))
        {
            return fileUrl;
        }

        if (!Uri.TryCreate(fileUrl, UriKind.Absolute, out var uri))
        {
            return fileUrl;
        }

        var path = uri.AbsolutePath.TrimStart('/');

        if (!string.IsNullOrEmpty(bucketName))
        {
            var bucketPrefix = $"{bucketName}/";
            if (path.StartsWith(bucketPrefix, StringComparison.OrdinalIgnoreCase))
            {
                path = path[bucketPrefix.Length..];
            }
        }

        return Uri.UnescapeDataString(path.Replace('+', ' '));
    }
}
