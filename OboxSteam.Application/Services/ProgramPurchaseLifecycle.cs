using Microsoft.Extensions.Logging;
using OboxSteam.Application.Interfaces;
using OboxSteam.Application.Notifications;
using OboxSteam.Application.Validation;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;
using OboxSteam.Domain.Interfaces;

namespace OboxSteam.Application.Services;

/// <summary>
/// Closes a program purchase when the student fails or withdraws. AcademicFail/Attendance
/// map to <see cref="EnrollmentStatus.Failed"/>; Withdraw maps to <see cref="EnrollmentStatus.Dropped"/>.
/// All Active/Pending class seats are withdrawn immediately.
/// </summary>
public sealed class ProgramPurchaseLifecycle
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentTime _currentTime;
    private readonly INotificationPublisher _notificationPublisher;
    private readonly ILogger<ProgramPurchaseLifecycle> _logger;

    public ProgramPurchaseLifecycle(
        IUnitOfWork unitOfWork,
        ICurrentTime currentTime,
        INotificationPublisher notificationPublisher,
        ILogger<ProgramPurchaseLifecycle> logger)
    {
        _unitOfWork = unitOfWork;
        _currentTime = currentTime;
        _notificationPublisher = notificationPublisher;
        _logger = logger;
    }

    /// <summary>
    /// Closes the purchase and withdraws every Active/Pending class seat. No-op when the
    /// enrollment is already terminal (Failed/Dropped/Completed).
    /// </summary>
    public async Task CloseAsync(
        ProgramEnrollment enrollment,
        ProgramPurchaseEndReason reason,
        Guid? endedModuleId = null)
    {
        if (enrollment.Status is EnrollmentStatus.Failed or EnrollmentStatus.Dropped or EnrollmentStatus.Completed)
        {
            _logger.LogInformation(
                "[CloseAsync] Program enrollment {EnrollmentId} already {Status}; skipping close.",
                enrollment.Id,
                enrollment.Status);
            return;
        }

        enrollment.Status = reason == ProgramPurchaseEndReason.Withdraw
            ? EnrollmentStatus.Dropped
            : EnrollmentStatus.Failed;
        enrollment.EndReason = reason;
        enrollment.EndedModuleId = endedModuleId;
        enrollment.EndedAt = _currentTime.GetCurrentTime();

        await _unitOfWork.ProgramEnrollments.Update(enrollment);

        var seats = await _unitOfWork.ClassEnrollments.GetAllAsync(
            ce => ce.ProgramEnrollmentId == enrollment.Id
                  && !ce.IsDeleted
                  && (ce.Status == ClassEnrollmentStatus.Active || ce.Status == ClassEnrollmentStatus.Pending));

        foreach (var seat in seats)
        {
            seat.Status = ClassEnrollmentStatus.Withdrawn;
            seat.HoldExpiresAt = null;
            await _unitOfWork.ClassEnrollments.Update(seat);
        }

        await _unitOfWork.SaveChangesAsync();

        var module = endedModuleId.HasValue
            ? await _unitOfWork.Modules.GetByIdAsync(endedModuleId.Value)
            : null;

        await _notificationPublisher.PublishAsync(
            NotificationCatalog.ModuleFailed(
                enrollment.StudentId,
                endedModuleId ?? Guid.Empty,
                moduleEnrollmentId: null,
                programId: enrollment.ProgramId,
                moduleName: module?.Name,
                programEnrollmentId: enrollment.Id));

        _logger.LogWarning(
            "[CloseAsync] Program enrollment {EnrollmentId} closed — reason {Reason}, ended module {ModuleId}.",
            enrollment.Id,
            reason,
            endedModuleId);
    }

    /// <summary>
    /// After a submission is graded fail, closes the program purchase when the student has
    /// exhausted the effective attempt budget and the recovery cap for the assignment.
    /// No-op when the enrollment is already terminal, attempts remain, or the recovery cap
    /// has not been reached.
    /// </summary>
    public async Task TryCloseAfterFailedAssignmentAsync(
        Guid studentId,
        Guid assignmentId,
        Guid? moduleEnrollmentId)
    {
        var assignment = await _unitOfWork.Assignments.GetByIdAsync(assignmentId);
        if (assignment == null || assignment.IsDeleted)
        {
            return;
        }

        var module = await _unitOfWork.Modules.GetByIdAsync(assignment.ModuleId);
        if (AssessmentAttemptPolicy.IsUnlimitedAttempts(module))
        {
            return;
        }

        ModuleEnrollment? moduleEnrollment = null;
        if (moduleEnrollmentId.HasValue)
        {
            moduleEnrollment = await _unitOfWork.ModuleEnrollments.GetByIdAsync(moduleEnrollmentId.Value);
        }
        else
        {
            moduleEnrollment = await _unitOfWork.ModuleEnrollments.FirstOrDefaultAsync(
                me => me.StudentId == studentId
                      && me.ModuleId == assignment.ModuleId
                      && !me.IsDeleted
                      && (me.Status == EnrollmentStatus.Active || me.Status == EnrollmentStatus.Deferred));
        }

        if (moduleEnrollment == null || !moduleEnrollment.ProgramEnrollmentId.HasValue)
        {
            return;
        }

        var programEnrollment = await _unitOfWork.ProgramEnrollments.GetByIdAsync(
            moduleEnrollment.ProgramEnrollmentId.Value);
        if (programEnrollment == null || programEnrollment.IsDeleted)
        {
            return;
        }

        var submissions = await _unitOfWork.Submissions.GetAllAsync(
            s => s.AssignmentId == assignmentId
                 && s.StudentId == studentId
                 && s.ModuleEnrollmentId == moduleEnrollment.Id
                 && !s.IsDeleted);

        var latestGraded = submissions
            .Where(s => s.Status == SubmissionStatus.Graded)
            .OrderByDescending(s => s.AttemptNumber)
            .FirstOrDefault();

        if (latestGraded == null
            || !latestGraded.AssignedGrade.HasValue
            || latestGraded.AssignedGrade.Value >= assignment.PassScore)
        {
            return;
        }

        var effectiveMax = await AssessmentAttemptPolicy.GetEffectiveMaxAttemptsAsync(
            _unitOfWork,
            assignment,
            studentId,
            moduleEnrollment.Id);

        if (latestGraded.AttemptNumber < effectiveMax)
        {
            return;
        }

        var decidedCount = (await _unitOfWork.AssessmentRecoveryRequests.GetAllAsync(
            r => r.StudentId == studentId
                 && r.AssignmentId == assignmentId
                 && r.ModuleEnrollmentId == moduleEnrollment.Id
                 && !r.IsDeleted
                 && (r.Status == AssessmentRecoveryRequestStatus.Approved
                     || r.Status == AssessmentRecoveryRequestStatus.Rejected))).Count;

        if (decidedCount < AssessmentAttemptPolicy.MaxRecoveryRequestsPerAssignment)
        {
            return;
        }

        await CloseAsync(
            programEnrollment,
            ProgramPurchaseEndReason.AcademicFail,
            assignment.ModuleId);
    }
}
