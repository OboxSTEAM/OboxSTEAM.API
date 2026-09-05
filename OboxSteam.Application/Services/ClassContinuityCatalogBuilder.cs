using OboxSteam.Application.DTOs.ClassDTO;
using OboxSteam.Application.DTOs.ClassRedeliveryDTO;
using OboxSteam.Application.Validation;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;
using OboxSteam.Domain.Interfaces;

namespace OboxSteam.Application.Services;

/// <summary>
/// Shared Open / InProgress Standard class list for Active redelivery and Failed/Dropped rebuy.
/// </summary>
public sealed class ClassContinuityCatalogBuilder
{
    private readonly IUnitOfWork _unitOfWork;

    public ClassContinuityCatalogBuilder(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<RebuyClassCatalogDto> BuildAsync(ClassContinuityCatalogBuildRequest request)
    {
        var program = request.Program;
        var now = request.Now;
        var includeInProgress = request.IncludeInProgressClasses;
        var stopModule = request.StopModule;
        var completedModuleIds = request.CompletedModuleIds;
        var excludedClassIds = request.ExcludedClassIds;

        await ClassSeatHoldHelper.ReleaseExpiredHoldsAsync(_unitOfWork);

        var modules = (await _unitOfWork.Modules.GetAllAsync(
                m => m.ProgramId == program.Id && !m.IsDeleted))
            .OrderBy(m => m.ModuleOrder)
            .ToList();

        var openOrRunningClasses = await _unitOfWork.Classes.GetAllAsync(
            c => c.ProgramId == program.Id
                 && c.Kind == ClassKind.Standard
                 && !c.IsDeleted
                 && (c.Status == ClassStatus.Open
                     || (includeInProgress && c.Status == ClassStatus.InProgress)));

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

        var applyStopModuleGate = includeInProgress && stopModule != null;
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
                .Select(module => MapModuleProgress(
                    module,
                    classSessions,
                    stopModule,
                    applyStopModuleGate,
                    completedModuleIds,
                    now))
                .ToList();

            var isSourceClass = excludedClassIds.Contains(openClass.Id);
            var blocksStopModule = applyStopModuleGate
                && ProgramPurchaseLifecycle.ClassBlocksRebuy(
                    modules,
                    classSessions,
                    stopModule!.ModuleOrder);
            var lateJoinReason = ClassEnrollmentValidator.GetLateJoinBlockReason(
                openClass,
                classSessions,
                now);
            var blocks = isSourceClass || blocksStopModule || lateJoinReason != null;

            string? mentorName = null;
            if (openClass.MentorId.HasValue
                && mentorById.TryGetValue(openClass.MentorId.Value, out var mentor))
            {
                mentorName = mentor.FullName;
            }

            var moduleSessions = BuildModuleSessions(classSessions, request.ModuleSessionsFocusModuleId);

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
                IsEligible = !blocks,
                IneligibleReason = isSourceClass
                    ? ProgramPurchaseLifecycle.RebuySameClassMessage
                    : blocksStopModule
                        ? ProgramPurchaseLifecycle.RebuyClassIneligibleMessage
                        : lateJoinReason,
                Modules = moduleProgress,
                ModuleSessions = moduleSessions,
            });
        }

        var source = request.SourceEnrollment;

        return new RebuyClassCatalogDto
        {
            ProgramId = program.Id,
            Context = request.Context,
            IsRebuy = request.IsRebuy,
            SourceProgramEnrollmentId = source?.Id,
            SourceStatus = source?.Status,
            SourceEndReason = source?.EndReason,
            StopModuleId = stopModule?.Id,
            StopModuleCode = stopModule?.Code,
            StopModuleName = stopModule?.Name,
            StopModuleOrder = stopModule?.ModuleOrder,
            WithinRebuyWindow = request.WithinWindow,
            CheckoutAmount = request.CheckoutAmount,
            Classes = classes,
        };
    }

    /// <summary>Active continuity amount: always 50% of <see cref="Program.Price"/> (no expiry while Active).</summary>
    public static decimal ResolveActiveContinuityAmount(Program program)
        => ProgramPurchaseLifecycle.ResolveContinuityFee(program);

    private static RebuyClassModuleProgressDto MapModuleProgress(
        Module module,
        IReadOnlyCollection<ClassSession> classSessions,
        Module? stopModule,
        bool applyStopModuleGate,
        IReadOnlySet<Guid> completedModuleIds,
        DateTime now)
    {
        var moduleSessions = classSessions.Where(cs => cs.ModuleId == module.Id).ToList();
        var progress = ProgramPurchaseLifecycle.ResolveModuleProgress(moduleSessions);
        var blocks = applyStopModuleGate
            && stopModule != null
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
            CreditHint = ProgramPurchaseLifecycle.ResolveCreditHint(
                completedModuleIds.Contains(module.Id),
                moduleSessions,
                now),
        };
    }

    private static List<ClassRedeliveryCandidateSessionDto> BuildModuleSessions(
        IReadOnlyCollection<ClassSession> classSessions,
        Guid? focusModuleId)
    {
        if (!focusModuleId.HasValue)
        {
            return [];
        }

        return classSessions
            .Where(cs => cs.ModuleId == focusModuleId.Value
                         && cs.Status != ClassSessionStatus.Cancelled
                         && !cs.IsDeleted)
            .OrderBy(cs => cs.StartTime)
            .Select(cs => new ClassRedeliveryCandidateSessionDto
            {
                SessionId = cs.Id,
                Title = cs.Title,
                StartTime = cs.StartTime,
                EndTime = cs.EndTime,
                SessionKind = cs.SessionKind,
            })
            .ToList();
    }
}

/// <summary>Inputs for <see cref="ClassContinuityCatalogBuilder.BuildAsync"/>.</summary>
public sealed class ClassContinuityCatalogBuildRequest
{
    public required Program Program { get; init; }

    public required ClassContinuityContext Context { get; init; }

    public ProgramEnrollment? SourceEnrollment { get; init; }

    public Module? StopModule { get; init; }

    public IReadOnlySet<Guid> CompletedModuleIds { get; init; } = new HashSet<Guid>();

    public IReadOnlySet<Guid> ExcludedClassIds { get; init; } = new HashSet<Guid>();

    public bool IncludeInProgressClasses { get; init; }

    public bool IsRebuy { get; init; }

    public bool WithinWindow { get; init; }

    public decimal CheckoutAmount { get; init; }

    /// <summary>When set, each class includes sessions for this module (Active redelivery).</summary>
    public Guid? ModuleSessionsFocusModuleId { get; init; }

    public DateTime Now { get; init; }
}
