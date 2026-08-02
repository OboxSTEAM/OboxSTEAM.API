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
    private readonly ICertificateService _certificateService;
    private readonly INotificationPublisher _notificationPublisher;
    private readonly ILogger<ResearchSubmissionService> _logger;

    public ResearchSubmissionService(
        IClaimsService claimsService,
        IUnitOfWork unitOfWork,
        IBlobService blobService,
        ICertificateService certificateService,
        INotificationPublisher notificationPublisher,
        ILogger<ResearchSubmissionService> logger)
    {
        _claimsService = claimsService;
        _unitOfWork = unitOfWork;
        _blobService = blobService;
        _certificateService = certificateService;
        _notificationPublisher = notificationPublisher;
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

        await _notificationPublisher.PublishAsync(NotificationCatalog.ResearchSubmissionOpened(
            enrollment.StudentId,
            submission.Id,
            assignment.Id,
            module!.ProgramId));

        _logger.LogInformation(
            "StartSubmission opened research submission. SubmissionId={SubmissionId}, MilestoneId={MilestoneId}, StudentId={StudentId}, OpenedBy={OpenedBy}",
            submission.Id,
            milestone.Id,
            enrollment.StudentId,
            opener.Id);

        return await MapSubmissionToResponseDtoAsync(submission, assignment);
    }

    public async Task<StartResearchSubmissionForClassResponseDto> StartSubmissionForClass(
        StartResearchSubmissionForClassRequestDto request)
    {
        var classEntity = await _unitOfWork.Classes.GetByIdAsync(request.ClassId);
        ClassValidator.ValidateClassExists(classEntity, request.ClassId);

        var milestone = await _unitOfWork.ResearchMilestones.GetByIdAsync(request.ResearchMilestoneId);
        ResearchMilestoneValidator.ValidateMilestoneExists(milestone, request.ResearchMilestoneId);

        var module = await _unitOfWork.Modules.GetByIdAsync(milestone!.ModuleId);
        ResearchMilestoneValidator.ValidateResearchModule(module, milestone.ModuleId);

        if (module!.ProgramId != classEntity!.ProgramId)
        {
            throw ErrorHelper.BadRequest(MentorScopeValidator.ClassProgramMismatchMessage);
        }

        var opener = await ResearchSubmissionValidator.EnsureCanStartSubmissionForClassAsync(
            _unitOfWork,
            _claimsService,
            request.ClassId,
            milestone.ModuleId);

        var assignment = await _unitOfWork.Assignments.GetByIdAsync(milestone.AssignmentId);
        if (assignment == null || assignment.IsDeleted)
        {
            throw ErrorHelper.NotFound($"Assignment with id '{milestone.AssignmentId}' not found.");
        }

        var now = DateTime.UtcNow;
        ResearchSubmissionValidator.ValidateAssignmentAvailability(assignment, now);

        var classEnrollments = await _unitOfWork.ClassEnrollments.GetAllAsync(
            ce => ce.ClassId == request.ClassId
                  && ce.Status == ClassEnrollmentStatus.Active
                  && !ce.IsDeleted);

        var response = new StartResearchSubmissionForClassResponseDto
        {
            ClassId = request.ClassId,
            ResearchMilestoneId = request.ResearchMilestoneId,
            TotalClassStudents = classEnrollments.Count
        };

        if (classEnrollments.Count == 0)
        {
            return response;
        }

        var studentIds = classEnrollments.Select(ce => ce.StudentId).Distinct().ToList();
        var programEnrollmentIds = classEnrollments
            .Select(ce => ce.ProgramEnrollmentId)
            .Distinct()
            .ToList();

        var moduleEnrollments = await _unitOfWork.ModuleEnrollments.GetAllAsync(
            me => studentIds.Contains(me.StudentId)
                  && me.ModuleId == milestone.ModuleId
                  && me.Status == EnrollmentStatus.Active
                  && !me.IsDeleted
                  && me.ProgramEnrollmentId.HasValue
                  && programEnrollmentIds.Contains(me.ProgramEnrollmentId.Value));

        var moduleEnrollmentByStudentProgram = moduleEnrollments
            .GroupBy(me => (me.StudentId, me.ProgramEnrollmentId!.Value))
            .ToDictionary(
                group => group.Key,
                group => group.OrderByDescending(me => me.AttemptNumber).ThenByDescending(me => me.CreatedAt).First());

        var matchedModuleEnrollmentIds = classEnrollments
            .Select(ce => moduleEnrollmentByStudentProgram.TryGetValue(
                (ce.StudentId, ce.ProgramEnrollmentId),
                out var enrollment)
                ? enrollment.Id
                : (Guid?)null)
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .ToList();

        var existingSubmissions = matchedModuleEnrollmentIds.Count == 0
            ? []
            : await _unitOfWork.Submissions.GetAllAsync(
                s => s.ModuleEnrollmentId.HasValue
                     && matchedModuleEnrollmentIds.Contains(s.ModuleEnrollmentId.Value)
                     && s.ResearchMilestoneId == request.ResearchMilestoneId
                     && !s.IsDeleted);

        var existingSubmissionByEnrollmentId = existingSubmissions
            .GroupBy(s => s.ModuleEnrollmentId!.Value)
            .ToDictionary(group => group.Key, group => group.First());

        var submissionsToAdd = new List<Submission>();

        foreach (var classEnrollment in classEnrollments)
        {
            if (!moduleEnrollmentByStudentProgram.TryGetValue(
                    (classEnrollment.StudentId, classEnrollment.ProgramEnrollmentId),
                    out var moduleEnrollment))
            {
                response.Skipped.Add(new StartResearchSubmissionForClassSkippedDto
                {
                    StudentId = classEnrollment.StudentId,
                    Reason = "No active module enrollment for this research module."
                });
                continue;
            }

            if (existingSubmissionByEnrollmentId.ContainsKey(moduleEnrollment.Id))
            {
                response.Skipped.Add(new StartResearchSubmissionForClassSkippedDto
                {
                    StudentId = classEnrollment.StudentId,
                    Reason = "A research submission already exists for this enrollment and milestone."
                });
                continue;
            }

            submissionsToAdd.Add(new Submission
            {
                Id = Guid.NewGuid(),
                Code = ResearchSubmissionValidator.GenerateSubmissionCode(),
                AssignmentId = assignment.Id,
                StudentId = moduleEnrollment.StudentId,
                ModuleEnrollmentId = moduleEnrollment.Id,
                ResearchMilestoneId = milestone.Id,
                AttemptNumber = 0,
                Status = SubmissionStatus.Pending,
                CreatedAt = now,
                CreatedBy = opener.Id,
                IsDeleted = false
            });
        }

        if (submissionsToAdd.Count > 0)
        {
            await _unitOfWork.Submissions.AddRangeAsync(submissionsToAdd);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation(
                "StartSubmissionForClass opened {OpenedCount} research submission(s). ClassId={ClassId}, MilestoneId={MilestoneId}, OpenedBy={OpenedBy}",
                submissionsToAdd.Count,
                request.ClassId,
                milestone.Id,
                opener.Id);
        }

        foreach (var submission in submissionsToAdd)
        {
            response.Opened.Add(await MapSubmissionToResponseDtoAsync(submission, assignment));
        }

        response.OpenedCount = response.Opened.Count;
        response.SkippedCount = response.Skipped.Count;

        return response;
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

        bool? passed = null;
        if (submission.Status == SubmissionStatus.Graded && submission.AssignedGrade.HasValue)
        {
            passed = submission.AssignedGrade.Value >= assignment.PassScore;
        }

        return await MapSubmissionToResponseDtoAsync(submission, assignment, passed);
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

        ModuleEnrollment? moduleEnrollment = null;
        if (submission.ModuleEnrollmentId.HasValue)
        {
            moduleEnrollment = await _unitOfWork.ModuleEnrollments.GetByIdAsync(submission.ModuleEnrollmentId.Value);
            if (moduleEnrollment == null || moduleEnrollment.IsDeleted || moduleEnrollment.Status != EnrollmentStatus.Active)
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

        var module = await _unitOfWork.Modules.GetByIdAsync(assignment.ModuleId);
        Guid? classId = null;
        if (moduleEnrollment?.ProgramEnrollmentId != null)
        {
            var classEnrollment = await _unitOfWork.ClassEnrollments.FirstOrDefaultAsync(
                ce => ce.ProgramEnrollmentId == moduleEnrollment.ProgramEnrollmentId.Value
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
        submission.Status = request.ReturnForRevision
            ? SubmissionStatus.ReturnedForRevision
            : SubmissionStatus.Graded;

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
