using Microsoft.Extensions.Logging;
using OboxSteam.Application.DTOs.ClassRedeliveryDTO;
using OboxSteam.Application.Interfaces;
using OboxSteam.Application.Notifications;
using OboxSteam.Application.Utils;
using OboxSteam.Application.Validation;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;
using OboxSteam.Domain.Interfaces;

namespace OboxSteam.Application.Services;

public sealed class ClassRedeliveryRequestService : IClassRedeliveryRequestService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClaimsService _claimsService;
    private readonly INotificationPublisher _notificationPublisher;
    private readonly ILogger<ClassRedeliveryRequestService> _logger;

    public ClassRedeliveryRequestService(
        IUnitOfWork unitOfWork,
        IClaimsService claimsService,
        INotificationPublisher notificationPublisher,
        ILogger<ClassRedeliveryRequestService> logger)
    {
        _unitOfWork = unitOfWork;
        _claimsService = claimsService;
        _notificationPublisher = notificationPublisher;
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
                "Theory modules do not use class re-delivery. Redo the assignment freely or request a deadline extension.");
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

        var classEnrollment = await _unitOfWork.ClassEnrollments.FirstOrDefaultAsync(
            ce => ce.StudentId == enrollment.StudentId
                  && ce.ProgramEnrollmentId == enrollment.ProgramEnrollmentId.Value
                  && ce.Status == ClassEnrollmentStatus.Active
                  && !ce.IsDeleted)
            ?? throw ErrorHelper.BadRequest("Student has no active class enrollment for this program.");

        var existingOpen = await _unitOfWork.ClassRedeliveryRequests.FirstOrDefaultAsync(
            r => r.StudentId == enrollment.StudentId
                 && r.ModuleId == enrollment.ModuleId
                 && !r.IsDeleted
                 && (r.Status == ClassRedeliveryRequestStatus.PendingAutoMatch
                     || r.Status == ClassRedeliveryRequestStatus.MatchedPendingPayment
                     || r.Status == ClassRedeliveryRequestStatus.PendingManager
                     || r.Status == ClassRedeliveryRequestStatus.Approved));

        if (existingOpen != null)
        {
            throw ErrorHelper.Conflict("An open class re-delivery request already exists for this module.");
        }

        var entity = new ClassRedeliveryRequest
        {
            Id = Guid.NewGuid(),
            StudentId = enrollment.StudentId,
            ModuleEnrollmentId = enrollment.Id,
            ModuleId = enrollment.ModuleId,
            SourceClassId = classEnrollment.ClassId,
            RequestedByUserId = actor.Id,
            Status = ClassRedeliveryRequestStatus.PendingAutoMatch,
            RequestMessage = string.IsNullOrWhiteSpace(request.RequestMessage)
                ? null
                : request.RequestMessage.Trim(),
        };

        await _unitOfWork.ClassRedeliveryRequests.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync();

        var matched = await TryAutoMatchAsync(entity, module.ProgramId, enrollment.ModuleId, classEnrollment.ClassId);
        if (matched != null)
        {
            await PrepareMatchedPendingPaymentAsync(entity, enrollment, module, matched);
        }
        else
        {
            entity.Status = ClassRedeliveryRequestStatus.PendingManager;
            await _unitOfWork.ClassRedeliveryRequests.Update(entity);
            await _unitOfWork.SaveChangesAsync();

            await _notificationPublisher.PublishAsync(
                NotificationCatalog.ClassRedeliveryPendingManager(
                    entity.Id,
                    enrollment.StudentId,
                    module.Id,
                    module.ProgramId,
                    module.Name));
        }

        _logger.LogInformation(
            "[CreateAsync] Class re-delivery {RequestId} for student {StudentId} module {ModuleId} status {Status}.",
            entity.Id,
            entity.StudentId,
            entity.ModuleId,
            entity.Status);

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
            ClassRedeliveryRequestStatus.PendingAutoMatch
            or ClassRedeliveryRequestStatus.PendingManager
            or ClassRedeliveryRequestStatus.MatchedPendingPayment))
        {
            throw ErrorHelper.BadRequest("This request can no longer be withdrawn.");
        }

        entity.Status = ClassRedeliveryRequestStatus.Withdrawn;
        await _unitOfWork.ClassRedeliveryRequests.Update(entity);
        await _unitOfWork.SaveChangesAsync();
        return Map(entity);
    }

    public async Task<ClassRedeliveryRequestResponseDto> ManagerAssignTargetAsync(
        Guid requestId,
        DecideClassRedeliveryRequestDto dto)
    {
        await EnrollmentAccessValidator.GetCurrentManagerAsync(
            _unitOfWork,
            _claimsService,
            "Only managers can assign a re-delivery class.");

        if (!dto.TargetClassId.HasValue || dto.TargetClassId == Guid.Empty)
        {
            throw ErrorHelper.BadRequest("TargetClassId is required.");
        }

        var entity = await GetOrThrow(requestId);
        if (entity.Status != ClassRedeliveryRequestStatus.PendingManager)
        {
            throw ErrorHelper.BadRequest("Only PendingManager requests can be assigned a target class.");
        }

        var enrollment = await _unitOfWork.ModuleEnrollments.GetByIdAsync(entity.ModuleEnrollmentId)
            ?? throw ErrorHelper.NotFound("Module enrollment not found.");
        var module = await _unitOfWork.Modules.GetByIdAsync(entity.ModuleId)
            ?? throw ErrorHelper.NotFound("Module not found.");

        var target = await _unitOfWork.Classes.GetByIdAsync(dto.TargetClassId.Value)
            ?? throw ErrorHelper.NotFound($"Class '{dto.TargetClassId}' not found.");

        if (target.IsDeleted || target.ProgramId != module.ProgramId)
        {
            throw ErrorHelper.BadRequest("Target class must belong to the same program.");
        }

        if (target.Id == entity.SourceClassId)
        {
            throw ErrorHelper.BadRequest("Target class must be different from the source class.");
        }

        if (target.Status is not (ClassStatus.Open or ClassStatus.InProgress))
        {
            throw ErrorHelper.BadRequest("Target class must be Open or InProgress.");
        }

        entity.DecisionNote = string.IsNullOrWhiteSpace(dto.DecisionNote) ? null : dto.DecisionNote.Trim();
        entity.DecidedAt = DateTime.UtcNow;
        entity.DecidedBy = _claimsService.GetCurrentUserId;

        await PrepareMatchedPendingPaymentAsync(entity, enrollment, module, target);
        return Map(entity);
    }

    public async Task<ClassRedeliveryRequestResponseDto> RejectAsync(
        Guid requestId,
        DecideClassRedeliveryRequestDto? dto)
    {
        await EnrollmentAccessValidator.GetCurrentManagerAsync(
            _unitOfWork,
            _claimsService,
            "Only managers can reject re-delivery requests.");

        var entity = await GetOrThrow(requestId);
        if (entity.Status is not (
            ClassRedeliveryRequestStatus.PendingManager
            or ClassRedeliveryRequestStatus.MatchedPendingPayment
            or ClassRedeliveryRequestStatus.PendingAutoMatch))
        {
            throw ErrorHelper.BadRequest("This request cannot be rejected in its current status.");
        }

        entity.Status = ClassRedeliveryRequestStatus.Rejected;
        entity.DecisionNote = string.IsNullOrWhiteSpace(dto?.DecisionNote) ? null : dto!.DecisionNote.Trim();
        entity.DecidedAt = DateTime.UtcNow;
        entity.DecidedBy = _claimsService.GetCurrentUserId;

        await _unitOfWork.ClassRedeliveryRequests.Update(entity);
        await _unitOfWork.SaveChangesAsync();

        await _notificationPublisher.PublishAsync(
            NotificationCatalog.ClassRedeliveryRejected(entity.Id, entity.StudentId, entity.ModuleId));

        return Map(entity);
    }

    public async Task<List<ClassRedeliveryRequestResponseDto>> GetMineAsync()
    {
        var userId = _claimsService.GetCurrentUserId;
        var items = await _unitOfWork.ClassRedeliveryRequests.GetAllAsync(
            r => (r.StudentId == userId || r.RequestedByUserId == userId) && !r.IsDeleted);
        return items.OrderByDescending(r => r.CreatedAt).Select(Map).ToList();
    }

    public async Task<List<ClassRedeliveryRequestResponseDto>> GetPendingManagerAsync()
    {
        await EnrollmentAccessValidator.GetCurrentManagerAsync(
            _unitOfWork,
            _claimsService,
            "Only managers can view the re-delivery queue.");

        var items = await _unitOfWork.ClassRedeliveryRequests.GetAllAsync(
            r => r.Status == ClassRedeliveryRequestStatus.PendingManager && !r.IsDeleted);
        return items.OrderByDescending(r => r.CreatedAt).Select(Map).ToList();
    }

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

        var sourceEnrollment = await _unitOfWork.ClassEnrollments.FirstOrDefaultAsync(
            ce => ce.StudentId == entity.StudentId
                  && ce.ClassId == entity.SourceClassId
                  && ce.Status == ClassEnrollmentStatus.Active
                  && !ce.IsDeleted);

        if (sourceEnrollment == null)
        {
            _logger.LogWarning(
                "[CompleteAfterPaymentAsync] No active source class enrollment for re-delivery {RequestId}.",
                entity.Id);
            return;
        }

        var targetClass = await _unitOfWork.Classes.GetByIdAsync(entity.TargetClassId.Value);
        if (targetClass == null || targetClass.IsDeleted)
        {
            return;
        }

        sourceEnrollment.Status = ClassEnrollmentStatus.Transferred;
        await _unitOfWork.ClassEnrollments.Update(sourceEnrollment);

        var newEnrollment = new ClassEnrollment
        {
            Id = Guid.NewGuid(),
            StudentId = entity.StudentId,
            ClassId = entity.TargetClassId.Value,
            ProgramEnrollmentId = sourceEnrollment.ProgramEnrollmentId,
            Status = ClassEnrollmentStatus.Active,
            EnrolledAt = DateTime.UtcNow,
        };
        await _unitOfWork.ClassEnrollments.AddAsync(newEnrollment);

        entity.Status = ClassRedeliveryRequestStatus.Completed;
        entity.PaymentId = payment.Id;
        entity.DecidedAt ??= DateTime.UtcNow;
        await _unitOfWork.ClassRedeliveryRequests.Update(entity);

        // Keep prior module learning; mark original enrollment failed and activate retake enrollment.
        var originalModuleEnrollment = await _unitOfWork.ModuleEnrollments.GetByIdAsync(entity.ModuleEnrollmentId);
        if (originalModuleEnrollment != null
            && originalModuleEnrollment.Status == EnrollmentStatus.Active)
        {
            originalModuleEnrollment.Status = EnrollmentStatus.Failed;
            await _unitOfWork.ModuleEnrollments.Update(originalModuleEnrollment);
        }

        await _unitOfWork.SaveChangesAsync();

        await _notificationPublisher.PublishAsync(
            NotificationCatalog.ClassTransferred(
                entity.StudentId,
                entity.TargetClassId.Value,
                newEnrollment.Id,
                targetClass.ProgramId,
                targetClass.Name));

        await _notificationPublisher.PublishAsync(
            NotificationCatalog.ClassRedeliveryCompleted(
                entity.Id,
                entity.StudentId,
                entity.ModuleId,
                entity.TargetClassId.Value));

        _logger.LogInformation(
            "[CompleteAfterPaymentAsync] Re-delivery {RequestId} completed; student {StudentId} → class {ClassId}.",
            entity.Id,
            entity.StudentId,
            entity.TargetClassId);
    }

    private async Task PrepareMatchedPendingPaymentAsync(
        ClassRedeliveryRequest entity,
        ModuleEnrollment sourceEnrollment,
        Module module,
        Class targetClass)
    {
        if (module.RetakeFee <= 0)
        {
            throw ErrorHelper.BadRequest("This module does not have a retake fee configured for re-delivery.");
        }

        var nextAttempt = sourceEnrollment.AttemptNumber + 1;
        var retakeEnrollment = new ModuleEnrollment
        {
            Id = Guid.NewGuid(),
            StudentId = entity.StudentId,
            ModuleId = module.Id,
            ProgramEnrollmentId = sourceEnrollment.ProgramEnrollmentId,
            Status = EnrollmentStatus.PendingPayment,
            ProgressPercent = 0m,
            AttemptNumber = nextAttempt,
            AssignmentFailureCount = 0,
            EnrolledAt = DateTime.UtcNow,
        };

        await _unitOfWork.ModuleEnrollments.AddAsync(retakeEnrollment);

        entity.TargetClassId = targetClass.Id;
        entity.RetakeModuleEnrollmentId = retakeEnrollment.Id;
        entity.Status = ClassRedeliveryRequestStatus.MatchedPendingPayment;

        await _unitOfWork.ClassRedeliveryRequests.Update(entity);
        await _unitOfWork.SaveChangesAsync();

        // RetakeModuleEnrollmentId is set after SaveChanges assigns Ids when using identity —
        // Guid Ids are client-generated in this project via BaseEntity.
        retakeEnrollment = await _unitOfWork.ModuleEnrollments.GetByIdAsync(retakeEnrollment.Id)
            ?? retakeEnrollment;
        entity.RetakeModuleEnrollmentId = retakeEnrollment.Id;
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
                targetClass.Name));
    }

    private async Task<Class?> TryAutoMatchAsync(
        ClassRedeliveryRequest entity,
        Guid programId,
        Guid moduleId,
        Guid sourceClassId)
    {
        var candidates = await _unitOfWork.Classes.GetAllAsync(
            c => c.ProgramId == programId
                 && c.Id != sourceClassId
                 && !c.IsDeleted
                 && (c.Status == ClassStatus.Open || c.Status == ClassStatus.InProgress));

        Class? best = null;
        DateTime? bestSessionStart = null;

        foreach (var candidate in candidates.OrderBy(c => c.StartDate))
        {
            var activeCount = (await _unitOfWork.ClassEnrollments.GetAllAsync(
                ce => ce.ClassId == candidate.Id
                      && ce.Status == ClassEnrollmentStatus.Active
                      && !ce.IsDeleted)).Count;

            if (activeCount >= candidate.MaxCapacity)
            {
                continue;
            }

            var moduleSessions = await _unitOfWork.ClassSessions.GetAllAsync(
                cs => cs.ClassId == candidate.Id
                      && cs.ModuleId == moduleId
                      && !cs.IsDeleted);

            var reached = moduleSessions.Any(cs =>
                cs.Status is ClassSessionStatus.InProgress or ClassSessionStatus.Completed);
            if (reached)
            {
                continue;
            }

            var nextSession = moduleSessions
                .Where(cs => cs.Status == ClassSessionStatus.Scheduled)
                .OrderBy(cs => cs.StartTime)
                .FirstOrDefault();

            var sortKey = nextSession?.StartTime ?? candidate.StartDate;
            if (best == null || sortKey < bestSessionStart)
            {
                best = candidate;
                bestSessionStart = sortKey;
            }
        }

        return best;
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
            RequestMessage = entity.RequestMessage,
            DecisionNote = entity.DecisionNote,
            DecidedAt = entity.DecidedAt,
            DecidedBy = entity.DecidedBy,
            CreatedAt = entity.CreatedAt,
        };
}
