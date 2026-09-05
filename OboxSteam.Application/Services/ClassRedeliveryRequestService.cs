using Microsoft.Extensions.Logging;
using OboxSteam.Application.DTOs.ClassDTO;
using OboxSteam.Application.DTOs.ClassRedeliveryDTO;
using OboxSteam.Application.Interfaces;
using OboxSteam.Application.Notifications;
using OboxSteam.Application.Utils;
using OboxSteam.Application.Validation;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;
using OboxSteam.Domain.Interfaces;

namespace OboxSteam.Application.Services;

/// <summary>
/// Single-tier class continuity. Once the recovery chain is exhausted the request goes
/// straight to <see cref="ClassRedeliveryRequestStatus.AwaitingClassSelection"/>: the student
/// picks an eligible Standard class from the shared continuity catalog, pays the retake fee,
/// and their Primary seat transfers to that class. There is no manager waitlist and no
/// intensive Remedial tier; legacy PendingManager / AwaitingIntensiveConsent rows are only
/// honoured as open requests so they still block a duplicate create.
/// </summary>
public sealed class ClassRedeliveryRequestService : IClassRedeliveryRequestService
{
    private const string ManagerTierRemovedMessage =
        "Manager waitlist / remedial intensive redelivery is no longer available. "
        + "Students pick a Standard class via the continuity catalog.";

    private readonly IUnitOfWork _unitOfWork;
    private readonly IClaimsService _claimsService;
    private readonly INotificationPublisher _notificationPublisher;
    private readonly IRebuyClassCatalogService _rebuyClassCatalogService;
    private readonly ICurrentTime _currentTime;
    private readonly ILogger<ClassRedeliveryRequestService> _logger;

    public ClassRedeliveryRequestService(
        IUnitOfWork unitOfWork,
        IClaimsService claimsService,
        INotificationPublisher notificationPublisher,
        IRebuyClassCatalogService rebuyClassCatalogService,
        ICurrentTime currentTime,
        ILogger<ClassRedeliveryRequestService> logger)
    {
        _unitOfWork = unitOfWork;
        _claimsService = claimsService;
        _notificationPublisher = notificationPublisher;
        _rebuyClassCatalogService = rebuyClassCatalogService;
        _currentTime = currentTime;
        _logger = logger;
    }

    public async Task<ClassRedeliveryRequestResponseDto> CreateAsync(CreateClassRedeliveryRequestDto request)
    {
        if (request.ModuleEnrollmentId == Guid.Empty)
        {
            throw ErrorHelper.BadRequest("ModuleEnrollmentId is required.");
        }

        var actor = await _unitOfWork.Users.GetByIdAsync(_claimsService.GetCurrentUserId)
            ?? throw ErrorHelper.Unauthorized("User not found.");

        var enrollment = await _unitOfWork.ModuleEnrollments.GetByIdAsync(request.ModuleEnrollmentId)
            ?? throw ErrorHelper.NotFound($"Module enrollment '{request.ModuleEnrollmentId}' not found.");

        if (enrollment.IsDeleted)
        {
            throw ErrorHelper.NotFound($"Module enrollment '{request.ModuleEnrollmentId}' not found.");
        }

        var module = await _unitOfWork.Modules.GetByIdAsync(enrollment.ModuleId)
            ?? throw ErrorHelper.NotFound($"Module '{enrollment.ModuleId}' not found.");

        if (module.ModuleType == ModuleType.Theory)
        {
            throw ErrorHelper.BadRequest(
                "Theory modules do not use class re-delivery. Redo the assignment on the same class while the window is open.");
        }

        if (actor.Role == RoleType.Student)
        {
            if (enrollment.StudentId != actor.Id)
            {
                throw ErrorHelper.Forbidden("Module enrollment does not belong to you.");
            }
        }
        else if (actor.Role is not (RoleType.Mentor or RoleType.Manager or RoleType.Admin))
        {
            throw ErrorHelper.Forbidden("You cannot create a class re-delivery request.");
        }

        if (!enrollment.ProgramEnrollmentId.HasValue)
        {
            throw ErrorHelper.BadRequest("Module enrollment must be linked to a program enrollment.");
        }

        await EnsureRecoveryChainExhaustedAsync(enrollment, module);

        var sourceClassEnrollment = await ResolveSourceClassEnrollmentAsync(
            enrollment.StudentId,
            enrollment.ProgramEnrollmentId.Value);

        var existingOpen = await _unitOfWork.ClassRedeliveryRequests.FirstOrDefaultAsync(
            r => r.StudentId == enrollment.StudentId
                 && r.ModuleId == enrollment.ModuleId
                 && !r.IsDeleted
                 && (r.Status == ClassRedeliveryRequestStatus.MatchedPendingPayment
                     || r.Status == ClassRedeliveryRequestStatus.AwaitingClassSelection
                     || r.Status == ClassRedeliveryRequestStatus.PendingAutoMatch
                     || r.Status == ClassRedeliveryRequestStatus.PendingManager
                     || r.Status == ClassRedeliveryRequestStatus.AwaitingIntensiveConsent
                     || r.Status == ClassRedeliveryRequestStatus.Approved));

        if (existingOpen != null)
        {
            throw ErrorHelper.Conflict("An open class re-delivery request already exists for this module.");
        }

        var catalog = await BuildContinuityCatalogAsync(enrollment.StudentId, enrollment, module);
        var eligibleCount = catalog.Classes.Count(c => c.IsEligible);

        var entity = new ClassRedeliveryRequest
        {
            Id = Guid.NewGuid(),
            StudentId = enrollment.StudentId,
            ModuleEnrollmentId = enrollment.Id,
            ModuleId = enrollment.ModuleId,
            SourceClassId = sourceClassEnrollment.ClassId,
            RequestedByUserId = actor.Id,
            Status = ClassRedeliveryRequestStatus.AwaitingClassSelection,
            RequestMessage = string.IsNullOrWhiteSpace(request.RequestMessage)
                ? null
                : request.RequestMessage.Trim(),
        };

        await _unitOfWork.ClassRedeliveryRequests.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync();

        await _notificationPublisher.PublishAsync(
            NotificationCatalog.ClassRedeliveryAwaitingSelection(
                entity.Id,
                entity.StudentId,
                module.Id,
                eligibleCount,
                module.Name,
                module.ProgramId,
                enrollment.ProgramEnrollmentId));

        _logger.LogInformation(
            "[CreateAsync] Class continuity {RequestId} for student {StudentId} module {ModuleId} "
            + "awaiting class selection ({EligibleCount} of {TotalCount} catalog class(es) eligible).",
            entity.Id,
            entity.StudentId,
            entity.ModuleId,
            eligibleCount,
            catalog.Classes.Count);

        return Map(entity);
    }

    public async Task<RebuyClassCatalogDto> GetCandidatesAsync(Guid requestId)
    {
        var entity = await GetOrThrow(requestId);
        await EnsureCanViewRequestAsync(entity);

        if (entity.Status != ClassRedeliveryRequestStatus.AwaitingClassSelection)
        {
            throw ErrorHelper.BadRequest(
                "Continuity classes are only listed while the request awaits class selection.");
        }

        var enrollment = await _unitOfWork.ModuleEnrollments.GetByIdAsync(entity.ModuleEnrollmentId)
            ?? throw ErrorHelper.NotFound("Module enrollment not found.");
        var module = await _unitOfWork.Modules.GetByIdAsync(entity.ModuleId)
            ?? throw ErrorHelper.NotFound("Module not found.");

        return await BuildContinuityCatalogAsync(entity.StudentId, enrollment, module);
    }

    public async Task<ClassRedeliveryRequestResponseDto> SelectClassAsync(Guid requestId, Guid classId)
    {
        if (classId == Guid.Empty)
        {
            throw ErrorHelper.BadRequest("ClassId is required.");
        }

        var entity = await GetOrThrow(requestId);

        if (entity.StudentId != _claimsService.GetCurrentUserId)
        {
            throw ErrorHelper.Forbidden("Only the student of this request can select a class.");
        }

        if (entity.Status != ClassRedeliveryRequestStatus.AwaitingClassSelection)
        {
            throw ErrorHelper.BadRequest("Only requests awaiting class selection can pick a class.");
        }

        var enrollment = await _unitOfWork.ModuleEnrollments.GetByIdAsync(entity.ModuleEnrollmentId)
            ?? throw ErrorHelper.NotFound("Module enrollment not found.");
        var module = await _unitOfWork.Modules.GetByIdAsync(entity.ModuleId)
            ?? throw ErrorHelper.NotFound("Module not found.");

        var catalog = await BuildContinuityCatalogAsync(entity.StudentId, enrollment, module);
        var selected = catalog.Classes.FirstOrDefault(c => c.ClassId == classId)
            ?? throw ErrorHelper.BadRequest(
                "This class is not offered for continuity on this module. Reload the class list and pick again.");

        if (!selected.IsEligible)
        {
            throw ErrorHelper.BadRequest(
                selected.IneligibleReason ?? "This class is not eligible for continuity on this module.");
        }

        var target = await _unitOfWork.Classes.GetByIdAsync(classId)
            ?? throw ErrorHelper.NotFound($"Class '{classId}' not found.");

        var sourceClassEnrollment = await FindSourceClassEnrollmentAsync(entity);
        await ValidateSelectedTargetAsync(entity, module, target, sourceClassEnrollment);

        entity.ResolutionType = RedeliveryResolutionType.StudentSelectedCohort;

        await PrepareMatchedPendingPaymentAsync(entity, enrollment, module, target);

        _logger.LogInformation(
            "[SelectClassAsync] Student {StudentId} selected class {ClassId} for continuity {RequestId}.",
            entity.StudentId,
            target.Id,
            entity.Id);

        return Map(entity);
    }

    public async Task<ClassRedeliveryRequestResponseDto> WithdrawAsync(Guid requestId)
    {
        var userId = _claimsService.GetCurrentUserId;
        var entity = await GetOrThrow(requestId);

        if (entity.StudentId != userId && entity.RequestedByUserId != userId)
        {
            throw ErrorHelper.Forbidden("You cannot withdraw this request.");
        }

        if (entity.Status is not (
            ClassRedeliveryRequestStatus.AwaitingClassSelection
            or ClassRedeliveryRequestStatus.MatchedPendingPayment
            or ClassRedeliveryRequestStatus.PendingAutoMatch
            or ClassRedeliveryRequestStatus.PendingManager
            or ClassRedeliveryRequestStatus.AwaitingIntensiveConsent))
        {
            throw ErrorHelper.BadRequest("This request can no longer be withdrawn.");
        }

        entity.Status = ClassRedeliveryRequestStatus.Withdrawn;
        await _unitOfWork.ClassRedeliveryRequests.Update(entity);
        await _unitOfWork.SaveChangesAsync();

        await PublishWithdrawnAsync(entity);

        return Map(entity);
    }

    public Task<ClassRedeliveryRequestResponseDto> ManagerAssignTargetAsync(
        Guid requestId,
        DecideClassRedeliveryRequestDto dto)
        => throw ErrorHelper.Gone(ManagerTierRemovedMessage);

    public Task<ClassRedeliveryRequestResponseDto> RejectAsync(
        Guid requestId,
        DecideClassRedeliveryRequestDto? dto)
        => throw ErrorHelper.Gone(ManagerTierRemovedMessage);

    public async Task<List<ClassRedeliveryRequestResponseDto>> GetMineAsync()
    {
        var userId = _claimsService.GetCurrentUserId;
        var items = await _unitOfWork.ClassRedeliveryRequests.GetAllAsync(
            r => (r.StudentId == userId || r.RequestedByUserId == userId) && !r.IsDeleted);
        return items.OrderByDescending(r => r.CreatedAt).Select(Map).ToList();
    }

    public Task<List<ClassRedeliveryRequestResponseDto>> GetPendingManagerAsync()
        => throw ErrorHelper.Gone(ManagerTierRemovedMessage);

    public Task<List<RedeliveryWaitlistProgramGroupDto>> GetWaitlistGroupedAsync()
        => throw ErrorHelper.Gone(ManagerTierRemovedMessage);

    public Task<OpenRemedialClassResponseDto> OpenRemedialClassAsync(OpenRemedialClassRequestDto dto)
        => throw ErrorHelper.Gone(ManagerTierRemovedMessage);

    public Task<ClassRedeliveryRequestResponseDto> AcceptIntensiveAsync(Guid requestId)
        => throw ErrorHelper.Gone(ManagerTierRemovedMessage);

    public Task<ClassRedeliveryRequestResponseDto> DeclineIntensiveAsync(Guid requestId)
        => throw ErrorHelper.Gone(ManagerTierRemovedMessage);

    /// <summary>
    /// No-op since continuity dropped the waitlist: students read the live catalog on demand,
    /// so a newly opened class needs no fan-out notification.
    /// </summary>
    public Task NotifyPendingManagerForNewClassAsync(Guid classId) => Task.CompletedTask;

    public async Task CompleteAfterPaymentAsync(Guid paymentId)
    {
        var payment = await _unitOfWork.Payments.GetByIdAsync(paymentId);
        if (payment == null || !payment.ModuleEnrollmentId.HasValue)
        {
            return;
        }

        var entity = await _unitOfWork.ClassRedeliveryRequests.FirstOrDefaultAsync(
            r => r.RetakeModuleEnrollmentId == payment.ModuleEnrollmentId
                 && !r.IsDeleted
                 && (r.Status == ClassRedeliveryRequestStatus.MatchedPendingPayment
                     || r.Status == ClassRedeliveryRequestStatus.Approved));

        if (entity == null || !entity.TargetClassId.HasValue)
        {
            return;
        }

        var targetClass = await _unitOfWork.Classes.GetByIdAsync(entity.TargetClassId.Value);
        if (targetClass == null || targetClass.IsDeleted)
        {
            return;
        }

        var retakeModuleEnrollment = await _unitOfWork.ModuleEnrollments.GetByIdAsync(
            entity.RetakeModuleEnrollmentId!.Value);

        var sourceEnrollment = await _unitOfWork.ClassEnrollments.FirstOrDefaultAsync(
            ce => ce.StudentId == entity.StudentId
                  && ce.ClassId == entity.SourceClassId
                  && ce.Status == ClassEnrollmentStatus.Active
                  && !ce.IsDeleted);

        var programEnrollmentId = sourceEnrollment?.ProgramEnrollmentId
            ?? retakeModuleEnrollment?.ProgramEnrollmentId;

        if (!programEnrollmentId.HasValue)
        {
            _logger.LogWarning(
                "[CompleteAfterPaymentAsync] No program enrollment resolved for continuity {RequestId}.",
                entity.Id);
            return;
        }

        var now = _currentTime.GetCurrentTime();

        // Continuity always moves the single Primary seat; no parallel retake seat exists.
        if (sourceEnrollment != null)
        {
            sourceEnrollment.Status = ClassEnrollmentStatus.Transferred;
            await _unitOfWork.ClassEnrollments.Update(sourceEnrollment);
        }

        var newEnrollment = new ClassEnrollment
        {
            Id = Guid.NewGuid(),
            StudentId = entity.StudentId,
            ClassId = targetClass.Id,
            ProgramEnrollmentId = programEnrollmentId.Value,
            Kind = ClassEnrollmentKind.Primary,
            Status = ClassEnrollmentStatus.Active,
            EnrolledAt = now,
        };
        await _unitOfWork.ClassEnrollments.AddAsync(newEnrollment);

        entity.Status = ClassRedeliveryRequestStatus.Completed;
        entity.PaymentId = payment.Id;
        entity.DecidedAt ??= now;
        await _unitOfWork.ClassRedeliveryRequests.Update(entity);

        // Voluntary retake keeps the Completed record; a failed attempt is closed as Failed.
        var originalModuleEnrollment = await _unitOfWork.ModuleEnrollments.GetByIdAsync(entity.ModuleEnrollmentId);
        if (originalModuleEnrollment != null && originalModuleEnrollment.Status == EnrollmentStatus.Active)
        {
            originalModuleEnrollment.Status = EnrollmentStatus.Failed;
            await _unitOfWork.ModuleEnrollments.Update(originalModuleEnrollment);
        }

        if (retakeModuleEnrollment != null && retakeModuleEnrollment.Status != EnrollmentStatus.Active)
        {
            retakeModuleEnrollment.Status = EnrollmentStatus.Active;
            retakeModuleEnrollment.StartedAt ??= now;
            await _unitOfWork.ModuleEnrollments.Update(retakeModuleEnrollment);
        }

        await _unitOfWork.SaveChangesAsync();

        await _notificationPublisher.PublishAsync(
            NotificationCatalog.ClassTransferred(
                entity.StudentId,
                targetClass.Id,
                newEnrollment.Id,
                targetClass.ProgramId,
                targetClass.Name,
                newEnrollment.ProgramEnrollmentId));

        await _notificationPublisher.PublishAsync(
            NotificationCatalog.ClassRedeliveryCompleted(
                entity.Id,
                entity.StudentId,
                entity.ModuleId,
                targetClass.Id,
                targetClass.ProgramId,
                newEnrollment.ProgramEnrollmentId));

        _logger.LogInformation(
            "[CompleteAfterPaymentAsync] Continuity {RequestId} completed as {ResolutionType}; "
            + "student {StudentId} transferred to class {ClassId}.",
            entity.Id,
            entity.ResolutionType,
            entity.StudentId,
            targetClass.Id);
    }

    // ── Eligibility gate ──────────────────────────────────────────────────────

    /// <summary>
    /// Redelivery sits at the end of the recovery chain: a Failed attempt, a voluntary
    /// retake of a Completed attempt, or an Active attempt that is stuck with no pending
    /// recovery left to wait for.
    /// </summary>
    private async Task EnsureRecoveryChainExhaustedAsync(ModuleEnrollment enrollment, Module module)
    {
        if (enrollment.Status is EnrollmentStatus.Failed or EnrollmentStatus.Completed)
        {
            return;
        }

        if (enrollment.Status != EnrollmentStatus.Active)
        {
            throw ErrorHelper.BadRequest(
                $"Module enrollment status '{enrollment.Status}' is not eligible for class re-delivery.");
        }

        var pendingRecovery = await _unitOfWork.AssessmentRecoveryRequests.FirstOrDefaultAsync(
            r => r.ModuleEnrollmentId == enrollment.Id
                 && r.Status == AssessmentRecoveryRequestStatus.Pending
                 && !r.IsDeleted);

        if (pendingRecovery != null)
        {
            throw ErrorHelper.BadRequest(
                "A recovery request is still pending for this module. Wait for the mentor decision first.");
        }

        if (await HasFailedSubmissionAsync(enrollment))
        {
            return;
        }

        if (await HasExhaustedRecoveryCapAsync(enrollment, module))
        {
            return;
        }

        throw ErrorHelper.BadRequest(
            "You still have assignment attempts or recovery requests available for this module. " +
            "Use those before requesting class re-delivery.");
    }

    private async Task<bool> HasFailedSubmissionAsync(ModuleEnrollment enrollment)
    {
        var graded = await _unitOfWork.Submissions.GetAllAsync(
            s => s.ModuleEnrollmentId == enrollment.Id
                 && s.Status == SubmissionStatus.Graded
                 && s.AssignedGrade != null
                 && !s.IsDeleted);

        if (graded.Count == 0)
        {
            return false;
        }

        var assignmentIds = graded.Select(s => s.AssignmentId).Distinct().ToList();
        var assignments = await _unitOfWork.Assignments.GetAllAsync(
            a => assignmentIds.Contains(a.Id) && !a.IsDeleted);
        var passScores = assignments.ToDictionary(a => a.Id, a => a.PassScore);

        return graded.Any(s =>
            passScores.TryGetValue(s.AssignmentId, out var passScore)
            && s.AssignedGrade!.Value < passScore);
    }

    private async Task<bool> HasExhaustedRecoveryCapAsync(ModuleEnrollment enrollment, Module module)
    {
        var moduleAssignments = await _unitOfWork.Assignments.GetAllAsync(
            a => a.ModuleId == module.Id && !a.IsDeleted);

        if (moduleAssignments.Count == 0)
        {
            return false;
        }

        var assignmentIds = moduleAssignments.Select(a => a.Id).ToList();
        var decided = await _unitOfWork.AssessmentRecoveryRequests.GetAllAsync(
            r => r.ModuleEnrollmentId == enrollment.Id
                 && assignmentIds.Contains(r.AssignmentId)
                 && (r.Status == AssessmentRecoveryRequestStatus.Approved
                     || r.Status == AssessmentRecoveryRequestStatus.Rejected)
                 && !r.IsDeleted);

        return decided
            .GroupBy(r => r.AssignmentId)
            .Any(g => g.Count() >= AssessmentAttemptPolicy.MaxRecoveryRequestsPerAssignment);
    }

    // ── Continuity catalog ────────────────────────────────────────────────────

    private async Task<RebuyClassCatalogDto> BuildContinuityCatalogAsync(
        Guid studentId,
        ModuleEnrollment enrollment,
        Module module)
    {
        if (!enrollment.ProgramEnrollmentId.HasValue)
        {
            throw ErrorHelper.BadRequest("Module enrollment must be linked to a program enrollment.");
        }

        var program = await _unitOfWork.Programs.GetByIdAsync(module.ProgramId)
            ?? throw ErrorHelper.NotFound($"Program '{module.ProgramId}' not found.");

        var programEnrollment = await _unitOfWork.ProgramEnrollments.GetByIdAsync(
            enrollment.ProgramEnrollmentId.Value)
            ?? throw ErrorHelper.NotFound("Program enrollment not found.");

        return await _rebuyClassCatalogService.BuildActiveCatalogAsync(
            studentId,
            program,
            programEnrollment,
            module);
    }

    // ── Target validation ─────────────────────────────────────────────────────

    /// <summary>
    /// Guards the catalog cannot own: seats may fill between listing and selection, and the
    /// student's own schedule and Primary class load sit outside class-level eligibility.
    /// </summary>
    private async Task ValidateSelectedTargetAsync(
        ClassRedeliveryRequest entity,
        Module module,
        Class target,
        ClassEnrollment? sourceClassEnrollment)
    {
        if (target.Id == entity.SourceClassId)
        {
            throw ErrorHelper.BadRequest("Target class must be different from the source class.");
        }

        await ClassEnrollmentValidator.ValidateClassHasCapacityAsync(_unitOfWork, target.Id, target.MaxCapacity);

        await ScheduleConflictValidator.ValidateStudentCanJoinModuleOnClassAsync(
            _unitOfWork,
            entity.StudentId,
            target.Id,
            module.Id,
            entity.SourceClassId);

        await StudentLoadValidator.ValidateUnderPrimaryClassLoadAsync(
            _unitOfWork,
            entity.StudentId,
            sourceClassEnrollment?.Id);
    }

    // ── Shared helpers ────────────────────────────────────────────────────────

    private async Task PrepareMatchedPendingPaymentAsync(
        ClassRedeliveryRequest entity,
        ModuleEnrollment sourceEnrollment,
        Module module,
        Class targetClass)
    {
        var program = await _unitOfWork.Programs.GetByIdAsync(module.ProgramId)
            ?? throw ErrorHelper.NotFound($"Program '{module.ProgramId}' not found.");

        var amount = ClassContinuityCatalogBuilder.ResolveActiveContinuityAmount(program);
        if (amount <= 0)
        {
            throw ErrorHelper.BadRequest("This program does not have a valid price for continuity.");
        }

        var retakeEnrollment = new ModuleEnrollment
        {
            Id = Guid.NewGuid(),
            StudentId = entity.StudentId,
            ModuleId = module.Id,
            ProgramEnrollmentId = sourceEnrollment.ProgramEnrollmentId,
            Status = EnrollmentStatus.PendingPayment,
            ProgressPercent = 0m,
            AttemptNumber = sourceEnrollment.AttemptNumber + 1,
            EnrolledAt = _currentTime.GetCurrentTime(),
        };

        await _unitOfWork.ModuleEnrollments.AddAsync(retakeEnrollment);

        entity.TargetClassId = targetClass.Id;
        entity.RetakeModuleEnrollmentId = retakeEnrollment.Id;
        entity.Status = ClassRedeliveryRequestStatus.MatchedPendingPayment;

        await _unitOfWork.ClassRedeliveryRequests.Update(entity);
        await _unitOfWork.SaveChangesAsync();

        await _notificationPublisher.PublishAsync(
            NotificationCatalog.ClassRedeliveryMatchedPendingPayment(
                entity.Id,
                entity.StudentId,
                module.Id,
                targetClass.Id,
                retakeEnrollment.Id,
                module.Name,
                targetClass.Name,
                module.ProgramId,
                sourceEnrollment.ProgramEnrollmentId));
    }

    private async Task PublishWithdrawnAsync(ClassRedeliveryRequest entity)
    {
        var module = await _unitOfWork.Modules.GetByIdAsync(entity.ModuleId);
        var moduleEnrollment = await _unitOfWork.ModuleEnrollments.GetByIdAsync(entity.ModuleEnrollmentId);

        await _notificationPublisher.PublishAsync(
            NotificationCatalog.ClassRedeliveryWithdrawn(
                entity.Id,
                entity.StudentId,
                entity.ModuleId,
                module?.Name,
                module?.ProgramId,
                moduleEnrollment?.ProgramEnrollmentId));
    }

    /// <summary>
    /// Resolves the class seat the request originates from, preferring the Primary seat
    /// so a legacy parallel Retake seat never becomes the source class.
    /// </summary>
    private async Task<ClassEnrollment> ResolveSourceClassEnrollmentAsync(
        Guid studentId,
        Guid programEnrollmentId)
    {
        var active = await _unitOfWork.ClassEnrollments.GetAllAsync(
            ce => ce.StudentId == studentId
                  && ce.ProgramEnrollmentId == programEnrollmentId
                  && ce.Status == ClassEnrollmentStatus.Active
                  && !ce.IsDeleted);

        var source = active.FirstOrDefault(ce => ce.Kind == ClassEnrollmentKind.Primary)
            ?? active.FirstOrDefault();

        return source
            ?? throw ErrorHelper.BadRequest("Student has no active class enrollment for this program.");
    }

    private async Task<ClassEnrollment?> FindSourceClassEnrollmentAsync(ClassRedeliveryRequest entity)
        => await _unitOfWork.ClassEnrollments.FirstOrDefaultAsync(
            ce => ce.StudentId == entity.StudentId
                  && ce.ClassId == entity.SourceClassId
                  && ce.Status == ClassEnrollmentStatus.Active
                  && !ce.IsDeleted);

    private async Task EnsureCanViewRequestAsync(ClassRedeliveryRequest entity)
    {
        var userId = _claimsService.GetCurrentUserId;
        if (userId == entity.StudentId || userId == entity.RequestedByUserId)
        {
            return;
        }

        var user = await _unitOfWork.Users.GetByIdAsync(userId);
        if (user == null || user.IsDeleted || user.Role is not (RoleType.Manager or RoleType.Admin))
        {
            throw ErrorHelper.Forbidden("You cannot view this re-delivery request.");
        }
    }

    private async Task<ClassRedeliveryRequest> GetOrThrow(Guid requestId)
    {
        var entity = await _unitOfWork.ClassRedeliveryRequests.GetByIdAsync(requestId);
        if (entity == null || entity.IsDeleted)
        {
            throw ErrorHelper.NotFound($"Class re-delivery request '{requestId}' not found.");
        }

        return entity;
    }

    private static ClassRedeliveryRequestResponseDto Map(ClassRedeliveryRequest entity)
        => new()
        {
            Id = entity.Id,
            StudentId = entity.StudentId,
            ModuleEnrollmentId = entity.ModuleEnrollmentId,
            ModuleId = entity.ModuleId,
            SourceClassId = entity.SourceClassId,
            RequestedByUserId = entity.RequestedByUserId,
            Status = entity.Status,
            TargetClassId = entity.TargetClassId,
            PaymentId = entity.PaymentId,
            RetakeModuleEnrollmentId = entity.RetakeModuleEnrollmentId,
            IntensivePaceAcceptedAt = entity.IntensivePaceAcceptedAt,
            ResolutionType = entity.ResolutionType,
            RequestMessage = entity.RequestMessage,
            DecisionNote = entity.DecisionNote,
            DecidedAt = entity.DecidedAt,
            DecidedBy = entity.DecidedBy,
            CreatedAt = entity.CreatedAt,
        };
}
