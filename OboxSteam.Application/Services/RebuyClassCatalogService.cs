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
/// Student class picker for checkout: Open-only for first purchase, Completed retakes,
/// and Failed/Dropped after the 3-month window; Open or InProgress with stop-module
/// eligibility after Failed/Dropped inside the window. Also builds Active continuity catalogs.
/// </summary>
public sealed class RebuyClassCatalogService : IRebuyClassCatalogService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClaimsService _claimsService;
    private readonly ProgramPurchaseLifecycle _programPurchaseLifecycle;
    private readonly ClassContinuityCatalogBuilder _catalogBuilder;
    private readonly ICurrentTime _currentTime;
    private readonly ILogger<RebuyClassCatalogService> _logger;

    public RebuyClassCatalogService(
        IUnitOfWork unitOfWork,
        IClaimsService claimsService,
        ProgramPurchaseLifecycle programPurchaseLifecycle,
        ClassContinuityCatalogBuilder catalogBuilder,
        ICurrentTime currentTime,
        ILogger<RebuyClassCatalogService> logger)
    {
        _unitOfWork = unitOfWork;
        _claimsService = claimsService;
        _programPurchaseLifecycle = programPurchaseLifecycle;
        _catalogBuilder = catalogBuilder;
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
        var now = _currentTime.GetCurrentTime();
        var isRebuy = ProgramPurchaseLifecycle.AllowsInProgressClassJoin(source, now);

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

        HashSet<Guid> completedModuleIds = [];
        if (source != null)
        {
            var sourceModules = await _unitOfWork.ModuleEnrollments.GetAllAsync(
                me => me.ProgramEnrollmentId == source.Id
                      && !me.IsDeleted
                      && me.Status == EnrollmentStatus.Completed);
            completedModuleIds = sourceModules.Select(me => me.ModuleId).ToHashSet();
        }

        var withinWindow = source != null && ProgramPurchaseLifecycle.IsWithinRebuyWindow(source, now);
        var checkoutAmount = ProgramPurchaseLifecycle.ResolveCheckoutAmount(program!, source, now);

        var sourceClassIds = source == null
            ? new HashSet<Guid>()
            : await _programPurchaseLifecycle.GetSourceOccupiedClassIdsAsync(source.Id);

        var catalog = await _catalogBuilder.BuildAsync(new ClassContinuityCatalogBuildRequest
        {
            Program = program!,
            Context = ClassContinuityContext.Rebuy,
            SourceEnrollment = source,
            StopModule = stopModule,
            CompletedModuleIds = completedModuleIds,
            ExcludedClassIds = sourceClassIds,
            IncludeInProgressClasses = isRebuy,
            IsRebuy = isRebuy,
            WithinWindow = withinWindow,
            CheckoutAmount = checkoutAmount,
            Now = now,
        });

        _logger.LogInformation(
            "[GetRebuyClassesAsync] Student {StudentId} program {ProgramId}: rebuy={IsRebuy}, {Eligible}/{Total} eligible class(es), stop={StopModuleCode}.",
            student.Id,
            programId,
            isRebuy,
            catalog.Classes.Count(c => c.IsEligible),
            catalog.Classes.Count,
            stopModule?.Code);

        return catalog;
    }

    public async Task<RebuyClassCatalogDto> GetContinuityClassesForModuleEnrollmentAsync(Guid moduleEnrollmentId)
    {
        if (moduleEnrollmentId == Guid.Empty)
        {
            throw ErrorHelper.BadRequest("ModuleEnrollmentId is required.");
        }

        var student = await EnrollmentAccessValidator.GetCurrentStudentForEnrollAsync(
            _unitOfWork,
            _claimsService,
            "Only students can view continuity classes.");

        var enrollment = await _unitOfWork.ModuleEnrollments.GetByIdAsync(moduleEnrollmentId)
            ?? throw ErrorHelper.NotFound($"Module enrollment '{moduleEnrollmentId}' not found.");

        if (enrollment.IsDeleted || enrollment.StudentId != student.Id)
        {
            throw ErrorHelper.NotFound($"Module enrollment '{moduleEnrollmentId}' not found.");
        }

        if (!enrollment.ProgramEnrollmentId.HasValue)
        {
            throw ErrorHelper.BadRequest("Module enrollment must be linked to a program enrollment.");
        }

        var programEnrollment = await _unitOfWork.ProgramEnrollments.GetByIdAsync(
            enrollment.ProgramEnrollmentId.Value)
            ?? throw ErrorHelper.NotFound("Program enrollment not found.");

        if (programEnrollment.IsDeleted || programEnrollment.Status != EnrollmentStatus.Active)
        {
            throw ErrorHelper.BadRequest(
                "Continuity classes are only available while the program enrollment is Active. "
                + "After fail/drop use GET /api/programs/{id}/rebuy-classes.");
        }

        var module = await _unitOfWork.Modules.GetByIdAsync(enrollment.ModuleId)
            ?? throw ErrorHelper.NotFound($"Module '{enrollment.ModuleId}' not found.");

        if (module.IsDeleted)
        {
            throw ErrorHelper.NotFound($"Module '{enrollment.ModuleId}' not found.");
        }

        if (module.ModuleType == ModuleType.Theory)
        {
            throw ErrorHelper.BadRequest(
                "Theory modules do not use class continuity. Redo the assignment on the same class while the window is open.");
        }

        var program = await _unitOfWork.Programs.GetByIdAsync(module.ProgramId);
        ProgramEnrollmentValidator.ValidateProgramExists(program, module.ProgramId);
        ProgramEnrollmentValidator.EnsureProgramPurchasable(program!);

        return await BuildActiveCatalogAsync(student.Id, program!, programEnrollment, module);
    }

    public async Task<RebuyClassCatalogDto> BuildActiveCatalogAsync(
        Guid studentId,
        Program program,
        ProgramEnrollment programEnrollment,
        Module stopModule)
    {
        var now = _currentTime.GetCurrentTime();
        var checkoutAmount = ClassContinuityCatalogBuilder.ResolveActiveContinuityAmount(program);
        if (checkoutAmount <= 0)
        {
            throw ErrorHelper.BadRequest("This program does not have a valid price for continuity.");
        }

        var completedModuleIds = (await _unitOfWork.ModuleEnrollments.GetAllAsync(
                me => me.ProgramEnrollmentId == programEnrollment.Id
                      && !me.IsDeleted
                      && me.Status == EnrollmentStatus.Completed))
            .Select(me => me.ModuleId)
            .ToHashSet();

        var excludedClassIds = await _programPurchaseLifecycle.GetSourceOccupiedClassIdsAsync(
            programEnrollment.Id);

        var catalog = await _catalogBuilder.BuildAsync(new ClassContinuityCatalogBuildRequest
        {
            Program = program,
            Context = ClassContinuityContext.ActiveRedelivery,
            SourceEnrollment = programEnrollment,
            StopModule = stopModule,
            CompletedModuleIds = completedModuleIds,
            ExcludedClassIds = excludedClassIds,
            IncludeInProgressClasses = true,
            IsRebuy = false,
            WithinWindow = true,
            CheckoutAmount = checkoutAmount,
            ModuleSessionsFocusModuleId = stopModule.Id,
            Now = now,
        });

        _logger.LogInformation(
            "[BuildActiveCatalogAsync] Student {StudentId} program {ProgramId} module {ModuleId}: "
            + "{Eligible}/{Total} eligible class(es), amount={CheckoutAmount}.",
            studentId,
            program.Id,
            stopModule.Id,
            catalog.Classes.Count(c => c.IsEligible),
            catalog.Classes.Count,
            checkoutAmount);

        return catalog;
    }
}
