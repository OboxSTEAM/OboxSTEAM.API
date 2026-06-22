using Microsoft.AspNetCore.Http;
using OboxSteam.Application.DTOs.ResearchSubmissionDTO;
using OboxSteam.Application.Interfaces;
using OboxSteam.Application.Utils;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;
using OboxSteam.Domain.Interfaces;

namespace OboxSteam.Application.Validation;

public static class ResearchSubmissionValidator
{
    private static readonly HashSet<string> AllowedUploadExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf", ".doc", ".docx", ".zip",
        ".jpg", ".jpeg", ".png", ".gif", ".webp",
        ".mp4", ".mov", ".avi", ".mkv"
    };

    private const long MaxDocSize = 50L * 1024 * 1024;
    private const long MaxImageSize = 10L * 1024 * 1024;
    private const long MaxVideoSize = 3L * 1024 * 1024 * 1024;

    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
        { ".jpg", ".jpeg", ".png", ".gif", ".webp" };

    private static readonly HashSet<string> VideoExtensions = new(StringComparer.OrdinalIgnoreCase)
        { ".mp4", ".mov", ".avi", ".mkv" };
    public const string StartSubmissionForbiddenMessage =
        "Only Mentor, Manager, and SuperAdmin can open a research submission for a student.";

    public const string SubmitResearchForbiddenMessage =
        "Only students can submit research work.";

    public const string GradeSubmissionForbiddenMessage =
        "Only Mentor, Manager, and SuperAdmin can grade research submissions.";

    public const string ViewSubmissionForbiddenMessage =
        "You do not have permission to view this research submission.";

    public static Submission ValidateSubmissionExists(Submission? submission, Guid submissionId)
    {
        if (submission == null || submission.IsDeleted)
        {
            throw ErrorHelper.NotFound($"Submission with id '{submissionId}' not found.");
        }

        return submission;
    }

    public static void ValidateResearchSubmission(Submission submission)
    {
        if (!submission.ResearchMilestoneId.HasValue)
        {
            throw ErrorHelper.BadRequest("This submission is not linked to a research milestone.");
        }
    }

    public static void ValidateAssignmentAvailability(Assignment assignment, DateTime utcNow)
    {
        if (assignment.AvailableFrom.HasValue && utcNow < assignment.AvailableFrom.Value)
        {
            throw ErrorHelper.Forbidden("Assignment is not yet available.");
        }

        if (assignment.AvailableUntil.HasValue && utcNow > assignment.AvailableUntil.Value)
        {
            throw ErrorHelper.Conflict("Assignment is no longer available.");
        }
    }

    public static void ValidateSubmitContent(CreateResearchSubmissionRequestDto request)
    {
        var hasText = !string.IsNullOrWhiteSpace(request.ContentText);
        var hasFile = !string.IsNullOrWhiteSpace(request.FileUrl);
        var hasEvidence = request.EvidenceUrls?.Any(url => !string.IsNullOrWhiteSpace(url)) == true;

        if (!hasText && !hasFile && !hasEvidence)
        {
            throw ErrorHelper.BadRequest(
                "At least one of ContentText, FileUrl, or EvidenceUrls is required.");
        }
    }

    public static void ValidateUploadFile(IFormFile file)
    {
        if (file == null || file.Length == 0)
        {
            throw ErrorHelper.BadRequest("File is required.");
        }

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!AllowedUploadExtensions.Contains(extension))
        {
            throw ErrorHelper.BadRequest(
                "File type not supported. Allowed: PDF (.pdf), DOC (.doc, .docx), ZIP (.zip), " +
                "Image (.jpg, .jpeg, .png, .gif, .webp), Video (.mp4, .mov, .avi, .mkv).");
        }

        var maxSize = ImageExtensions.Contains(extension)
            ? MaxImageSize
            : VideoExtensions.Contains(extension)
                ? MaxVideoSize
                : MaxDocSize;

        if (file.Length > maxSize)
        {
            var label = ImageExtensions.Contains(extension)
                ? "Image file size must not exceed 10 MB."
                : VideoExtensions.Contains(extension)
                    ? "Video file size must not exceed 3 GB."
                    : "File size must not exceed 50 MB.";

            throw ErrorHelper.BadRequest(label);
        }
    }

    public static void ValidateSubmissionOwnership(Submission submission, Guid studentId)
    {
        if (submission.StudentId != studentId)
        {
            throw ErrorHelper.Forbidden("You do not have access to this submission.");
        }
    }

    public static void ValidateSubmissionOpenForSubmit(Submission submission)
    {
        if (submission.Status is not (SubmissionStatus.Pending or SubmissionStatus.ReturnedForRevision))
        {
            throw ErrorHelper.Conflict("This submission is not open for submission.");
        }
    }

    public static void ValidateSubmissionAwaitingGrade(Submission submission)
    {
        if (submission.Status != SubmissionStatus.TurnedIn)
        {
            throw ErrorHelper.Conflict("Only turned-in submissions can be graded.");
        }
    }

    public static int GetNextAttemptNumber(Submission submission)
        => submission.AttemptNumber + 1;

    public static void ValidateMaxAttemptsNotExceeded(Assignment assignment, int nextAttemptNumber)
    {
        if (nextAttemptNumber > assignment.MaxAttempts)
        {
            throw ErrorHelper.Conflict(
                $"Maximum number of attempts ({assignment.MaxAttempts}) has been reached for this assignment.");
        }
    }

    public static (bool CanSubmit, List<string> SubmitBlockReasons) EvaluateStudentSubmitEligibility(
        bool isUnlocked,
        IReadOnlyList<string> activityBlockReasons,
        Assignment assignment,
        Submission? submission,
        DateTime utcNow)
    {
        var submitBlockReasons = new List<string>();

        if (!isUnlocked)
        {
            submitBlockReasons.Add("Milestone is locked.");
        }

        submitBlockReasons.AddRange(activityBlockReasons);

        if (assignment.AvailableFrom.HasValue && utcNow < assignment.AvailableFrom.Value)
        {
            submitBlockReasons.Add("Assignment is not yet available.");
        }

        if (assignment.AvailableUntil.HasValue && utcNow > assignment.AvailableUntil.Value)
        {
            submitBlockReasons.Add("Assignment is no longer available.");
        }

        if (submission == null)
        {
            submitBlockReasons.Add("Mentor has not opened submission yet.");
            return (false, submitBlockReasons);
        }

        if (submission.Status == SubmissionStatus.TurnedIn)
        {
            submitBlockReasons.Add("Submission is pending mentor review.");
            return (false, submitBlockReasons);
        }

        if (submission.Status == SubmissionStatus.Graded)
        {
            submitBlockReasons.Add("This milestone submission has already been graded.");
            return (false, submitBlockReasons);
        }

        if (submission.Status is SubmissionStatus.Pending or SubmissionStatus.ReturnedForRevision)
        {
            var nextAttemptNumber = GetNextAttemptNumber(submission);
            if (nextAttemptNumber > assignment.MaxAttempts)
            {
                submitBlockReasons.Add(
                    $"Maximum number of attempts ({assignment.MaxAttempts}) has been reached for this assignment.");
                return (false, submitBlockReasons);
            }

            if (submitBlockReasons.Count > 0)
            {
                return (false, submitBlockReasons);
            }

            return (true, submitBlockReasons);
        }

        submitBlockReasons.Add("Submission is not open for submission.");
        return (false, submitBlockReasons);
    }

    public static async Task<User> EnsureCanStartSubmissionAsync(
        IUnitOfWork unitOfWork,
        IClaimsService claimsService,
        Guid moduleId,
        Guid studentId)
    {
        var user = await GetCurrentUserAsync(unitOfWork, claimsService);

        if (user.Role is RoleType.SuperAdmin or RoleType.Manager)
        {
            return user;
        }

        if (user.Role == RoleType.Mentor)
        {
            var module = await unitOfWork.Modules.GetByIdAsync(moduleId);
            if (module == null || module.IsDeleted)
            {
                throw ErrorHelper.NotFound($"Module with id '{moduleId}' not found.");
            }

            await MentorScopeValidator.EnsureMentorOwnsStudentInProgramAsync(
                unitOfWork,
                user.Id,
                studentId,
                module.ProgramId);
            return user;
        }

        throw ErrorHelper.Forbidden(StartSubmissionForbiddenMessage);
    }

    public static async Task<User> EnsureCanGradeSubmissionAsync(
        IUnitOfWork unitOfWork,
        IClaimsService claimsService,
        Guid moduleId,
        Guid studentId)
    {
        var user = await GetCurrentUserAsync(unitOfWork, claimsService);

        if (user.Role is RoleType.SuperAdmin or RoleType.Manager)
        {
            return user;
        }

        if (user.Role == RoleType.Mentor)
        {
            var module = await unitOfWork.Modules.GetByIdAsync(moduleId);
            if (module == null || module.IsDeleted)
            {
                throw ErrorHelper.NotFound($"Module with id '{moduleId}' not found.");
            }

            await MentorScopeValidator.EnsureMentorOwnsStudentInProgramAsync(
                unitOfWork,
                user.Id,
                studentId,
                module.ProgramId);
            return user;
        }

        throw ErrorHelper.Forbidden(GradeSubmissionForbiddenMessage);
    }

    public static async Task EnsureCanViewSubmissionAsync(
        IUnitOfWork unitOfWork,
        IClaimsService claimsService,
        Submission submission)
    {
        var userId = claimsService.GetCurrentUserId;
        if (userId == Guid.Empty)
        {
            throw ErrorHelper.Unauthorized("Unauthorized access.");
        }

        if (submission.StudentId == userId)
        {
            return;
        }

        var user = await unitOfWork.Users.GetByIdAsync(userId);
        if (user == null || user.IsDeleted)
        {
            throw ErrorHelper.NotFound("Current user not found.");
        }

        if (user.Role is RoleType.SuperAdmin or RoleType.Manager)
        {
            return;
        }

        if (user.Role == RoleType.Parent)
        {
            var parentLink = await unitOfWork.ParentStudents.FirstOrDefaultAsync(
                ps => ps.ParentId == userId && ps.StudentId == submission.StudentId && !ps.IsDeleted);

            if (parentLink != null)
            {
                return;
            }

            throw ErrorHelper.Forbidden("You can only view submissions of students linked to your account.");
        }

        if (user.Role == RoleType.Mentor)
        {
            var assignment = await unitOfWork.Assignments.GetByIdAsync(submission.AssignmentId);
            if (assignment == null || assignment.IsDeleted)
            {
                throw ErrorHelper.NotFound($"Assignment with id '{submission.AssignmentId}' not found.");
            }

            var module = await unitOfWork.Modules.GetByIdAsync(assignment.ModuleId);
            if (module == null || module.IsDeleted)
            {
                throw ErrorHelper.NotFound($"Module with id '{assignment.ModuleId}' not found.");
            }

            await MentorScopeValidator.EnsureMentorOwnsStudentInProgramAsync(
                unitOfWork,
                user.Id,
                submission.StudentId,
                module.ProgramId);
            return;
        }

        throw ErrorHelper.Forbidden(ViewSubmissionForbiddenMessage);
    }

    public static async Task ValidateMilestoneReadyForOpenAsync(
        IUnitOfWork unitOfWork,
        ModuleEnrollment enrollment,
        ResearchMilestone milestone,
        Assignment assignment,
        IReadOnlyDictionary<Guid, Submission> submissionsByMilestoneId,
        IReadOnlyDictionary<Guid, Assignment> assignmentsById,
        IReadOnlySet<Guid> completedActivityIds,
        DateTime utcNow)
    {
        var moduleMilestones = await unitOfWork.ResearchMilestones.GetAllAsync(
            rm => rm.ModuleId == milestone.ModuleId && !rm.IsDeleted);

        var orderedMilestones = moduleMilestones.OrderBy(rm => rm.MilestoneOrder).ToList();
        var milestoneIndex = orderedMilestones.FindIndex(rm => rm.Id == milestone.Id);
        if (milestoneIndex < 0)
        {
            throw ErrorHelper.BadRequest("Milestone does not belong to the enrollment module.");
        }

        if (milestoneIndex > 0)
        {
            var previousMilestone = orderedMilestones[milestoneIndex - 1];
            if (!ResearchMilestoneValidator.HasPassedSubmission(
                    previousMilestone,
                    submissionsByMilestoneId,
                    assignmentsById))
            {
                throw ErrorHelper.Forbidden(
                    $"Complete milestone '{previousMilestone.Title}' with a passing grade before opening this milestone.");
            }
        }

        var activityLinks = await ResearchMilestoneValidator.LoadActivityLinksAsync(unitOfWork, milestone.Id);
        foreach (var link in activityLinks.Where(l => l.IsRequiredForSubmission))
        {
            if (link.Activity == null || link.Activity.IsDeleted)
            {
                continue;
            }

            if (!completedActivityIds.Contains(link.ActivityId))
            {
                throw ErrorHelper.Forbidden(
                    $"Required activity '{link.Activity.Name}' is not completed.");
            }
        }

        ValidateAssignmentAvailability(assignment, utcNow);
    }

    public static string GenerateSubmissionCode()
        => $"SUB-{Guid.NewGuid():N}"[..12].ToUpperInvariant();

    public static async Task<List<Guid>> LoadModuleMilestoneIdsAsync(
        IUnitOfWork unitOfWork,
        Guid moduleId)
    {
        var milestones = await unitOfWork.ResearchMilestones.GetAllAsync(
            rm => rm.ModuleId == moduleId && !rm.IsDeleted);

        return milestones.Select(rm => rm.Id).ToList();
    }

    public static async Task<List<string>> LoadEvidenceUrlsAsync(
        IUnitOfWork unitOfWork,
        Guid submissionId)
    {
        var evidences = await unitOfWork.SubmissionEvidences.GetAllAsync(
            se => se.SubmissionId == submissionId && !se.IsDeleted,
            se => se.Media);

        return evidences
            .Where(se => se.Media != null && !se.Media.IsDeleted && !string.IsNullOrWhiteSpace(se.Media.FileUrl))
            .Select(se => se.Media!.FileUrl!)
            .ToList();
    }

    public static async Task ReplaceEvidenceUrlsAsync(
        IUnitOfWork unitOfWork,
        Submission submission,
        List<string>? evidenceUrls,
        Guid uploaderId,
        DateTime now)
    {
        var existingEvidences = await unitOfWork.SubmissionEvidences.GetAllAsync(
            se => se.SubmissionId == submission.Id && !se.IsDeleted);

        foreach (var evidence in existingEvidences)
        {
            await unitOfWork.SubmissionEvidences.SoftRemove(evidence);
        }

        var urls = evidenceUrls?
            .Where(url => !string.IsNullOrWhiteSpace(url))
            .Select(url => url.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList() ?? [];

        foreach (var url in urls)
        {
            var media = new MediaAsset
            {
                Id = Guid.NewGuid(),
                UploaderId = uploaderId,
                FileUrl = url,
                UploadedAt = now,
                CreatedAt = now,
                CreatedBy = uploaderId,
                IsDeleted = false
            };

            await unitOfWork.MediaAssets.AddAsync(media);

            var evidence = new SubmissionEvidence
            {
                SubmissionId = submission.Id,
                MediaId = media.Id,
                CreatedAt = now,
                CreatedBy = uploaderId,
                IsDeleted = false
            };

            await unitOfWork.SubmissionEvidences.AddAsync(evidence);
        }
    }

    private static async Task<User> GetCurrentUserAsync(
        IUnitOfWork unitOfWork,
        IClaimsService claimsService)
    {
        var userId = claimsService.GetCurrentUserId;
        if (userId == Guid.Empty)
        {
            throw ErrorHelper.Unauthorized("Unauthorized access.");
        }

        var user = await unitOfWork.Users.GetByIdAsync(userId);
        if (user == null || user.IsDeleted)
        {
            throw ErrorHelper.NotFound("Current user not found.");
        }

        return user;
    }
}
