using Microsoft.Extensions.Logging;
using OboxSteam.Application.DTOs.ClassDTO;
using OboxSteam.Application.Interfaces;
using OboxSteam.Application.Utils;
using OboxSteam.Application.Validation;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;
using OboxSteam.Domain.Interfaces;

namespace OboxSteam.Application.Services;

/// <summary>
/// Student class picker for checkout: Open-only for first purchase and Completed retakes;
/// Open or InProgress with stop-module eligibility after Failed/Dropped.
/// </summary>
public sealed class RebuyClassCatalogService : IRebuyClassCatalogService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClaimsService _claimsService;
    private readonly ProgramPurchaseLifecycle _programPurchaseLifecycle;
    private readonly ICurrentTime _currentTime;
    private readonly ILogger<RebuyClassCatalogService> _logger;

    public RebuyClassCatalogService(
        IUnitOfWork unitOfWork,
        IClaimsService claimsService,
        ProgramPurchaseLifecycle programPurchaseLifecycle,
        ICurrentTime currentTime,
        ILogger<RebuyClassCatalogService> logger)
    {
        _unitOfWork = unitOfWork;
        _claimsService = claimsService;
        _programPurchaseLifecycle = programPurchaseLifecycle;
        _currentTime = currentTime;
        _logger = logger;
    }

    public async Task<RebuyClassCatalogDto> GetRebuyClassesAsync(Guid programId)
    {
        ProgramEnrollmentValidator.ValidateProgramIdRequired(programId);

        var student = await EnrollmentAccessValidator.GetCurrentStudentForEnrollAsync(
            _unitOfWork,
            _claimsService,
            ProgramEnrollmentValidator.EnrollForbiddenMessage);

        var program = await _unitOfWork.Programs.GetByIdAsync(programId);
        ProgramEnrollmentValidator.ValidateProgramExists(program, programId);
        ProgramEnrollmentValidator.EnsureProgramPurchasable(program!);

        var enrollments = await _unitOfWork.ProgramEnrollments.GetAllAsync(
            pe => pe.StudentId == student.Id && pe.ProgramId == programId && !pe.IsDeleted);

        if (enrollments.Any(pe => pe.Status is EnrollmentStatus.Active or EnrollmentStatus.Deferred))
        {
            throw ErrorHelper.Conflict("Student is already enrolled in this program.");
        }

        var source = ProgramPurchaseLifecycle.FindRebuySource(enrollments);
        var isRebuy = ProgramPurchaseLifecycle.AllowsInProgressClassJoin(source);

        await ClassSeatHoldHelper.ReleaseExpiredHoldsAsync(_unitOfWork);

        Module? stopModule = null;
        if (isRebuy)
        {
            var stopModuleId = await _programPurchaseLifecycle.ResolveStopModuleIdAsync(source!);
            if (stopModuleId.HasValue)
            {
                stopModule = await _unitOfWork.Modules.GetByIdAsync(stopModuleId.Value);
                if (stopModule != null && stopModule.IsDeleted)
                {
                    stopModule = null;
                }
            }
        }

        var now = _currentTime.GetCurrentTime();
        var withinWindow = source != null && ProgramPurchaseLifecycle.IsWithinRebuyWindow(source, now);
        var checkoutAmount = ProgramPurchaseLifecycle.ResolveCheckoutAmount(program!, source, now);

        var sourceClassIds = source == null
            ? []
            : await _programPurchaseLifecycle.GetSourceOccupiedClassIdsAsync(source.Id);

        var modules = (await _unitOfWork.Modules.GetAllAsync(
                m => m.ProgramId == programId && !m.IsDeleted))
            .OrderBy(m => m.ModuleOrder)
            .ToList();

        var openOrRunningClasses = await _unitOfWork.Classes.GetAllAsync(
            c => c.ProgramId == programId
                 && c.Kind == ClassKind.Standard
                 && !c.IsDeleted
                 && (c.Status == ClassStatus.Open
                     || (isRebuy && c.Status == ClassStatus.InProgress)));

        var classIds = openOrRunningClasses.Select(c => c.Id).ToList();
        var sessions = classIds.Count == 0
            ? []
            : await _unitOfWork.ClassSessions.GetAllAsync(
                cs => classIds.Contains(cs.ClassId) && !cs.IsDeleted);
        var sessionsByClassId = sessions
            .GroupBy(cs => cs.ClassId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var mentorIds = openOrRunningClasses
            .Where(c => c.MentorId.HasValue)
            .Select(c => c.MentorId!.Value)
            .Distinct()
            .ToList();
        var mentors = mentorIds.Count == 0
            ? []
            : await _unitOfWork.Users.GetAllAsync(u => mentorIds.Contains(u.Id) && !u.IsDeleted);
        var mentorById = mentors.ToDictionary(u => u.Id);

        var classes = new List<RebuyClassDto>();
        foreach (var openClass in openOrRunningClasses.OrderBy(c => c.StartDate).ThenBy(c => c.Code))
        {
            var seatsTaken = await ClassEnrollmentValidator.GetSeatsTakenAsync(_unitOfWork, openClass.Id);
            var seatsRemaining = openClass.MaxCapacity - seatsTaken;
            if (seatsRemaining <= 0)
            {
                continue;
            }

            sessionsByClassId.TryGetValue(openClass.Id, out var classSessions);
            classSessions ??= [];

            var moduleProgress = modules
                .Select(module => MapModuleProgress(module, classSessions, stopModule))
                .ToList();

            var isSourceClass = sourceClassIds.Contains(openClass.Id);
            var blocksStopModule = isRebuy
                && stopModule != null
                && ProgramPurchaseLifecycle.ClassBlocksRebuy(
                    modules,
                    classSessions,
                    stopModule.ModuleOrder);
            var lateJoinReason = ClassEnrollmentValidator.GetLateJoinBlockReason(
                openClass,
                classSessions,
                now);
            var blocksRebuy = isSourceClass || blocksStopModule || lateJoinReason != null;

            string? mentorName = null;
            if (openClass.MentorId.HasValue
                && mentorById.TryGetValue(openClass.MentorId.Value, out var mentor))
            {
                mentorName = mentor.FullName;
            }

            classes.Add(new RebuyClassDto
            {
                ClassId = openClass.Id,
                Code = openClass.Code,
                Name = openClass.Name,
                Status = openClass.Status,
                StartDate = openClass.StartDate,
                EndDate = openClass.EndDate,
                MentorId = openClass.MentorId,
                MentorName = mentorName,
                MaxCapacity = openClass.MaxCapacity,
                SeatsTaken = seatsTaken,
                SeatsRemaining = seatsRemaining,
                ScheduleSummary = openClass.ScheduleSummary,
                IsEligible = !blocksRebuy,
                IneligibleReason = isSourceClass
                    ? ProgramPurchaseLifecycle.RebuySameClassMessage
                    : blocksStopModule
                        ? ProgramPurchaseLifecycle.RebuyClassIneligibleMessage
                        : lateJoinReason,
                Modules = moduleProgress,
            });
        }

        _logger.LogInformation(
            "[GetRebuyClassesAsync] Student {StudentId} program {ProgramId}: rebuy={IsRebuy}, {Eligible}/{Total} eligible class(es), stop={StopModuleCode}.",
            student.Id,
            programId,
            isRebuy,
            classes.Count(c => c.IsEligible),
            classes.Count,
            stopModule?.Code);

        return new RebuyClassCatalogDto
        {
            ProgramId = programId,
            IsRebuy = isRebuy,
            SourceProgramEnrollmentId = source?.Id,
            SourceStatus = source?.Status,
            SourceEndReason = source?.EndReason,
            StopModuleId = stopModule?.Id,
            StopModuleCode = stopModule?.Code,
            StopModuleName = stopModule?.Name,
            StopModuleOrder = stopModule?.ModuleOrder,
            WithinRebuyWindow = withinWindow,
            CheckoutAmount = checkoutAmount,
            Classes = classes,
        };
    }

    private static RebuyClassModuleProgressDto MapModuleProgress(
        Module module,
        IReadOnlyCollection<ClassSession> classSessions,
        Module? stopModule)
    {
        var progress = ProgramPurchaseLifecycle.ResolveModuleProgress(
            classSessions.Where(cs => cs.ModuleId == module.Id));
        var blocks = stopModule != null
            && module.ModuleOrder >= stopModule.ModuleOrder
            && ProgramPurchaseLifecycle.ModuleProgressBlocksRebuy(progress);

        return new RebuyClassModuleProgressDto
        {
            ModuleId = module.Id,
            Code = module.Code,
            Name = module.Name,
            ModuleOrder = module.ModuleOrder,
            ModuleType = module.ModuleType,
            Progress = progress,
            BlocksRebuy = blocks,
        };
    }
}
