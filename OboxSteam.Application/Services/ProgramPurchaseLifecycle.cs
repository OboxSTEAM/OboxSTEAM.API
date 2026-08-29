using Microsoft.Extensions.Logging;
using OboxSteam.Application.Commons;
using OboxSteam.Application.Interfaces;
using OboxSteam.Application.Notifications;
using OboxSteam.Application.Utils;
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
    /// <summary>Months after a purchase closes during which a rebuy keeps retake pricing and progress credit.</summary>
    public const int RebuyWindowMonths = 3;

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

    /// <summary>
    /// Latest closed purchase (Failed/Dropped/Completed) a rebuy links to, or null for a first purchase.
    /// </summary>
    public static ProgramEnrollment? FindRebuySource(IEnumerable<ProgramEnrollment> enrollments)
        => enrollments
            .Where(pe => pe.Status is EnrollmentStatus.Failed or EnrollmentStatus.Dropped or EnrollmentStatus.Completed)
            .OrderByDescending(pe => pe.EndedAt ?? pe.CompletedAt ?? pe.EnrolledAt ?? DateTime.MinValue)
            .FirstOrDefault();

    /// <summary>
    /// True when <paramref name="now"/> falls within the rebuy window anchored at the source's close
    /// date (<see cref="ProgramEnrollment.EndedAt"/> for Failed/Dropped, <see cref="ProgramEnrollment.CompletedAt"/>
    /// for Completed), inclusive of the boundary.
    /// </summary>
    public static bool IsWithinRebuyWindow(ProgramEnrollment source, DateTime now)
    {
        var anchor = source.EndedAt ?? source.CompletedAt;
        return anchor.HasValue && now <= anchor.Value.AddMonths(RebuyWindowMonths);
    }

    /// <summary>
    /// Checkout amount for a (possibly rebuy) pending enrollment: <see cref="Program.RetakeFee"/>
    /// (fallback <see cref="Program.Price"/>) inside the rebuy window, full Price otherwise.
    /// </summary>
    public async Task<decimal> ResolveCheckoutAmountAsync(Program program, ProgramEnrollment enrollment)
    {
        var price = program.Price ?? 0m;
        if (!enrollment.SourceProgramEnrollmentId.HasValue || program.Price == null)
        {
            return price;
        }

        var source = await _unitOfWork.ProgramEnrollments.GetByIdAsync(enrollment.SourceProgramEnrollmentId.Value);
        if (source == null || !IsWithinRebuyWindow(source, _currentTime.GetCurrentTime()))
        {
            return price;
        }

        return program.RetakeFee ?? price;
    }

    /// <summary>
    /// For rebuys after a close, the selected class must not have started the module the student
    /// stopped at, nor any later module. Failed sources use <see cref="ProgramEnrollment.EndedModuleId"/>;
    /// Withdraw sources use the first not-Completed module in <see cref="Module.ModuleOrder"/>.
    /// Completed sources are unconstrained (no stop module).
    /// </summary>
    public async Task ValidateRebuyClassEligibilityAsync(ProgramEnrollment enrollment, Guid classId)
    {
        if (!enrollment.SourceProgramEnrollmentId.HasValue)
        {
            return;
        }

        var source = await _unitOfWork.ProgramEnrollments.GetByIdAsync(enrollment.SourceProgramEnrollmentId.Value);
        if (source == null || source.Status == EnrollmentStatus.Completed)
        {
            return;
        }

        var stopModuleId = source.EndedModuleId;
        if (!stopModuleId.HasValue)
        {
            stopModuleId = await ResolveWithdrawStopModuleIdAsync(source);
        }

        if (!stopModuleId.HasValue)
        {
            return;
        }

        var stopModule = await _unitOfWork.Modules.GetByIdAsync(stopModuleId.Value);
        if (stopModule == null || stopModule.IsDeleted)
        {
            return;
        }

        var blockedSessions = await _unitOfWork.ClassSessions.GetAllAsync(
            cs => cs.ClassId == classId
                  && !cs.IsDeleted
                  && (cs.Status == ClassSessionStatus.InProgress || cs.Status == ClassSessionStatus.Completed));

        var blockedModuleIds = blockedSessions.Select(cs => cs.ModuleId).Distinct().ToList();
        if (blockedModuleIds.Count == 0)
        {
            return;
        }

        var blockedModules = await _unitOfWork.Modules.GetAllAsync(
            m => m.ProgramId == source.ProgramId
                 && blockedModuleIds.Contains(m.Id)
                 && !m.IsDeleted);

        if (blockedModules.Any(m => m.ModuleOrder >= stopModule.ModuleOrder))
        {
            throw ErrorHelper.BadRequest(
                "This class has already started the module you stopped at or a later module. Choose a class that has not started it yet.");
        }
    }

    private async Task<Guid?> ResolveWithdrawStopModuleIdAsync(ProgramEnrollment source)
    {
        var moduleEnrollments = await _unitOfWork.ModuleEnrollments.GetAllAsync(
            me => me.ProgramEnrollmentId == source.Id && !me.IsDeleted);

        var completedModuleIds = moduleEnrollments
            .Where(me => me.Status == EnrollmentStatus.Completed)
            .Select(me => me.ModuleId)
            .ToHashSet();

        var modules = await _unitOfWork.Modules.GetAllAsync(
            m => m.ProgramId == source.ProgramId && !m.IsDeleted);

        return modules
            .Where(m => !completedModuleIds.Contains(m.Id))
            .OrderBy(m => m.ModuleOrder)
            .Select(m => (Guid?)m.Id)
            .FirstOrDefault();
    }

    /// <summary>
    /// On rebuy payment success, copies the source enrollment's Completed module enrollments
    /// (with their ActivityProgress rows and Graded submissions) into the new enrollment.
    /// Only applies inside the rebuy window; Completed sources and out-of-window rebuys copy
    /// nothing. Each copied module enrollment gets the next global AttemptNumber per
    /// (student, module) so the (StudentId, ModuleId, AttemptNumber) unique index holds.
    /// Idempotent: modules already present on the new enrollment are skipped.
    /// </summary>
    public async Task ApplyRebuyCreditsAsync(ProgramEnrollment enrollment)
    {
        if (!enrollment.SourceProgramEnrollmentId.HasValue)
        {
            return;
        }

        var source = await _unitOfWork.ProgramEnrollments.GetByIdAsync(enrollment.SourceProgramEnrollmentId.Value);
        if (source == null || source.Status == EnrollmentStatus.Completed)
        {
            return;
        }

        if (!IsWithinRebuyWindow(source, _currentTime.GetCurrentTime()))
        {
            return;
        }

        var completedSources = await _unitOfWork.ModuleEnrollments.GetAllAsync(
            me => me.ProgramEnrollmentId == source.Id
                  && !me.IsDeleted
                  && me.Status == EnrollmentStatus.Completed);
        if (completedSources.Count == 0)
        {
            return;
        }

        var existingOnNew = await _unitOfWork.ModuleEnrollments.GetAllAsync(
            me => me.ProgramEnrollmentId == enrollment.Id && !me.IsDeleted);
        var alreadyCopiedModuleIds = existingOnNew.Select(me => me.ModuleId).ToHashSet();

        var now = _currentTime.GetCurrentTime();

        foreach (var sourceModuleEnrollment in completedSources)
        {
            if (!alreadyCopiedModuleIds.Add(sourceModuleEnrollment.ModuleId))
            {
                continue;
            }

            var allAttempts = await _unitOfWork.ModuleEnrollments.GetAllAsync(
                me => me.StudentId == enrollment.StudentId
                      && me.ModuleId == sourceModuleEnrollment.ModuleId
                      && !me.IsDeleted);
            var nextAttempt = allAttempts.Count == 0 ? 1 : allAttempts.Max(me => me.AttemptNumber) + 1;

            var copiedEnrollment = new ModuleEnrollment
            {
                Id = Guid.NewGuid(),
                StudentId = enrollment.StudentId,
                ModuleId = sourceModuleEnrollment.ModuleId,
                ProgramEnrollmentId = enrollment.Id,
                Status = EnrollmentStatus.Completed,
                ProgressPercent = 100m,
                FinalGrade = sourceModuleEnrollment.FinalGrade,
                AttemptNumber = nextAttempt,
                EnrolledAt = now,
                StartedAt = now,
                CompletedAt = now,
            };
            await _unitOfWork.ModuleEnrollments.AddAsync(copiedEnrollment);

            var progresses = await _unitOfWork.ActivityProgresses.GetAllAsync(
                ap => ap.ModuleEnrollmentId == sourceModuleEnrollment.Id && !ap.IsDeleted);
            foreach (var progress in progresses)
            {
                await _unitOfWork.ActivityProgresses.AddAsync(new ActivityProgress
                {
                    StudentId = enrollment.StudentId,
                    ActivityId = progress.ActivityId,
                    ModuleEnrollmentId = copiedEnrollment.Id,
                    ActivityStatus = progress.ActivityStatus,
                    IsCompleted = progress.IsCompleted,
                    CompletedAt = progress.CompletedAt,
                    CompletionSource = progress.CompletionSource,
                    ResumeState = progress.ResumeState,
                    LastAccessedAt = progress.LastAccessedAt,
                });
            }

            var gradedSubmissions = await _unitOfWork.Submissions.GetAllAsync(
                s => s.ModuleEnrollmentId == sourceModuleEnrollment.Id
                     && !s.IsDeleted
                     && s.Status == SubmissionStatus.Graded);
            foreach (var submission in gradedSubmissions)
            {
                await _unitOfWork.Submissions.AddAsync(new Submission
                {
                    Code = ResearchSubmissionValidator.GenerateSubmissionCode(),
                    AssignmentId = submission.AssignmentId,
                    StudentId = enrollment.StudentId,
                    ModuleEnrollmentId = copiedEnrollment.Id,
                    AttemptNumber = submission.AttemptNumber,
                    Status = SubmissionStatus.Graded,
                    ContentText = submission.ContentText,
                    FileUrl = submission.FileUrl,
                    AssignedGrade = submission.AssignedGrade,
                    MentorFeedback = submission.MentorFeedback,
                    VerifiedBy = submission.VerifiedBy,
                    StartedAt = submission.StartedAt,
                    ExpiresAt = submission.ExpiresAt,
                    SubmittedAt = submission.SubmittedAt,
                    ResearchMilestoneId = submission.ResearchMilestoneId,
                    GradedAt = submission.GradedAt,
                });
            }
        }

        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation(
            "[ApplyRebuyCreditsAsync] Copied {Count} completed module(s) from source {SourceId} to enrollment {EnrollmentId}.",
            completedSources.Count,
            source.Id,
            enrollment.Id);
    }

    /// <summary>
    /// Manager backup path: reopens a purchase closed by <see cref="ProgramPurchaseEndReason.Attendance"/>
    /// when an attendance correction brings the missed ratio below the fail threshold.
    /// Restores the failed module enrollment and every withdrawn class seat of the enrollment.
    /// </summary>
    public async Task<bool> TryReopenAfterAttendanceCorrectionAsync(ProgramEnrollment enrollment, Guid moduleId)
    {
        if (enrollment.Status != EnrollmentStatus.Failed
            || enrollment.EndReason != ProgramPurchaseEndReason.Attendance
            || enrollment.EndedModuleId != moduleId)
        {
            return false;
        }

        var moduleEnrollments = await _unitOfWork.ModuleEnrollments.GetAllAsync(
            me => me.ProgramEnrollmentId == enrollment.Id
                  && me.ModuleId == moduleId
                  && !me.IsDeleted
                  && me.Status == EnrollmentStatus.Failed);
        var failedModuleEnrollment = moduleEnrollments
            .OrderByDescending(me => me.AttemptNumber)
            .FirstOrDefault();
        if (failedModuleEnrollment == null)
        {
            return false;
        }

        var missed = await ModuleAbsencePolicy.CountMissedAsync(_unitOfWork, failedModuleEnrollment.Id);
        var total = await ModuleAbsencePolicy.CountSessionActivitiesAsync(_unitOfWork, moduleId);
        if (ModuleAbsencePolicy.ShouldFail(missed, total))
        {
            return false;
        }

        await ReopenAsync(enrollment, failedModuleEnrollment);
        return true;
    }

    /// <summary>
    /// Manager backup path: reopens a purchase closed by <see cref="ProgramPurchaseEndReason.AcademicFail"/>
    /// when a failing grade is corrected to a passing one. Attempt counts and recovery decisions
    /// are left untouched - the corrected pass stands. After reopening, module/program progress is
    /// recalculated so the corrected pass can complete the module (and program) naturally.
    /// </summary>
    public async Task<bool> TryReopenAfterGradeCorrectionAsync(Submission submission, Assignment assignment)
    {
        if (!submission.ModuleEnrollmentId.HasValue
            || submission.Status != SubmissionStatus.Graded
            || !submission.AssignedGrade.HasValue
            || submission.AssignedGrade.Value < assignment.PassScore)
        {
            return false;
        }

        var moduleEnrollment = await _unitOfWork.ModuleEnrollments.GetByIdAsync(
            submission.ModuleEnrollmentId.Value);
        if (moduleEnrollment == null
            || moduleEnrollment.IsDeleted
            || !moduleEnrollment.ProgramEnrollmentId.HasValue)
        {
            return false;
        }

        var enrollment = await _unitOfWork.ProgramEnrollments.GetByIdAsync(
            moduleEnrollment.ProgramEnrollmentId.Value);
        if (enrollment == null
            || enrollment.IsDeleted
            || enrollment.Status != EnrollmentStatus.Failed
            || enrollment.EndReason != ProgramPurchaseEndReason.AcademicFail
            || enrollment.EndedModuleId != moduleEnrollment.ModuleId)
        {
            return false;
        }

        await ReopenAsync(enrollment, moduleEnrollment);

        await ActivityProgressCalculationHelper.RecalculateModuleProgressAsync(_unitOfWork, moduleEnrollment);
        await ActivityProgressCalculationHelper.RecalculateProgramProgressAsync(
            _unitOfWork,
            enrollment.Id,
            moduleEnrollment);
        await _unitOfWork.SaveChangesAsync();
        return true;
    }

    private async Task ReopenAsync(ProgramEnrollment enrollment, ModuleEnrollment failedModuleEnrollment)
    {
        enrollment.Status = EnrollmentStatus.Active;
        enrollment.EndReason = null;
        enrollment.EndedModuleId = null;
        enrollment.EndedAt = null;
        await _unitOfWork.ProgramEnrollments.Update(enrollment);

        if (failedModuleEnrollment.Status == EnrollmentStatus.Failed)
        {
            failedModuleEnrollment.Status = EnrollmentStatus.Active;
            await _unitOfWork.ModuleEnrollments.Update(failedModuleEnrollment);
        }

        var seats = await _unitOfWork.ClassEnrollments.GetAllAsync(
            ce => ce.ProgramEnrollmentId == enrollment.Id
                  && !ce.IsDeleted
                  && ce.Status == ClassEnrollmentStatus.Withdrawn);
        foreach (var seat in seats)
        {
            seat.Status = ClassEnrollmentStatus.Active;
            await _unitOfWork.ClassEnrollments.Update(seat);
        }

        await _unitOfWork.SaveChangesAsync();

        _logger.LogWarning(
            "[ReopenAsync] Program enrollment {EnrollmentId} reopened — module enrollment {ModuleEnrollmentId} restored, {SeatCount} seat(s) reactivated.",
            enrollment.Id,
            failedModuleEnrollment.Id,
            seats.Count);
    }
}
