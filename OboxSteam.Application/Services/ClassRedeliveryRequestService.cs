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

/// <summary>
/// Two-tier retake ladder (WS7g / WS7h).
/// Tier 1: the student picks among eligible Standard cohorts that have not reached the module yet.
/// Tier 2: when no cohort fits, the request waits for a manager who may open an intensive
/// Remedial class; the student then accepts or declines the compressed schedule.
/// </summary>
public sealed class ClassRedeliveryRequestService : IClassRedeliveryRequestService
{
    private const int DefaultRemedialCapacity = 20;
    private const int RemedialClassDurationMonths = 1;

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

        await EnsureRecoveryChainExhaustedAsync(enrollment, module);

        var sourceClassEnrollment = await ResolveSourceClassEnrollmentAsync(
            enrollment.StudentId,
            enrollment.ProgramEnrollmentId.Value);

        var existingOpen = await _unitOfWork.ClassRedeliveryRequests.FirstOrDefaultAsync(
            r => r.StudentId == enrollment.StudentId
                 && r.ModuleId == enrollment.ModuleId
                 && !r.IsDeleted
                 && (r.Status == ClassRedeliveryRequestStatus.PendingAutoMatch
                     || r.Status == ClassRedeliveryRequestStatus.MatchedPendingPayment
                     || r.Status == ClassRedeliveryRequestStatus.PendingManager
                     || r.Status == ClassRedeliveryRequestStatus.Approved
                     || r.Status == ClassRedeliveryRequestStatus.AwaitingClassSelection
                     || r.Status == ClassRedeliveryRequestStatus.AwaitingIntensiveConsent));

        if (existingOpen != null)
        {
            throw ErrorHelper.Conflict("An open class re-delivery request already exists for this module.");
        }

        var candidates = await ScanStandardCandidatesAsync(
            enrollment.StudentId,
            module,
            sourceClassEnrollment);

        var entity = new ClassRedeliveryRequest
        {
            Id = Guid.NewGuid(),
            StudentId = enrollment.StudentId,
            ModuleEnrollmentId = enrollment.Id,
            ModuleId = enrollment.ModuleId,
            SourceClassId = sourceClassEnrollment.ClassId,
            RequestedByUserId = actor.Id,
            Status = candidates.Count > 0
                ? ClassRedeliveryRequestStatus.AwaitingClassSelection
                : ClassRedeliveryRequestStatus.PendingManager,
            RequestMessage = string.IsNullOrWhiteSpace(request.RequestMessage)
                ? null
                : request.RequestMessage.Trim(),
        };

        await _unitOfWork.ClassRedeliveryRequests.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync();

        if (entity.Status == ClassRedeliveryRequestStatus.AwaitingClassSelection)
        {
            await _notificationPublisher.PublishAsync(
                NotificationCatalog.ClassRedeliveryAwaitingSelection(
                    entity.Id,
                    entity.StudentId,
                    module.Id,
                    candidates.Count,
                    module.Name,
                    module.ProgramId,
                    enrollment.ProgramEnrollmentId));
        }
        else
        {
            await _notificationPublisher.PublishAsync(
                NotificationCatalog.ClassRedeliveryPendingManager(
                    entity.Id,
                    enrollment.StudentId,
                    module.Id,
                    module.ProgramId,
                    module.Name));
        }

        _logger.LogInformation(
            "[CreateAsync] Class re-delivery {RequestId} for student {StudentId} module {ModuleId} status {Status} " +
            "({CandidateCount} candidate class(es)).",
            entity.Id,
            entity.StudentId,
            entity.ModuleId,
            entity.Status,
            candidates.Count);

        return Map(entity);
    }

    public async Task<List<ClassRedeliveryCandidateDto>> GetCandidatesAsync(Guid requestId)
    {
        var entity = await GetOrThrow(requestId);
        await EnsureCanViewRequestAsync(entity);

        if (entity.Status is not (
            ClassRedeliveryRequestStatus.AwaitingClassSelection
            or ClassRedeliveryRequestStatus.PendingManager))
        {
            throw ErrorHelper.BadRequest(
                "Candidate classes are only listed while the request awaits class selection or a manager decision.");
        }

        var enrollment = await _unitOfWork.ModuleEnrollments.GetByIdAsync(entity.ModuleEnrollmentId)
            ?? throw ErrorHelper.NotFound("Module enrollment not found.");
        var module = await _unitOfWork.Modules.GetByIdAsync(entity.ModuleId)
            ?? throw ErrorHelper.NotFound("Module not found.");

        if (!enrollment.ProgramEnrollmentId.HasValue)
        {
            throw ErrorHelper.BadRequest("Module enrollment must be linked to a program enrollment.");
        }

        var sourceClassEnrollment = await FindSourceClassEnrollmentAsync(entity);

        return await ScanStandardCandidatesAsync(entity.StudentId, module, sourceClassEnrollment);
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

        var target = await _unitOfWork.Classes.GetByIdAsync(classId)
            ?? throw ErrorHelper.NotFound($"Class '{classId}' not found.");

        var sourceClassEnrollment = await FindSourceClassEnrollmentAsync(entity);
        await ValidateStandardTargetAsync(entity, module, target, sourceClassEnrollment);

        entity.ResolutionType = RedeliveryResolutionType.StudentSelectedCohort;

        await PrepareMatchedPendingPaymentAsync(entity, enrollment, module, target);

        _logger.LogInformation(
            "[SelectClassAsync] Student {StudentId} selected class {ClassId} for re-delivery {RequestId}.",
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
            ClassRedeliveryRequestStatus.PendingAutoMatch
            or ClassRedeliveryRequestStatus.PendingManager
            or ClassRedeliveryRequestStatus.MatchedPendingPayment
            or ClassRedeliveryRequestStatus.AwaitingClassSelection
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

        var sourceClassEnrollment = await FindSourceClassEnrollmentAsync(entity);
        await ValidateStandardTargetAsync(entity, module, target, sourceClassEnrollment);

        entity.DecisionNote = string.IsNullOrWhiteSpace(dto.DecisionNote) ? null : dto.DecisionNote.Trim();
        entity.DecidedAt = DateTime.UtcNow;
        entity.DecidedBy = _claimsService.GetCurrentUserId;
        entity.ResolutionType = RedeliveryResolutionType.StudentSelectedCohort;

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
            or ClassRedeliveryRequestStatus.PendingAutoMatch
            or ClassRedeliveryRequestStatus.AwaitingClassSelection
            or ClassRedeliveryRequestStatus.AwaitingIntensiveConsent))
        {
            throw ErrorHelper.BadRequest("This request cannot be rejected in its current status.");
        }

        entity.Status = ClassRedeliveryRequestStatus.Rejected;
        entity.DecisionNote = string.IsNullOrWhiteSpace(dto?.DecisionNote) ? null : dto!.DecisionNote.Trim();
        entity.DecidedAt = DateTime.UtcNow;
        entity.DecidedBy = _claimsService.GetCurrentUserId;

        await _unitOfWork.ClassRedeliveryRequests.Update(entity);
        await _unitOfWork.SaveChangesAsync();

        var module = await _unitOfWork.Modules.GetByIdAsync(entity.ModuleId);
        var moduleEnrollment = await _unitOfWork.ModuleEnrollments.GetByIdAsync(entity.ModuleEnrollmentId);
        await _notificationPublisher.PublishAsync(
            NotificationCatalog.ClassRedeliveryRejected(
                entity.Id,
                entity.StudentId,
                entity.ModuleId,
                module?.ProgramId,
                moduleEnrollment?.ProgramEnrollmentId));

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

    public async Task<List<RedeliveryWaitlistProgramGroupDto>> GetWaitlistGroupedAsync()
    {
        await EnrollmentAccessValidator.GetCurrentManagerAsync(
            _unitOfWork,
            _claimsService,
            "Only managers can view the re-delivery waitlist.");

        var waiting = await _unitOfWork.ClassRedeliveryRequests.GetAllAsync(
            r => r.Status == ClassRedeliveryRequestStatus.PendingManager && !r.IsDeleted);

        if (waiting.Count == 0)
        {
            return [];
        }

        var moduleIds = waiting.Select(r => r.ModuleId).Distinct().ToList();
        var modules = await _unitOfWork.Modules.GetAllAsync(m => moduleIds.Contains(m.Id) && !m.IsDeleted);
        var modulesById = modules.ToDictionary(m => m.Id);

        var programIds = modules.Select(m => m.ProgramId).Distinct().ToList();
        var programs = await _unitOfWork.Programs.GetAllAsync(p => programIds.Contains(p.Id) && !p.IsDeleted);
        var programsById = programs.ToDictionary(p => p.Id);

        var now = DateTime.UtcNow;
        var groups = new List<RedeliveryWaitlistProgramGroupDto>();

        foreach (var programGroup in waiting
            .Where(r => modulesById.ContainsKey(r.ModuleId))
            .GroupBy(r => modulesById[r.ModuleId].ProgramId))
        {
            var program = programsById.GetValueOrDefault(programGroup.Key);

            var moduleGroups = programGroup
                .GroupBy(r => r.ModuleId)
                .Select(g =>
                {
                    var module = modulesById[g.Key];
                    var oldest = g.Min(r => r.CreatedAt);
                    return new RedeliveryWaitlistModuleGroupDto
                    {
                        ModuleId = module.Id,
                        ModuleCode = module.Code,
                        ModuleName = module.Name,
                        WaitingCount = g.Count(),
                        OldestWaitingDays = Math.Max(0, (int)(now - oldest).TotalDays),
                    };
                })
                .OrderByDescending(m => m.WaitingCount)
                .ThenByDescending(m => m.OldestWaitingDays)
                .ToList();

            groups.Add(new RedeliveryWaitlistProgramGroupDto
            {
                ProgramId = programGroup.Key,
                ProgramCode = program?.Code ?? string.Empty,
                ProgramName = program?.Name ?? string.Empty,
                Modules = moduleGroups,
            });
        }

        return groups.OrderBy(g => g.ProgramName).ToList();
    }

    public async Task<OpenRemedialClassResponseDto> OpenRemedialClassAsync(OpenRemedialClassRequestDto dto)
    {
        await EnrollmentAccessValidator.GetCurrentManagerAsync(
            _unitOfWork,
            _claimsService,
            "Only managers can open a remedial class.");

        if (dto.ModuleId == Guid.Empty)
        {
            throw ErrorHelper.BadRequest("ModuleId is required.");
        }

        if (dto.StartDate == default)
        {
            throw ErrorHelper.BadRequest("StartDate is required.");
        }

        if (dto.Capacity.HasValue && dto.Capacity.Value <= 0)
        {
            throw ErrorHelper.BadRequest("Capacity must be greater than zero.");
        }

        var module = await _unitOfWork.Modules.GetByIdAsync(dto.ModuleId)
            ?? throw ErrorHelper.NotFound($"Module '{dto.ModuleId}' not found.");

        if (module.IsDeleted)
        {
            throw ErrorHelper.NotFound($"Module '{dto.ModuleId}' not found.");
        }

        if (module.ModuleType == ModuleType.Theory)
        {
            throw ErrorHelper.BadRequest("Theory modules do not use remedial classes.");
        }

        User? mentor = null;
        if (dto.MentorId != Guid.Empty)
        {
            mentor = await _unitOfWork.Users.GetByIdAsync(dto.MentorId)
                ?? throw ErrorHelper.NotFound($"Mentor '{dto.MentorId}' not found.");

            if (mentor.IsDeleted || mentor.Role != RoleType.Mentor)
            {
                throw ErrorHelper.BadRequest("MentorId must reference an active mentor.");
            }
        }

        var remedialClass = new Class
        {
            Id = Guid.NewGuid(),
            Code = await GenerateRemedialClassCodeAsync(module.Code),
            Name = $"Remedial {module.Name}",
            ProgramId = module.ProgramId,
            MentorId = mentor?.Id,
            StartDate = dto.StartDate,
            EndDate = dto.StartDate.AddMonths(RemedialClassDurationMonths),
            MaxCapacity = dto.Capacity ?? DefaultRemedialCapacity,
            Kind = ClassKind.Remedial,
            RemedialModuleId = module.Id,
            Status = mentor != null ? ClassStatus.Open : ClassStatus.ReadyForMentor,
            ScheduleSummary = "Intensive remedial schedule",
        };

        await _unitOfWork.Classes.AddAsync(remedialClass);

        var waiting = await _unitOfWork.ClassRedeliveryRequests.GetAllAsync(
            r => r.ModuleId == module.Id
                 && r.Status == ClassRedeliveryRequestStatus.PendingManager
                 && !r.IsDeleted);

        foreach (var request in waiting)
        {
            request.Status = ClassRedeliveryRequestStatus.AwaitingIntensiveConsent;
            request.TargetClassId = remedialClass.Id;
            await _unitOfWork.ClassRedeliveryRequests.Update(request);
        }

        await _unitOfWork.SaveChangesAsync();

        foreach (var request in waiting)
        {
            var moduleEnrollment = await _unitOfWork.ModuleEnrollments.GetByIdAsync(request.ModuleEnrollmentId);
            await _notificationPublisher.PublishAsync(
                NotificationCatalog.ClassRedeliveryIntensiveOffered(
                    request.Id,
                    request.StudentId,
                    module.Id,
                    remedialClass.Id,
                    module.Name,
                    remedialClass.Name,
                    module.ProgramId,
                    moduleEnrollment?.ProgramEnrollmentId));
        }

        _logger.LogInformation(
            "[OpenRemedialClassAsync] Remedial class {ClassId} ({Code}) opened for module {ModuleId}; " +
            "{OfferCount} waitlisted request(s) offered.",
            remedialClass.Id,
            remedialClass.Code,
            module.Id,
            waiting.Count);

        return new OpenRemedialClassResponseDto
        {
            ClassId = remedialClass.Id,
            ClassCode = remedialClass.Code,
            ClassName = remedialClass.Name,
            OfferedRequestCount = waiting.Count,
        };
    }

    public async Task<ClassRedeliveryRequestResponseDto> AcceptIntensiveAsync(Guid requestId)
    {
        var entity = await GetOrThrow(requestId);

        if (entity.StudentId != _claimsService.GetCurrentUserId)
        {
            throw ErrorHelper.Forbidden("Only the student of this request can accept the remedial class.");
        }

        if (entity.Status != ClassRedeliveryRequestStatus.AwaitingIntensiveConsent)
        {
            throw ErrorHelper.BadRequest("Only requests awaiting intensive consent can be accepted.");
        }

        if (!entity.TargetClassId.HasValue)
        {
            throw ErrorHelper.BadRequest("This request has no remedial class offer.");
        }

        var enrollment = await _unitOfWork.ModuleEnrollments.GetByIdAsync(entity.ModuleEnrollmentId)
            ?? throw ErrorHelper.NotFound("Module enrollment not found.");
        var module = await _unitOfWork.Modules.GetByIdAsync(entity.ModuleId)
            ?? throw ErrorHelper.NotFound("Module not found.");

        var target = await _unitOfWork.Classes.GetByIdAsync(entity.TargetClassId.Value)
            ?? throw ErrorHelper.NotFound($"Class '{entity.TargetClassId}' not found.");

        if (target.IsDeleted || target.Kind != ClassKind.Remedial)
        {
            throw ErrorHelper.BadRequest("The offered class is no longer a remedial class.");
        }

        if (target.Status is ClassStatus.Cancelled or ClassStatus.Completed)
        {
            throw ErrorHelper.BadRequest("The offered remedial class is no longer joinable.");
        }

        await ClassEnrollmentValidator.ValidateClassHasCapacityAsync(_unitOfWork, target.Id, target.MaxCapacity);

        // Remedial seats run in parallel with the source class, so the whole schedule must fit.
        await ScheduleConflictValidator.ValidateStudentCanJoinClassAsync(_unitOfWork, entity.StudentId, target.Id);
        await StudentLoadValidator.ValidateUnderRetakeClassLoadAsync(_unitOfWork, entity.StudentId);

        entity.IntensivePaceAcceptedAt = DateTime.UtcNow;
        entity.ResolutionType = RedeliveryResolutionType.RemedialClass;

        await PrepareMatchedPendingPaymentAsync(entity, enrollment, module, target);

        _logger.LogInformation(
            "[AcceptIntensiveAsync] Student {StudentId} accepted remedial class {ClassId} for re-delivery {RequestId}.",
            entity.StudentId,
            target.Id,
            entity.Id);

        return Map(entity);
    }

    public async Task<ClassRedeliveryRequestResponseDto> DeclineIntensiveAsync(Guid requestId)
    {
        var entity = await GetOrThrow(requestId);

        if (entity.StudentId != _claimsService.GetCurrentUserId)
        {
            throw ErrorHelper.Forbidden("Only the student of this request can decline the remedial class.");
        }

        if (entity.Status != ClassRedeliveryRequestStatus.AwaitingIntensiveConsent)
        {
            throw ErrorHelper.BadRequest("Only requests awaiting intensive consent can be declined.");
        }

        entity.Status = ClassRedeliveryRequestStatus.Withdrawn;
        entity.TargetClassId = null;
        await _unitOfWork.ClassRedeliveryRequests.Update(entity);
        await _unitOfWork.SaveChangesAsync();

        await PublishWithdrawnAsync(entity);

        _logger.LogInformation(
            "[DeclineIntensiveAsync] Student {StudentId} declined the remedial offer on re-delivery {RequestId}.",
            entity.StudentId,
            entity.Id);

        return Map(entity);
    }

    public async Task NotifyPendingManagerForNewClassAsync(Guid classId)
    {
        var newClass = await _unitOfWork.Classes.GetByIdAsync(classId);
        if (newClass == null
            || newClass.IsDeleted
            || newClass.Kind != ClassKind.Standard
            || newClass.Status is not (ClassStatus.Open or ClassStatus.InProgress))
        {
            return;
        }

        var waiting = await _unitOfWork.ClassRedeliveryRequests.GetAllAsync(
            r => r.Status == ClassRedeliveryRequestStatus.PendingManager && !r.IsDeleted);

        if (waiting.Count == 0)
        {
            return;
        }

        var moduleIds = waiting.Select(r => r.ModuleId).Distinct().ToList();
        var modules = await _unitOfWork.Modules.GetAllAsync(
            m => moduleIds.Contains(m.Id) && m.ProgramId == newClass.ProgramId && !m.IsDeleted);

        if (modules.Count == 0)
        {
            return;
        }

        var modulesById = modules.ToDictionary(m => m.Id);
        var seatsTaken = await ClassEnrollmentValidator.GetSeatsTakenAsync(_unitOfWork, newClass.Id);
        if (seatsTaken >= newClass.MaxCapacity)
        {
            return;
        }

        var notified = 0;

        foreach (var request in waiting.Where(r => modulesById.ContainsKey(r.ModuleId)))
        {
            var module = modulesById[request.ModuleId];
            if (request.SourceClassId == newClass.Id)
            {
                continue;
            }

            var moduleSessions = await GetModuleSessionsAsync(newClass.Id, module.Id);
            if (HasStartedModule(moduleSessions))
            {
                continue;
            }

            var sourceClassEnrollment = await FindSourceClassEnrollmentAsync(request);
            if (!await IsUnderPrimaryClassLoadAsync(request.StudentId, sourceClassEnrollment?.Id))
            {
                continue;
            }

            var busySessions = await ScheduleConflictValidator.GetStudentBusySessionsAsync(
                _unitOfWork,
                request.StudentId,
                request.SourceClassId);

            if (ScheduleConflictValidator.FindFirstOverlap(busySessions, moduleSessions) != null)
            {
                continue;
            }

            var moduleEnrollment = await _unitOfWork.ModuleEnrollments.GetByIdAsync(request.ModuleEnrollmentId);
            await _notificationPublisher.PublishAsync(
                NotificationCatalog.ClassRedeliveryCandidatesAvailable(
                    request.Id,
                    request.StudentId,
                    module.Id,
                    newClass.Id,
                    module.Name,
                    newClass.Name,
                    module.ProgramId,
                    moduleEnrollment?.ProgramEnrollmentId));

            notified++;
        }

        if (notified > 0)
        {
            _logger.LogInformation(
                "[NotifyPendingManagerForNewClassAsync] Class {ClassId} matched {NotifiedCount} waitlisted request(s).",
                newClass.Id,
                notified);
        }
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
                "[CompleteAfterPaymentAsync] No program enrollment resolved for re-delivery {RequestId}.",
                entity.Id);
            return;
        }

        var isRemedial = targetClass.Kind == ClassKind.Remedial;

        // Standard cohorts replace the source seat; remedial classes run in parallel with it.
        if (!isRemedial && sourceEnrollment != null)
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
            Kind = isRemedial ? ClassEnrollmentKind.Retake : ClassEnrollmentKind.Primary,
            Status = ClassEnrollmentStatus.Active,
            EnrolledAt = DateTime.UtcNow,
        };
        await _unitOfWork.ClassEnrollments.AddAsync(newEnrollment);

        entity.Status = ClassRedeliveryRequestStatus.Completed;
        entity.PaymentId = payment.Id;
        entity.DecidedAt ??= DateTime.UtcNow;
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
            retakeModuleEnrollment.StartedAt ??= DateTime.UtcNow;
            await _unitOfWork.ModuleEnrollments.Update(retakeModuleEnrollment);
        }

        await _unitOfWork.SaveChangesAsync();

        if (isRemedial)
        {
            await _notificationPublisher.PublishAsync(
                NotificationCatalog.ClassEnrolled(
                    entity.StudentId,
                    targetClass.Id,
                    newEnrollment.Id,
                    targetClass.ProgramId,
                    targetClass.Name,
                    newEnrollment.ProgramEnrollmentId));
        }
        else
        {
            await _notificationPublisher.PublishAsync(
                NotificationCatalog.ClassTransferred(
                    entity.StudentId,
                    targetClass.Id,
                    newEnrollment.Id,
                    targetClass.ProgramId,
                    targetClass.Name,
                    newEnrollment.ProgramEnrollmentId));
        }

        await _notificationPublisher.PublishAsync(
            NotificationCatalog.ClassRedeliveryCompleted(
                entity.Id,
                entity.StudentId,
                entity.ModuleId,
                targetClass.Id,
                targetClass.ProgramId,
                newEnrollment.ProgramEnrollmentId));

        _logger.LogInformation(
            "[CompleteAfterPaymentAsync] Re-delivery {RequestId} completed as {ResolutionType}; " +
            "student {StudentId} → class {ClassId} ({Kind} seat).",
            entity.Id,
            entity.ResolutionType,
            entity.StudentId,
            targetClass.Id,
            newEnrollment.Kind);
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

    // ── Candidate scan ────────────────────────────────────────────────────────

    private async Task<List<ClassRedeliveryCandidateDto>> ScanStandardCandidatesAsync(
        Guid studentId,
        Module module,
        ClassEnrollment? sourceClassEnrollment)
    {
        if (!await IsUnderPrimaryClassLoadAsync(studentId, sourceClassEnrollment?.Id))
        {
            return [];
        }

        var sourceClassId = sourceClassEnrollment?.ClassId;
        var classes = await _unitOfWork.Classes.GetAllAsync(
            c => c.ProgramId == module.ProgramId
                 && c.Kind == ClassKind.Standard
                 && !c.IsDeleted
                 && (c.Status == ClassStatus.Open || c.Status == ClassStatus.InProgress));

        var busySessions = await ScheduleConflictValidator.GetStudentBusySessionsAsync(
            _unitOfWork,
            studentId,
            sourceClassId);

        var alreadyEnrolledClassIds = (await _unitOfWork.ClassEnrollments.GetAllAsync(
                ce => ce.StudentId == studentId
                      && ce.Status == ClassEnrollmentStatus.Active
                      && !ce.IsDeleted))
            .Select(ce => ce.ClassId)
            .ToHashSet();

        var candidates = new List<ClassRedeliveryCandidateDto>();

        foreach (var candidate in classes.OrderBy(c => c.StartDate))
        {
            if (alreadyEnrolledClassIds.Contains(candidate.Id))
            {
                continue;
            }

            var seatsTaken = await ClassEnrollmentValidator.GetSeatsTakenAsync(_unitOfWork, candidate.Id);
            if (seatsTaken >= candidate.MaxCapacity)
            {
                continue;
            }

            var moduleSessions = await GetModuleSessionsAsync(candidate.Id, module.Id);
            if (HasStartedModule(moduleSessions))
            {
                continue;
            }

            if (ScheduleConflictValidator.FindFirstOverlap(busySessions, moduleSessions) != null)
            {
                continue;
            }

            var mentor = candidate.MentorId.HasValue
                ? await _unitOfWork.Users.GetByIdAsync(candidate.MentorId.Value)
                : null;

            candidates.Add(new ClassRedeliveryCandidateDto
            {
                ClassId = candidate.Id,
                Code = candidate.Code,
                Name = candidate.Name,
                StartDate = candidate.StartDate,
                MentorId = candidate.MentorId,
                MentorName = mentor?.FullName,
                MaxCapacity = candidate.MaxCapacity,
                SeatsTaken = seatsTaken,
                SeatsRemaining = Math.Max(0, candidate.MaxCapacity - seatsTaken),
                ModuleSessions = moduleSessions
                    .OrderBy(cs => cs.StartTime)
                    .Select(cs => new ClassRedeliveryCandidateSessionDto
                    {
                        SessionId = cs.Id,
                        Title = cs.Title,
                        StartTime = cs.StartTime,
                        EndTime = cs.EndTime,
                        SessionKind = cs.SessionKind,
                    })
                    .ToList(),
            });
        }

        _logger.LogInformation(
            "[ScanStandardCandidatesAsync] Student {StudentId} module {ModuleId}: " +
            "{CandidateCount} of {ScannedCount} Standard class(es) eligible.",
            studentId,
            module.Id,
            candidates.Count,
            classes.Count);

        return candidates;
    }

    private async Task<List<ClassSession>> GetModuleSessionsAsync(Guid classId, Guid moduleId)
        => await _unitOfWork.ClassSessions.GetAllAsync(
            cs => cs.ClassId == classId
                  && cs.ModuleId == moduleId
                  && cs.Status != ClassSessionStatus.Cancelled
                  && !cs.IsDeleted);

    private static bool HasStartedModule(List<ClassSession> moduleSessions)
        => moduleSessions.Any(cs =>
            cs.Status is ClassSessionStatus.InProgress or ClassSessionStatus.Completed);

    /// <summary>
    /// Non-throwing counterpart of <see cref="StudentLoadValidator.ValidateUnderPrimaryClassLoadAsync"/>,
    /// used while scanning candidates (a full class is skipped, not an error).
    /// </summary>
    private async Task<bool> IsUnderPrimaryClassLoadAsync(Guid studentId, Guid? excludeEnrollmentId)
    {
        var active = await _unitOfWork.ClassEnrollments.GetAllAsync(
            ce => ce.StudentId == studentId
                  && !ce.IsDeleted
                  && ce.Status == ClassEnrollmentStatus.Active
                  && ce.Kind == ClassEnrollmentKind.Primary
                  && (!excludeEnrollmentId.HasValue || ce.Id != excludeEnrollmentId.Value));

        return active.Count < StudentLoadValidator.MaxPrimaryActiveClassesPerStudent;
    }

    // ── Target validation ─────────────────────────────────────────────────────

    private async Task ValidateStandardTargetAsync(
        ClassRedeliveryRequest entity,
        Module module,
        Class target,
        ClassEnrollment? sourceClassEnrollment)
    {
        if (target.IsDeleted || target.ProgramId != module.ProgramId)
        {
            throw ErrorHelper.BadRequest("Target class must belong to the same program.");
        }

        if (target.Kind != ClassKind.Standard)
        {
            throw ErrorHelper.BadRequest("Target class must be a Standard cohort.");
        }

        if (target.Id == entity.SourceClassId)
        {
            throw ErrorHelper.BadRequest("Target class must be different from the source class.");
        }

        if (target.Status is not (ClassStatus.Open or ClassStatus.InProgress))
        {
            throw ErrorHelper.BadRequest("Target class must be Open or InProgress.");
        }

        await ClassEnrollmentValidator.ValidateClassHasCapacityAsync(_unitOfWork, target.Id, target.MaxCapacity);

        var moduleSessions = await GetModuleSessionsAsync(target.Id, module.Id);
        if (HasStartedModule(moduleSessions))
        {
            throw ErrorHelper.BadRequest("Target class has already started this module.");
        }

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

        if (program.Price == null || program.Price <= 0)
        {
            throw ErrorHelper.BadRequest("This program does not have a valid price for re-delivery.");
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
            EnrolledAt = DateTime.UtcNow,
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
    /// so a parallel Retake seat never becomes the source class.
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

    private async Task<string> GenerateRemedialClassCodeAsync(string moduleCode)
    {
        var prefix = $"RMD-{moduleCode.Trim().ToUpperInvariant()}";

        for (var attempt = 0; attempt < 5; attempt++)
        {
            var code = $"{prefix}-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}";
            var duplicate = await _unitOfWork.Classes.FirstOrDefaultAsync(
                c => c.Code.ToLower() == code.ToLower() && !c.IsDeleted);

            if (duplicate == null)
            {
                return code;
            }
        }

        throw ErrorHelper.Conflict("Could not generate a unique remedial class code. Try again.");
    }

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
