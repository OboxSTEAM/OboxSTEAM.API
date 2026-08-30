using Microsoft.Extensions.Logging;
using OboxSteam.Application.DTOs.RetrospectiveDTO;
using OboxSteam.Application.Interfaces;
using OboxSteam.Application.Utils;
using OboxSteam.Application.Validation;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;
using OboxSteam.Domain.Interfaces;

namespace OboxSteam.Application.Services;

/// <summary>
/// Student retrospective flow: start or resume a plain-text draft, autosave, and turn in for mentor grading.
/// </summary>
public sealed class RetrospectiveAttemptService : IRetrospectiveAttemptService
{
    private readonly IClaimsService _claimsService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<RetrospectiveAttemptService> _logger;
    private readonly ProgramPurchaseLifecycle _programPurchaseLifecycle;

    public RetrospectiveAttemptService(
        IClaimsService claimsService,
        IUnitOfWork unitOfWork,
        ILogger<RetrospectiveAttemptService> logger,
        ProgramPurchaseLifecycle programPurchaseLifecycle)
    {
        _claimsService = claimsService;
        _unitOfWork = unitOfWork;
        _logger = logger;
        _programPurchaseLifecycle = programPurchaseLifecycle;
    }

    public async Task<RetrospectiveAttemptResponseDto> StartRetrospective(Guid assignmentId)
    {
        var student = await EnrollmentAccessValidator.GetCurrentStudentForEnrollAsync(
            _unitOfWork,
            _claimsService,
            RetrospectiveAttemptValidator.RetrospectiveForbiddenMessage);

        RetrospectiveAttemptValidator.ValidateAssignmentIdRequired(assignmentId);

        var assignment = await _unitOfWork.Assignments.GetByIdAsync(assignmentId);
        RetrospectiveAttemptValidator.ValidateAssignmentForRetrospective(assignment);

        var now = DateTime.UtcNow;

        var enrollment = await QuizAttemptValidator.ValidateActiveModuleEnrollmentAsync(
            _unitOfWork,
            student.Id,
            assignment!);

        var submission = await _unitOfWork.Submissions.FirstOrDefaultAsync(
            s => s.AssignmentId == assignment!.Id
                 && s.ModuleEnrollmentId == enrollment.Id
                 && !s.IsDeleted);

        if (submission != null)
        {
            RetrospectiveAttemptValidator.ValidateSubmissionNotResearch(submission);
            RetrospectiveAttemptValidator.ValidateSubmissionOwnership(submission, student.Id);
            RetrospectiveAttemptValidator.ValidateCanStartOrResume(submission);

            _logger.LogInformation(
                "StartRetrospective resuming submission. SubmissionId={SubmissionId}, Status={Status}, StudentId={StudentId}",
                submission.Id,
                submission.Status,
                student.Id);

            return MapToDto(submission, assignment!);
        }

        var window = await AssignmentWindowPolicy.TryGetForStudentAsync(
            _unitOfWork,
            assignment!.Id,
            student.Id);
        await _programPurchaseLifecycle.TryCloseIfWindowBlocksNewAttemptAsync(
            student.Id,
            assignment.Id,
            enrollment.Id,
            window,
            now);
        RetrospectiveAttemptValidator.ValidateAssignmentAvailability(window, now);

        var newSubmission = new Submission
        {
            Id = Guid.NewGuid(),
            Code = ResearchSubmissionValidator.GenerateSubmissionCode(),
            AssignmentId = assignment!.Id,
            StudentId = student.Id,
            ModuleEnrollmentId = enrollment.Id,
            AttemptNumber = 1,
            Status = SubmissionStatus.Pending,
            StartedAt = now,
            ExpiresAt = AssignmentValidator.ResolveAttemptExpiresAt(assignment.TimeLimitMinutes, now),
            CreatedAt = now,
            CreatedBy = student.Id,
            IsDeleted = false
        };

        await _unitOfWork.Submissions.AddAsync(newSubmission);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation(
            "StartRetrospective created draft. SubmissionId={SubmissionId}, AssignmentId={AssignmentId}, StudentId={StudentId}",
            newSubmission.Id,
            assignmentId,
            student.Id);

        return MapToDto(newSubmission, assignment);
    }

    public async Task<RetrospectiveAttemptResponseDto?> GetRetrospective(Guid submissionId)
    {
        var submission = await _unitOfWork.Submissions.GetByIdAsync(submissionId);
        if (submission == null || submission.IsDeleted)
        {
            return null;
        }

        RetrospectiveAttemptValidator.ValidateSubmissionNotResearch(submission);
        await ResearchSubmissionValidator.EnsureCanViewSubmissionAsync(
            _unitOfWork,
            _claimsService,
            submission);

        var assignment = await _unitOfWork.Assignments.GetByIdAsync(submission.AssignmentId);
        if (assignment == null || assignment.IsDeleted)
        {
            return null;
        }

        RetrospectiveAttemptValidator.ValidateAssignmentForRetrospective(assignment);

        return MapToDto(submission, assignment);
    }

    public async Task<SaveRetrospectiveDraftResponseDto> SaveDraft(
        Guid submissionId,
        SaveRetrospectiveDraftRequestDto request)
    {
        var student = await EnrollmentAccessValidator.GetCurrentStudentForEnrollAsync(
            _unitOfWork,
            _claimsService,
            RetrospectiveAttemptValidator.RetrospectiveForbiddenMessage);

        var submission = await _unitOfWork.Submissions.GetByIdAsync(submissionId);
        RetrospectiveAttemptValidator.ValidateSubmissionExists(submission, submissionId);
        RetrospectiveAttemptValidator.ValidateSubmissionNotResearch(submission!);
        RetrospectiveAttemptValidator.ValidateSubmissionOwnership(submission!, student.Id);
        RetrospectiveAttemptValidator.ValidateSubmissionOpenForDraft(submission!);

        var assignment = await _unitOfWork.Assignments.GetByIdAsync(submission!.AssignmentId);
        RetrospectiveAttemptValidator.ValidateAssignmentForRetrospective(assignment);

        await QuizAttemptValidator.ValidateActiveModuleEnrollmentAsync(
            _unitOfWork,
            student.Id,
            assignment!);

        var now = DateTime.UtcNow;

        submission.ContentText = RetrospectiveAttemptValidator.NormalizeDraftContentText(request.ContentText);
        submission.UpdatedAt = now;
        submission.UpdatedBy = student.Id;

        await _unitOfWork.Submissions.Update(submission);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation(
            "SaveRetrospectiveDraft saved. SubmissionId={SubmissionId}, StudentId={StudentId}",
            submissionId,
            student.Id);

        return new SaveRetrospectiveDraftResponseDto
        {
            LastSavedAt = now
        };
    }

    public async Task<RetrospectiveAttemptResponseDto> SubmitRetrospective(
        Guid submissionId,
        SubmitRetrospectiveRequestDto request)
    {
        var student = await EnrollmentAccessValidator.GetCurrentStudentForEnrollAsync(
            _unitOfWork,
            _claimsService,
            RetrospectiveAttemptValidator.RetrospectiveForbiddenMessage);

        var submission = await _unitOfWork.Submissions.GetByIdAsync(submissionId);
        RetrospectiveAttemptValidator.ValidateSubmissionExists(submission, submissionId);
        RetrospectiveAttemptValidator.ValidateSubmissionNotResearch(submission!);
        RetrospectiveAttemptValidator.ValidateSubmissionOwnership(submission!, student.Id);
        RetrospectiveAttemptValidator.ValidateSubmissionOpenForSubmit(submission!);

        var assignment = await _unitOfWork.Assignments.GetByIdAsync(submission!.AssignmentId);
        RetrospectiveAttemptValidator.ValidateAssignmentForRetrospective(assignment);

        await QuizAttemptValidator.ValidateActiveModuleEnrollmentAsync(
            _unitOfWork,
            student.Id,
            assignment!);

        var now = DateTime.UtcNow;

        var mergedContent = RetrospectiveAttemptValidator.NormalizeDraftContentText(request.ContentText)
            ?? submission.ContentText;

        RetrospectiveAttemptValidator.ValidateFinalContentText(mergedContent);

        if (submission.Status == SubmissionStatus.ReturnedForRevision
            || submission.Status == SubmissionStatus.Graded)
        {
            var nextAttemptNumber = ResearchSubmissionValidator.GetNextAttemptNumber(submission);
            await ResearchSubmissionValidator.ValidateMaxAttemptsNotExceededAsync(
                _unitOfWork,
                assignment!,
                student.Id,
                nextAttemptNumber,
                submission.ModuleEnrollmentId);
            submission.AttemptNumber = nextAttemptNumber;
        }

        submission.ContentText = mergedContent;
        submission.FileUrl = null;
        submission.Status = SubmissionStatus.TurnedIn;
        submission.SubmittedAt = now;
        submission.UpdatedAt = now;
        submission.UpdatedBy = student.Id;

        await _unitOfWork.Submissions.Update(submission);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation(
            "SubmitRetrospective turned in. SubmissionId={SubmissionId}, AssignmentId={AssignmentId}, StudentId={StudentId}",
            submission.Id,
            assignment!.Id,
            student.Id);

        return MapToDto(submission, assignment);
    }

    private static RetrospectiveAttemptResponseDto MapToDto(Submission submission, Assignment assignment)
    {
        bool? passed = submission.Status == SubmissionStatus.Graded && submission.AssignedGrade.HasValue
            ? submission.AssignedGrade.Value >= assignment.PassScore
            : null;

        return new RetrospectiveAttemptResponseDto
        {
            SubmissionId = submission.Id,
            AssignmentId = assignment.Id,
            Title = assignment.Title,
            Description = assignment.Description,
            AttemptNumber = submission.AttemptNumber,
            Status = submission.Status,
            ContentText = submission.ContentText,
            LastSavedAt = submission.UpdatedAt,
            AssignedGrade = submission.AssignedGrade,
            PassScore = assignment.PassScore,
            MaxPoints = assignment.MaxPoints,
            Passed = passed,
            MentorFeedback = submission.MentorFeedback,
            SubmittedAt = submission.SubmittedAt,
            GradedAt = submission.GradedAt
        };
    }
}
