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

    public const string RebuyClassIneligibleMessage =
        "This class has already started the module you stopped at or a later module. Choose a class that has not started it yet.";

    public const string RebuySameClassMessage =
        "You cannot rejoin the class you previously enrolled in. Choose a different class.";

    public const string ReopenBlockedByOpenPurchaseMessage =
        "This purchase cannot be reopened because the student already has an open checkout or enrollment for this program.";

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
    /// Closes the purchase, terminals every open module enrollment, and withdraws every
    /// Active/Pending class seat. No-op when the enrollment is already terminal
    /// (Failed/Dropped/Completed).
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

        var endedModuleEnrollmentId = await CloseOpenModuleEnrollmentsAsync(
            enrollment.Id,
            reason,
            endedModuleId);

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

        await PublishCloseNotificationAsync(
            enrollment,
            reason,
            endedModuleId,
            endedModuleEnrollmentId);

        _logger.LogWarning(
            "[CloseAsync] Program enrollment {EnrollmentId} closed — reason {Reason}, ended module {ModuleId}.",
            enrollment.Id,
            reason,
            endedModuleId);
    }

    /// <summary>
    /// Terminalizes open module enrollments on this purchase. Completed rows stay Completed.
    /// AcademicFail/Attendance: the ended module becomes Failed; other Active/Deferred rows
    /// become Dropped. Withdraw: every Active/Deferred row becomes Dropped (none are Failed).
    /// </summary>
    private async Task<Guid?> CloseOpenModuleEnrollmentsAsync(
        Guid programEnrollmentId,
        ProgramPurchaseEndReason reason,
        Guid? endedModuleId)
    {
        var moduleEnrollments = await _unitOfWork.ModuleEnrollments.GetAllAsync(
            me => me.ProgramEnrollmentId == programEnrollmentId && !me.IsDeleted);

        Guid? endedModuleEnrollmentId = null;
        foreach (var moduleEnrollment in moduleEnrollments)
        {
            if (moduleEnrollment.Status is EnrollmentStatus.Completed)
            {
                continue;
            }

            if (reason == ProgramPurchaseEndReason.Withdraw)
            {
                if (moduleEnrollment.Status is EnrollmentStatus.Active or EnrollmentStatus.Deferred)
                {
                    moduleEnrollment.Status = EnrollmentStatus.Dropped;
                    await _unitOfWork.ModuleEnrollments.Update(moduleEnrollment);
                }

                continue;
            }

            if (endedModuleId.HasValue && moduleEnrollment.ModuleId == endedModuleId.Value)
            {
                moduleEnrollment.Status = EnrollmentStatus.Failed;
                endedModuleEnrollmentId = moduleEnrollment.Id;
                await _unitOfWork.ModuleEnrollments.Update(moduleEnrollment);
                continue;
            }

            if (moduleEnrollment.Status is EnrollmentStatus.Active or EnrollmentStatus.Deferred)
            {
                moduleEnrollment.Status = EnrollmentStatus.Dropped;
                await _unitOfWork.ModuleEnrollments.Update(moduleEnrollment);
            }
        }

        return endedModuleEnrollmentId;
    }

    private async Task PublishCloseNotificationAsync(
        ProgramEnrollment enrollment,
        ProgramPurchaseEndReason reason,
        Guid? endedModuleId,
        Guid? endedModuleEnrollmentId)
    {
        var program = await _unitOfWork.Programs.GetByIdAsync(enrollment.ProgramId);
        var student = await _unitOfWork.Users.GetByIdAsync(enrollment.StudentId);

        if (reason == ProgramPurchaseEndReason.Withdraw)
        {
            await _notificationPublisher.PublishAsync(
                NotificationCatalog.ProgramWithdrawn(
                    enrollment.StudentId,
                    enrollment.ProgramId,
                    enrollment.Id,
                    program?.Name,
                    student?.FullName));
            return;
        }

        var module = endedModuleId.HasValue
            ? await _unitOfWork.Modules.GetByIdAsync(endedModuleId.Value)
            : null;

        await _notificationPublisher.PublishAsync(
            NotificationCatalog.ModuleFailed(
                enrollment.StudentId,
                endedModuleId ?? Guid.Empty,
                endedModuleEnrollmentId,
                enrollment.ProgramId,
                module?.Name,
                enrollment.Id,
                studentName: student?.FullName,
                programName: program?.Name,
                reason: reason));
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
        if (assignment == null || assignment.IsDeleted || !assignment.IsRequiredForModulePass)
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

        if (submissions.Any(s => s.Status == SubmissionStatus.TurnedIn && !s.IsDeleted))
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
    /// Fail/drop rebuys inside the 3-month window may join Open or InProgress classes
    /// (stop-module gated). First purchase, Completed retakes, and fail/drop after the
    /// window only join Open classes (fresh start; credit copy is already off).
    /// </summary>
    public static bool AllowsInProgressClassJoin(ProgramEnrollment? source, DateTime now)
        => source is { Status: EnrollmentStatus.Failed or EnrollmentStatus.Dropped }
           && IsWithinRebuyWindow(source, now);

    /// <inheritdoc cref="AllowsInProgressClassJoin(ProgramEnrollment?, DateTime)"/>
    public bool AllowsInProgressClassJoin(ProgramEnrollment? source)
        => AllowsInProgressClassJoin(source, _currentTime.GetCurrentTime());

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
    public static decimal ResolveCheckoutAmount(Program program, ProgramEnrollment? source, DateTime now)
    {
        var price = program.Price ?? 0m;
        if (source == null || program.Price == null || !IsWithinRebuyWindow(source, now))
        {
            return price;
        }

        return program.RetakeFee ?? price;
    }

    public async Task<decimal> ResolveCheckoutAmountAsync(Program program, ProgramEnrollment enrollment)
    {
        ProgramEnrollment? source = null;
        if (enrollment.SourceProgramEnrollmentId.HasValue)
        {
            source = await _unitOfWork.ProgramEnrollments.GetByIdAsync(enrollment.SourceProgramEnrollmentId.Value);
        }

        return ResolveCheckoutAmount(program, source, _currentTime.GetCurrentTime());
    }

    /// <summary>
    /// For rebuys after a close, the selected class must not be the class the student
    /// left. Inside the rebuy window, it also must not have started the module the
    /// student stopped at, nor any later module. Failed sources use
    /// <see cref="ProgramEnrollment.EndedModuleId"/>; Withdraw sources use the first
    /// not-Completed module in <see cref="Module.ModuleOrder"/>. Completed sources and
    /// fail/drop checkouts after the window skip the stop-module rule (Open-only join)
    /// but still cannot rejoin the old class.
    /// </summary>
    public async Task ValidateRebuyClassEligibilityAsync(ProgramEnrollment enrollment, Guid classId)
    {
        if (!enrollment.SourceProgramEnrollmentId.HasValue)
        {
            return;
        }

        var source = await _unitOfWork.ProgramEnrollments.GetByIdAsync(enrollment.SourceProgramEnrollmentId.Value);
        if (source == null)
        {
            return;
        }

        var sourceClassIds = await GetSourceOccupiedClassIdsAsync(source.Id);
        if (sourceClassIds.Contains(classId))
        {
            throw ErrorHelper.BadRequest(RebuySameClassMessage);
        }

        if (source.Status == EnrollmentStatus.Completed
            || !IsWithinRebuyWindow(source, _currentTime.GetCurrentTime()))
        {
            return;
        }

        var stopModuleId = await ResolveStopModuleIdAsync(source);
        if (!stopModuleId.HasValue)
        {
            return;
        }

        var stopModule = await _unitOfWork.Modules.GetByIdAsync(stopModuleId.Value);
        if (stopModule == null || stopModule.IsDeleted)
        {
            return;
        }

        var classSessions = await _unitOfWork.ClassSessions.GetAllAsync(
            cs => cs.ClassId == classId && !cs.IsDeleted);

        var programModules = await _unitOfWork.Modules.GetAllAsync(
            m => m.ProgramId == source.ProgramId && !m.IsDeleted);

        if (ClassBlocksRebuy(programModules, classSessions, stopModule.ModuleOrder))
        {
            throw ErrorHelper.BadRequest(RebuyClassIneligibleMessage);
        }
    }

    /// <summary>
    /// Stop module for class eligibility: <see cref="ProgramEnrollment.EndedModuleId"/> on fail,
    /// first not-Completed module on withdraw. Null for <see cref="EnrollmentStatus.Completed"/>
    /// (unconstrained) or when every module is already completed.
    /// </summary>
    public async Task<Guid?> ResolveStopModuleIdAsync(ProgramEnrollment source)
    {
        if (source.Status == EnrollmentStatus.Completed)
        {
            return null;
        }

        if (source.EndedModuleId.HasValue)
        {
            return source.EndedModuleId;
        }

        return await ResolveWithdrawStopModuleIdAsync(source);
    }

    /// <summary>
    /// LiveOnline/Offline teaching slots. AssignmentWindow is a work period, not taught time.
    /// </summary>
    public static bool IsTeachingSession(ClassSession session)
        => !session.IsDeleted
           && session.Status != ClassSessionStatus.Cancelled
           && session.SessionKind is SessionKind.LiveOnline or SessionKind.Offline;

    public static ClassModuleProgressStatus ResolveModuleProgress(IEnumerable<ClassSession> sessionsForModule)
    {
        var furthest = ClassModuleProgressStatus.NotStarted;
        foreach (var session in sessionsForModule)
        {
            if (!IsTeachingSession(session))
            {
                continue;
            }

            var mapped = session.Status switch
            {
                ClassSessionStatus.Completed => ClassModuleProgressStatus.Completed,
                ClassSessionStatus.InProgress => ClassModuleProgressStatus.InProgress,
                _ => ClassModuleProgressStatus.NotStarted,
            };

            if (mapped > furthest)
            {
                furthest = mapped;
            }
        }

        return furthest;
    }

    public static bool ModuleProgressBlocksRebuy(ClassModuleProgressStatus progress)
        => progress is ClassModuleProgressStatus.InProgress or ClassModuleProgressStatus.Completed;

    public static bool ClassBlocksRebuy(
        IReadOnlyCollection<Module> programModules,
        IReadOnlyCollection<ClassSession> classSessions,
        int stopModuleOrder)
    {
        var sessionsByModule = classSessions.GroupBy(cs => cs.ModuleId);
        foreach (var group in sessionsByModule)
        {
            var module = programModules.FirstOrDefault(m => m.Id == group.Key);
            if (module == null || module.ModuleOrder < stopModuleOrder)
            {
                continue;
            }

            if (ModuleProgressBlocksRebuy(ResolveModuleProgress(group)))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Classes the student already sat in (or held) on the source purchase. Rebuy must pick a different class.
    /// </summary>
    public async Task<HashSet<Guid>> GetSourceOccupiedClassIdsAsync(Guid sourceProgramEnrollmentId)
    {
        var seats = await _unitOfWork.ClassEnrollments.GetAllAsync(
            ce => ce.ProgramEnrollmentId == sourceProgramEnrollmentId && !ce.IsDeleted);

        return seats.Select(ce => ce.ClassId).ToHashSet();
    }

    /// <summary>
    /// True when the new class has already gone past this teaching session: status Completed,
    /// or the session end time is at or before <paramref name="now"/> (calendar already moved
    /// on, even if the mentor left the row Scheduled). AssignmentWindow, cancelled, and
    /// deleted rows never count. In-progress lives whose end is still in the future are not
    /// taught yet.
    /// </summary>
    public static bool SessionAlreadyTaught(ClassSession session, DateTime now)
    {
        if (!IsTeachingSession(session))
        {
            return false;
        }

        if (session.Status == ClassSessionStatus.Completed)
        {
            return true;
        }

        return session.EndTime <= now;
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
    /// into the new enrollment, scoped to what the new class has already taught by wall-clock
    /// and LiveOnline/Offline status. AssignmentWindow rows are ignored. A module with no
    /// teaching sessions (self-paced / unscheduled) is copied whole. A module whose every
    /// teaching session is already taught (<see cref="SessionAlreadyTaught"/>: Completed, or
    /// EndTime &lt;= now) is copied whole. Lives still in the future are not copied — the
    /// student relearns those with the new class. A part-taught module copies only
    /// ActivityProgress and Graded submissions whose Activity/Assignment the class has already
    /// taught. ProgressPercent is then set by
    /// <see cref="ActivityProgressCalculationHelper"/>. Only applies inside the rebuy window;
    /// Completed sources and out-of-window rebuys copy nothing. Each copied module enrollment
    /// gets the next global AttemptNumber per (student, module). Idempotent: modules already
    /// present on the new enrollment are skipped.
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

        // Prefer the Active seat; fall back to the Pending hold if copy runs before activation.
        var seats = await _unitOfWork.ClassEnrollments.GetAllAsync(
            ce => ce.ProgramEnrollmentId == enrollment.Id
                  && !ce.IsDeleted
                  && (ce.Status == ClassEnrollmentStatus.Active
                      || ce.Status == ClassEnrollmentStatus.Pending));
        var newClassEnrollment = seats.FirstOrDefault(ce => ce.Status == ClassEnrollmentStatus.Active)
            ?? seats.FirstOrDefault();
        if (newClassEnrollment == null)
        {
            return;
        }

        var newClassSessions = await _unitOfWork.ClassSessions.GetAllAsync(
            cs => cs.ClassId == newClassEnrollment.ClassId && !cs.IsDeleted);

        var now = _currentTime.GetCurrentTime();

        var taughtActivityIdsByModule = new Dictionary<Guid, HashSet<Guid>>();
        var taughtAssignmentIdsByModule = new Dictionary<Guid, HashSet<Guid>>();
        var moduleFullyTaught = new Dictionary<Guid, bool>();
        foreach (var group in newClassSessions.GroupBy(cs => cs.ModuleId))
        {
            var teachable = group.Where(IsTeachingSession).ToList();
            var taughtSessions = teachable.Where(cs => SessionAlreadyTaught(cs, now)).ToList();
            taughtActivityIdsByModule[group.Key] = taughtSessions
                .Where(cs => cs.ActivityId.HasValue)
                .Select(cs => cs.ActivityId!.Value)
                .ToHashSet();
            taughtAssignmentIdsByModule[group.Key] = taughtSessions
                .Where(cs => cs.AssignmentId.HasValue)
                .Select(cs => cs.AssignmentId!.Value)
                .ToHashSet();

            moduleFullyTaught[group.Key] =
                teachable.Count > 0 && teachable.All(cs => SessionAlreadyTaught(cs, now));
        }

        var existingOnNew = await _unitOfWork.ModuleEnrollments.GetAllAsync(
            me => me.ProgramEnrollmentId == enrollment.Id && !me.IsDeleted);
        var alreadyCopiedModuleIds = existingOnNew.Select(me => me.ModuleId).ToHashSet();

        var copiedRows = new List<(ModuleEnrollment Copied, ModuleEnrollment Source, bool FullyTaught)>();

        foreach (var sourceModuleEnrollment in completedSources)
        {
            if (!alreadyCopiedModuleIds.Add(sourceModuleEnrollment.ModuleId))
            {
                continue;
            }

            var moduleId = sourceModuleEnrollment.ModuleId;
            var taughtActivityIds = taughtActivityIdsByModule.GetValueOrDefault(moduleId) ?? [];
            var taughtAssignmentIds = taughtAssignmentIdsByModule.GetValueOrDefault(moduleId) ?? [];
            var hasTeachableSessions = newClassSessions.Any(
                cs => cs.ModuleId == moduleId && IsTeachingSession(cs));
            var fullyTaught = moduleFullyTaught.GetValueOrDefault(moduleId) || !hasTeachableSessions;

            if (!fullyTaught && taughtActivityIds.Count == 0 && taughtAssignmentIds.Count == 0)
            {
                continue;
            }

            var sourceProgresses = await _unitOfWork.ActivityProgresses.GetAllAsync(
                ap => ap.ModuleEnrollmentId == sourceModuleEnrollment.Id && !ap.IsDeleted);
            var sourceSubmissions = await _unitOfWork.Submissions.GetAllAsync(
                s => s.ModuleEnrollmentId == sourceModuleEnrollment.Id
                     && !s.IsDeleted
                     && s.Status == SubmissionStatus.Graded);

            var progressesToCopy = fullyTaught
                ? sourceProgresses
                : sourceProgresses.Where(ap => taughtActivityIds.Contains(ap.ActivityId)).ToList();
            var submissionsToCopy = fullyTaught
                ? sourceSubmissions
                : sourceSubmissions.Where(s => taughtAssignmentIds.Contains(s.AssignmentId)).ToList();

            var allAttempts = await _unitOfWork.ModuleEnrollments.GetAllAsync(
                me => me.StudentId == enrollment.StudentId
                      && me.ModuleId == moduleId
                      && !me.IsDeleted);
            var nextAttempt = allAttempts.Count == 0 ? 1 : allAttempts.Max(me => me.AttemptNumber) + 1;

            var copiedEnrollment = new ModuleEnrollment
            {
                Id = Guid.NewGuid(),
                StudentId = enrollment.StudentId,
                ModuleId = moduleId,
                ProgramEnrollmentId = enrollment.Id,
                Status = EnrollmentStatus.Active,
                ProgressPercent = 0m,
                FinalGrade = null,
                AttemptNumber = nextAttempt,
                EnrolledAt = now,
                StartedAt = now,
                CompletedAt = null,
            };
            await _unitOfWork.ModuleEnrollments.AddAsync(copiedEnrollment);

            foreach (var progress in progressesToCopy)
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

            foreach (var submission in submissionsToCopy)
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

            copiedRows.Add((copiedEnrollment, sourceModuleEnrollment, fullyTaught));
        }

        await _unitOfWork.SaveChangesAsync();

        foreach (var row in copiedRows)
        {
            await ActivityProgressCalculationHelper.RecalculateModuleProgressAsync(
                _unitOfWork,
                row.Copied);
            if (row.FullyTaught && row.Copied.Status == EnrollmentStatus.Completed)
            {
                row.Copied.FinalGrade = row.Source.FinalGrade;
                await _unitOfWork.ModuleEnrollments.Update(row.Copied);
            }
        }

        if (copiedRows.Count > 0)
        {
            await ActivityProgressCalculationHelper.RecalculateProgramProgressAsync(
                _unitOfWork,
                enrollment.Id,
                copiedRows[0].Copied);
        }

        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation(
            "[ApplyRebuyCreditsAsync] Copied {Count} completed module(s) from source {SourceId} to enrollment {EnrollmentId}.",
            copiedRows.Count,
            source.Id,
            enrollment.Id);
    }

    /// <summary>
    /// Reject a passing AcademicFail correction before any grade or progress is written when
    /// the student already has an Active or PendingPayment purchase of the same program.
    /// </summary>
    public async Task ThrowIfPassingGradeCorrectionIsBlockedAsync(
        Guid? moduleEnrollmentId,
        SubmissionStatus proposedStatus,
        decimal? proposedGrade,
        Assignment assignment)
    {
        var target = await TryResolveAcademicFailReopenAsync(
            moduleEnrollmentId,
            proposedStatus,
            proposedGrade,
            assignment);
        if (target == null)
        {
            return;
        }

        await ThrowIfHasOpenSiblingPurchaseAsync(target.Value.Enrollment);
    }

    /// <summary>
    /// Reject an attendance correction that would reopen an Attendance close before any
    /// attendance row is written, when the student already has an open sibling purchase.
    /// </summary>
    public async Task ThrowIfAttendanceCorrectionIsBlockedAsync(
        ProgramEnrollment enrollment,
        Guid moduleId,
        Guid sessionId,
        AttendanceStatus proposedStatus)
    {
        if (proposedStatus == AttendanceStatus.Absent)
        {
            return;
        }

        var failedModuleEnrollment = await TryResolveAttendanceReopenModuleAsync(enrollment, moduleId);
        if (failedModuleEnrollment == null)
        {
            return;
        }

        var missed = await CountMissedAfterProposedAsync(
            failedModuleEnrollment.Id,
            sessionId,
            proposedStatus);
        var total = await ModuleAbsencePolicy.CountSessionActivitiesAsync(_unitOfWork, moduleId);
        if (ModuleAbsencePolicy.ShouldFail(missed, total))
        {
            return;
        }

        await ThrowIfHasOpenSiblingPurchaseAsync(enrollment);
    }

    /// <summary>
    /// Manager backup path: reopens a purchase closed by <see cref="ProgramPurchaseEndReason.Attendance"/>
    /// when an attendance correction brings the missed ratio below the fail threshold.
    /// Restores the failed module enrollment and every withdrawn class seat of the enrollment.
    /// Refuses with Conflict when the student already has an Active or PendingPayment purchase
    /// of the same program.
    /// </summary>
    public async Task<bool> TryReopenAfterAttendanceCorrectionAsync(ProgramEnrollment enrollment, Guid moduleId)
    {
        var failedModuleEnrollment = await TryResolveAttendanceReopenModuleAsync(enrollment, moduleId);
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
    /// Refuses with Conflict when the student already has an Active or PendingPayment purchase
    /// of the same program.
    /// </summary>
    public async Task<bool> TryReopenAfterGradeCorrectionAsync(Submission submission, Assignment assignment)
    {
        var target = await TryResolveAcademicFailReopenAsync(
            submission.ModuleEnrollmentId,
            submission.Status,
            submission.AssignedGrade,
            assignment);
        if (target == null)
        {
            return false;
        }

        await ReopenAsync(target.Value.Enrollment, target.Value.Module);

        await ActivityProgressCalculationHelper.RecalculateModuleProgressAsync(_unitOfWork, target.Value.Module);
        await ActivityProgressCalculationHelper.RecalculateProgramProgressAsync(
            _unitOfWork,
            target.Value.Enrollment.Id,
            target.Value.Module);
        await _unitOfWork.SaveChangesAsync();
        return true;
    }

    private async Task<(ProgramEnrollment Enrollment, ModuleEnrollment Module)?> TryResolveAcademicFailReopenAsync(
        Guid? moduleEnrollmentId,
        SubmissionStatus status,
        decimal? assignedGrade,
        Assignment assignment)
    {
        if (!moduleEnrollmentId.HasValue
            || status != SubmissionStatus.Graded
            || !assignedGrade.HasValue
            || assignedGrade.Value < assignment.PassScore)
        {
            return null;
        }

        var moduleEnrollment = await _unitOfWork.ModuleEnrollments.GetByIdAsync(moduleEnrollmentId.Value);
        if (moduleEnrollment == null
            || moduleEnrollment.IsDeleted
            || !moduleEnrollment.ProgramEnrollmentId.HasValue)
        {
            return null;
        }

        var enrollment = await _unitOfWork.ProgramEnrollments.GetByIdAsync(
            moduleEnrollment.ProgramEnrollmentId.Value);
        if (enrollment == null
            || enrollment.IsDeleted
            || enrollment.Status != EnrollmentStatus.Failed
            || enrollment.EndReason != ProgramPurchaseEndReason.AcademicFail
            || enrollment.EndedModuleId != moduleEnrollment.ModuleId)
        {
            return null;
        }

        return (enrollment, moduleEnrollment);
    }

    private async Task<ModuleEnrollment?> TryResolveAttendanceReopenModuleAsync(
        ProgramEnrollment enrollment,
        Guid moduleId)
    {
        if (enrollment.Status != EnrollmentStatus.Failed
            || enrollment.EndReason != ProgramPurchaseEndReason.Attendance
            || enrollment.EndedModuleId != moduleId)
        {
            return null;
        }

        var moduleEnrollments = await _unitOfWork.ModuleEnrollments.GetAllAsync(
            me => me.ProgramEnrollmentId == enrollment.Id
                  && me.ModuleId == moduleId
                  && !me.IsDeleted
                  && me.Status == EnrollmentStatus.Failed);
        return moduleEnrollments
            .OrderByDescending(me => me.AttemptNumber)
            .FirstOrDefault();
    }

    private async Task<int> CountMissedAfterProposedAsync(
        Guid moduleEnrollmentId,
        Guid sessionId,
        AttendanceStatus proposedStatus)
    {
        var records = await _unitOfWork.SessionAttendances.GetAllAsync(
            sa => sa.ModuleEnrollmentId == moduleEnrollmentId && !sa.IsDeleted);

        var absentSessionIds = records
            .Where(sa => sa.ClassSessionId != sessionId && sa.Status == AttendanceStatus.Absent)
            .Select(sa => sa.ClassSessionId)
            .ToHashSet();
        if (proposedStatus == AttendanceStatus.Absent)
        {
            absentSessionIds.Add(sessionId);
        }

        if (absentSessionIds.Count == 0)
        {
            return 0;
        }

        var sessions = await _unitOfWork.ClassSessions.GetAllAsync(
            cs => absentSessionIds.Contains(cs.Id)
                  && cs.ActivityId != null
                  && !cs.IsDeleted);

        return sessions
            .Where(cs => cs.ActivityId.HasValue)
            .Select(cs => cs.ActivityId!.Value)
            .Distinct()
            .Count();
    }

    private async Task ThrowIfHasOpenSiblingPurchaseAsync(ProgramEnrollment enrollment)
    {
        var openSiblings = await _unitOfWork.ProgramEnrollments.GetAllAsync(
            pe => pe.StudentId == enrollment.StudentId
                  && pe.ProgramId == enrollment.ProgramId
                  && pe.Id != enrollment.Id
                  && !pe.IsDeleted
                  && (pe.Status == EnrollmentStatus.PendingPayment
                      || pe.Status == EnrollmentStatus.Active));
        if (openSiblings.Count > 0)
        {
            throw ErrorHelper.Conflict(ReopenBlockedByOpenPurchaseMessage);
        }
    }

    private async Task ReopenAsync(ProgramEnrollment enrollment, ModuleEnrollment failedModuleEnrollment)
    {
        await ThrowIfHasOpenSiblingPurchaseAsync(enrollment);

        enrollment.Status = EnrollmentStatus.Active;
        enrollment.EndReason = null;
        enrollment.EndedModuleId = null;
        enrollment.EndedAt = null;
        await _unitOfWork.ProgramEnrollments.Update(enrollment);

        if (failedModuleEnrollment.Status is EnrollmentStatus.Failed or EnrollmentStatus.Dropped)
        {
            failedModuleEnrollment.Status = EnrollmentStatus.Active;
            await _unitOfWork.ModuleEnrollments.Update(failedModuleEnrollment);
        }

        var siblingDropped = await _unitOfWork.ModuleEnrollments.GetAllAsync(
            me => me.ProgramEnrollmentId == enrollment.Id
                  && me.Id != failedModuleEnrollment.Id
                  && !me.IsDeleted
                  && me.Status == EnrollmentStatus.Dropped);
        foreach (var sibling in siblingDropped)
        {
            sibling.Status = EnrollmentStatus.Active;
            await _unitOfWork.ModuleEnrollments.Update(sibling);
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

    public const int NextMilestoneWindowPadHours = 48;

    /// <summary>
    /// Required work whose class window has ended, with no in-progress continuation and
    /// nothing waiting to be graded, closes the purchase (including Theory).
    /// </summary>
    public async Task TryCloseAfterAssignmentWindowElapsedAsync(
        Guid studentId,
        Guid assignmentId,
        Guid? moduleEnrollmentId,
        ClassSession? window = null)
    {
        var assignment = await _unitOfWork.Assignments.GetByIdAsync(assignmentId);
        if (assignment == null || assignment.IsDeleted || !assignment.IsRequiredForModulePass)
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

        if (moduleEnrollment == null || moduleEnrollment.IsDeleted || !moduleEnrollment.ProgramEnrollmentId.HasValue)
        {
            return;
        }

        var programEnrollment = await _unitOfWork.ProgramEnrollments.GetByIdAsync(
            moduleEnrollment.ProgramEnrollmentId.Value);
        if (programEnrollment == null
            || programEnrollment.IsDeleted
            || programEnrollment.Status is EnrollmentStatus.Failed
                or EnrollmentStatus.Dropped
                or EnrollmentStatus.Completed)
        {
            return;
        }

        var now = _currentTime.GetCurrentTime();
        window ??= await AssignmentWindowPolicy.TryGetForStudentAsync(
            _unitOfWork,
            assignmentId,
            studentId);
        if (window == null || now <= window.EndTime)
        {
            return;
        }

        var submissions = await _unitOfWork.Submissions.GetAllAsync(
            s => s.AssignmentId == assignmentId
                 && s.StudentId == studentId
                 && s.ModuleEnrollmentId == moduleEnrollment.Id
                 && !s.IsDeleted);

        if (HasPassedRequiredWork(submissions, assignment)
            || HasWaitingGrade(submissions)
            || submissions.Any(s => AssignmentWindowPolicy.IsBlockingInProgress(s, now)))
        {
            return;
        }

        await CloseAsync(programEnrollment, ProgramPurchaseEndReason.AcademicFail, assignment.ModuleId);
    }

    public async Task TryCloseIfWindowBlocksNewAttemptAsync(
        Guid studentId,
        Guid assignmentId,
        Guid? moduleEnrollmentId,
        ClassSession? window,
        DateTime utcNow)
    {
        if (AssignmentWindowPolicy.GetNewAttemptBlockReason(window, utcNow)
            != AssignmentWindowPolicy.ClosedMessage)
        {
            return;
        }

        await TryCloseAfterAssignmentWindowElapsedAsync(
            studentId,
            assignmentId,
            moduleEnrollmentId,
            window);
    }

    /// <summary>
    /// Closes Active purchases whose required AssignmentWindow has already ended.
    /// </summary>
    public async Task<int> CloseElapsedRequiredWindowsAsync(CancellationToken cancellationToken = default)
    {
        if (SeedExecutionGuard.IsSeeding)
        {
            return 0;
        }

        var now = _currentTime.GetCurrentTime();
        var windows = await _unitOfWork.ClassSessions.GetAllAsync(
            cs => cs.SessionKind == SessionKind.AssignmentWindow
                  && cs.AssignmentId != null
                  && cs.Status != ClassSessionStatus.Cancelled
                  && !cs.IsDeleted
                  && cs.EndTime < now);

        if (windows.Count == 0)
        {
            return 0;
        }

        var closed = 0;
        var seen = new HashSet<(Guid ClassId, Guid AssignmentId)>();
        foreach (var window in windows)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var assignmentId = window.AssignmentId!.Value;
            if (!seen.Add((window.ClassId, assignmentId)))
            {
                continue;
            }

            var canonical = await AssignmentWindowPolicy.TryGetForClassAsync(
                _unitOfWork,
                window.ClassId,
                assignmentId);
            if (canonical == null || now <= canonical.EndTime)
            {
                continue;
            }

            var seats = await _unitOfWork.ClassEnrollments.GetAllAsync(
                ce => ce.ClassId == window.ClassId
                      && ce.Status == ClassEnrollmentStatus.Active
                      && !ce.IsDeleted);

            foreach (var seat in seats)
            {
                var closedBefore = (await _unitOfWork.ProgramEnrollments.GetByIdAsync(seat.ProgramEnrollmentId))
                    ?.Status;
                await TryCloseAfterAssignmentWindowElapsedAsync(
                    seat.StudentId,
                    assignmentId,
                    null,
                    canonical);
                var closedAfter = (await _unitOfWork.ProgramEnrollments.GetByIdAsync(seat.ProgramEnrollmentId))
                    ?.Status;
                if (closedBefore != EnrollmentStatus.Failed && closedAfter == EnrollmentStatus.Failed)
                {
                    closed++;
                }
            }
        }

        return closed;
    }

    public async Task TryExtendNextMilestoneWindowAfterPassAsync(
        Assignment assignment,
        Guid studentId,
        Guid? moduleEnrollmentId = null)
    {
        var milestone = await _unitOfWork.ResearchMilestones.FirstOrDefaultAsync(
            rm => rm.AssignmentId == assignment.Id && !rm.IsDeleted);
        if (milestone == null)
        {
            return;
        }

        var next = (await _unitOfWork.ResearchMilestones.GetAllAsync(
                rm => rm.ModuleId == milestone.ModuleId && !rm.IsDeleted))
            .Where(rm => rm.MilestoneOrder > milestone.MilestoneOrder)
            .OrderBy(rm => rm.MilestoneOrder)
            .FirstOrDefault();
        if (next == null)
        {
            return;
        }

        ClassSession? window = null;
        if (moduleEnrollmentId.HasValue)
        {
            var moduleEnrollment = await _unitOfWork.ModuleEnrollments.GetByIdAsync(moduleEnrollmentId.Value);
            if (moduleEnrollment?.ProgramEnrollmentId is Guid programEnrollmentId)
            {
                var windows = await AssignmentWindowPolicy.LoadWindowsForProgramEnrollmentAsync(
                    _unitOfWork,
                    studentId,
                    programEnrollmentId);
                windows.TryGetValue(next.AssignmentId, out window);
            }
        }
        else
        {
            window = await AssignmentWindowPolicy.TryGetForStudentAsync(
                _unitOfWork,
                next.AssignmentId,
                studentId);
        }

        if (window == null)
        {
            return;
        }

        var now = _currentTime.GetCurrentTime();
        var pad = now.AddHours(NextMilestoneWindowPadHours);
        if (now <= window.EndTime && window.EndTime - now >= TimeSpan.FromHours(NextMilestoneWindowPadHours))
        {
            return;
        }

        var newEnd = window.EndTime > pad ? window.EndTime : pad;
        if (newEnd <= window.EndTime)
        {
            return;
        }

        window.EndTime = newEnd;
        await _unitOfWork.ClassSessions.Update(window);
        await _unitOfWork.SaveChangesAsync();
    }

    public static RebuyModuleCreditHint ResolveCreditHint(
        bool sourceCompletedModule,
        IEnumerable<ClassSession> sessionsForModule,
        DateTime now)
    {
        if (!sourceCompletedModule)
        {
            return RebuyModuleCreditHint.Ahead;
        }

        var teachable = sessionsForModule.Where(IsTeachingSession).ToList();
        if (teachable.Count == 0 || teachable.All(cs => SessionAlreadyTaught(cs, now)))
        {
            return RebuyModuleCreditHint.Copied;
        }

        return RebuyModuleCreditHint.RedoWithClass;
    }

    private static bool HasPassedRequiredWork(IEnumerable<Submission> submissions, Assignment assignment)
        => submissions.Any(s =>
            s.Status == SubmissionStatus.Graded
            && s.AssignedGrade.HasValue
            && s.AssignedGrade.Value >= assignment.PassScore);

    private static bool HasWaitingGrade(IEnumerable<Submission> submissions)
        => submissions.Any(s => s.Status == SubmissionStatus.TurnedIn);
}
