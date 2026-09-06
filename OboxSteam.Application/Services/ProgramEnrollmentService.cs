using Microsoft.Extensions.Logging;
using OboxSteam.Application.Commons;
using OboxSteam.Application.DTOs.EnrollmentDTO;
using OboxSteam.Application.Interfaces;
using OboxSteam.Application.Notifications;
using OboxSteam.Application.Utils;
using OboxSteam.Application.Validation;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;
using OboxSteam.Domain.Interfaces;

namespace OboxSteam.Application.Services;

public sealed class ProgramEnrollmentService : IProgramEnrollmentService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClaimsService _claimsService;
    private readonly ILogger<ProgramEnrollmentService> _logger;
    private readonly INotificationPublisher _notificationPublisher;
    private readonly ProgramPurchaseLifecycle _programPurchaseLifecycle;

    public ProgramEnrollmentService(
        IUnitOfWork unitOfWork,
        IClaimsService claimsService,
        ILogger<ProgramEnrollmentService> logger,
        INotificationPublisher notificationPublisher,
        ProgramPurchaseLifecycle programPurchaseLifecycle)
    {
        _unitOfWork = unitOfWork;
        _claimsService = claimsService;
        _logger = logger;
        _notificationPublisher = notificationPublisher;
        _programPurchaseLifecycle = programPurchaseLifecycle;
    }

    public async Task<ProgramEnrollment> GetOrCreatePendingEnrollmentAsync(Guid studentId, Guid programId)
    {
        ProgramEnrollmentValidator.ValidateStudentIdRequired(studentId);
        ProgramEnrollmentValidator.ValidateProgramIdRequired(programId);

        var programEntity = await _unitOfWork.Programs.GetByIdAsync(programId);
        if (programEntity == null || programEntity.IsDeleted)
        {
            _logger.LogWarning("[GetOrCreatePendingEnrollmentAsync] Program {ProgramId} not found.", programId);
            throw ErrorHelper.NotFound($"Program with id '{programId}' not found.");
        }

        ProgramEnrollmentValidator.EnsureProgramPurchasable(programEntity);

        var enrollments = await _unitOfWork.ProgramEnrollments.GetAllAsync(
            pe => pe.StudentId == studentId && pe.ProgramId == programId && !pe.IsDeleted);

        var pendingEnrollment = enrollments.FirstOrDefault(pe => pe.Status == EnrollmentStatus.PendingPayment);
        if (pendingEnrollment != null)
        {
            if (pendingEnrollment.SourceProgramEnrollmentId == null)
            {
                var pendingSource = ProgramPurchaseLifecycle.FindRebuySource(enrollments);
                if (pendingSource != null)
                {
                    pendingEnrollment.SourceProgramEnrollmentId = pendingSource.Id;
                    await _unitOfWork.ProgramEnrollments.Update(pendingEnrollment);
                    await _unitOfWork.SaveChangesAsync();
                }
            }

            _logger.LogInformation(
                "[GetOrCreatePendingEnrollmentAsync] Reusing existing PendingPayment enrollment {EnrollmentId} for student {StudentId} on program {ProgramId}.",
                pendingEnrollment.Id,
                studentId,
                programId);

            return pendingEnrollment;
        }

        if (enrollments.Any(pe => pe.Status is EnrollmentStatus.Active or EnrollmentStatus.Deferred))
        {
            _logger.LogWarning(
                "[GetOrCreatePendingEnrollmentAsync] Student {StudentId} already enrolled in program {ProgramId} with an open enrollment.",
                studentId,
                programId);
            throw ErrorHelper.Conflict("Student is already enrolled in this program.");
        }

        await ProgramEnrollmentValidator.ValidateUnderInProgressProgramLimitAsync(
            _unitOfWork,
            studentId);

        var rebuySource = ProgramPurchaseLifecycle.FindRebuySource(enrollments);

        var now = DateTime.UtcNow;
        var enrollment = new ProgramEnrollment
        {
            StudentId = studentId,
            ProgramId = programId,
            Status = EnrollmentStatus.PendingPayment,
            ProgressPercent = 0m,
            EnrolledAt = now,
            SourceProgramEnrollmentId = rebuySource?.Id,
        };

        await _unitOfWork.ProgramEnrollments.AddAsync(enrollment);
        await _unitOfWork.SaveChangesAsync();

        await _notificationPublisher.PublishAsync(
            NotificationCatalog.ProgramPendingPayment(
                studentId,
                programId,
                enrollment.Id,
                programEntity.Name));

        _logger.LogInformation(
            "[GetOrCreatePendingEnrollmentAsync] Created new PendingPayment enrollment {EnrollmentId} for student {StudentId} on program {ProgramId}.",
            enrollment.Id,
            studentId,
            programId);

        return enrollment;
    }

    public async Task<ProgramEnrollmentResponseDto> WithdrawAsync(Guid enrollmentId)
    {
        var student = await EnrollmentAccessValidator.GetCurrentStudentForEnrollAsync(
            _unitOfWork,
            _claimsService,
            "Only students can withdraw from a program.");

        var enrollment = await _unitOfWork.ProgramEnrollments.GetByIdAsync(enrollmentId);
        if (enrollment == null || enrollment.IsDeleted)
        {
            throw ErrorHelper.NotFound($"Program enrollment with id '{enrollmentId}' not found.");
        }

        if (enrollment.StudentId != student.Id)
        {
            throw ErrorHelper.Forbidden("You can only withdraw your own program enrollment.");
        }

        if (enrollment.Status != EnrollmentStatus.Active)
        {
            throw ErrorHelper.BadRequest(
                "Only an Active enrollment can be withdrawn. " +
                "Use checkout abandon for PendingPayment enrollments.");
        }

        await _programPurchaseLifecycle.CloseAsync(
            enrollment,
            ProgramPurchaseEndReason.Withdraw,
            endedModuleId: null);

        _logger.LogInformation(
            "[WithdrawAsync] Student {StudentId} withdrew from program enrollment {EnrollmentId}.",
            student.Id,
            enrollment.Id);

        return await GetProgramEnrollmentByIdAsync(enrollment.Id);
    }

    public async Task<ProgramEnrollmentResponseDto> GetProgramEnrollmentByIdAsync(Guid id)
    {
        await EnrollmentAccessValidator.GetCurrentUserForGetAsync(
            _unitOfWork,
            _claimsService,
            ProgramEnrollmentValidator.ViewListForbiddenMessage);

        var enrollment = await _unitOfWork.ProgramEnrollments.GetByIdAsync(id, pe => pe.Program);
        if (enrollment == null || enrollment.IsDeleted)
        {
            _logger.LogWarning("[GetProgramEnrollmentByIdAsync] Enrollment {Id} not found.", id);
            throw ErrorHelper.NotFound($"Program enrollment with id '{id}' not found.");
        }

        await EnrollmentAccessValidator.EnsureCanViewEnrollmentAsync(
            _unitOfWork,
            _claimsService,
            enrollment.StudentId,
            ProgramEnrollmentValidator.ViewEnrollmentForbiddenMessage);

        var program = enrollment.Program
            ?? await _unitOfWork.Programs.GetByIdAsync(enrollment.ProgramId);

        if (program == null || program.IsDeleted)
        {
            _logger.LogWarning(
                "[GetProgramEnrollmentByIdAsync] Program {ProgramId} not found for enrollment {Id}.",
                enrollment.ProgramId,
                id);
            throw ErrorHelper.NotFound($"Program with id '{enrollment.ProgramId}' not found.");
        }

        var related = await _unitOfWork.ProgramEnrollments.GetAllAsync(
            pe => pe.StudentId == enrollment.StudentId
                  && pe.ProgramId == enrollment.ProgramId
                  && !pe.IsDeleted);
        var byId = related.ToDictionary(pe => pe.Id);

        return MapToDto(enrollment, program, byId);
    }

    public async Task<Pagination<ProgramEnrollmentResponseDto>> GetMyProgramEnrollmentsAsync(
        Guid? programId,
        string? sortBy,
        bool isDescending,
        int page,
        int pageSize,
        bool includeSuperseded = false)
    {
        _logger.LogInformation(
            "[GetMyProgramEnrollmentsAsync] Start — programId: {ProgramId}, page: {Page}, pageSize: {PageSize}, includeSuperseded: {IncludeSuperseded}",
            programId,
            page,
            pageSize,
            includeSuperseded);

        ProgramEnrollmentValidator.ValidatePagination(page, pageSize);

        var currentUser = await EnrollmentAccessValidator.GetCurrentUserForGetAsync(
            _unitOfWork,
            _claimsService,
            ProgramEnrollmentValidator.ViewListForbiddenMessage);

        var query = _unitOfWork.ProgramEnrollments
            .GetQueryable()
            .Where(pe => !pe.IsDeleted);

        if (programId.HasValue)
        {
            query = query.Where(pe => pe.ProgramId == programId.Value);
        }

        if (currentUser.Role == RoleType.Student)
        {
            query = query.Where(pe => pe.StudentId == currentUser.Id);
        }
        else if (currentUser.Role == RoleType.Parent)
        {
            query = await ApplyParentStudentFilterAsync(query, currentUser.Id);
        }
        else
        {
            ProgramEnrollmentValidator.ValidateCanListProgramEnrollments(currentUser.Role);
        }

        var allItems = query.ToList();
        var result = await PaginateAndMapAsync(
            allItems,
            sortBy,
            isDescending,
            page,
            pageSize,
            includeSuperseded);

        _logger.LogInformation(
            "[GetMyProgramEnrollmentsAsync] Retrieved {Count}/{Total} enrollments for user {UserId}.",
            result.Items.Count,
            result.TotalCount,
            currentUser.Id);

        return result;
    }

    public async Task<Pagination<ProgramEnrollmentResponseDto>> GetProgramEnrollmentsByStudentIdAsync(
        Guid studentId,
        string? sortBy,
        bool isDescending,
        int page,
        int pageSize,
        bool includeSuperseded = false)
    {
        _logger.LogInformation(
            "[GetProgramEnrollmentsByStudentIdAsync] Start — studentId: {StudentId}, page: {Page}, pageSize: {PageSize}, includeSuperseded: {IncludeSuperseded}",
            studentId,
            page,
            pageSize,
            includeSuperseded);

        ProgramEnrollmentValidator.ValidatePagination(page, pageSize);
        ProgramEnrollmentValidator.ValidateStudentIdRequired(studentId);

        await EnrollmentAccessValidator.EnsureCanViewEnrollmentAsync(
            _unitOfWork,
            _claimsService,
            studentId,
            ProgramEnrollmentValidator.ViewEnrollmentForbiddenMessage);

        var student = await _unitOfWork.Users.GetByIdAsync(studentId);
        ProgramEnrollmentValidator.ValidateStudentExists(student, studentId);

        var allItems = _unitOfWork.ProgramEnrollments
            .GetQueryable()
            .Where(pe => pe.StudentId == studentId && !pe.IsDeleted)
            .ToList();

        var result = await PaginateAndMapAsync(
            allItems,
            sortBy,
            isDescending,
            page,
            pageSize,
            includeSuperseded);

        _logger.LogInformation(
            "[GetProgramEnrollmentsByStudentIdAsync] Retrieved {Count}/{Total} enrollments.",
            result.Items.Count,
            result.TotalCount);

        return result;
    }

    private async Task<Pagination<ProgramEnrollmentResponseDto>> PaginateAndMapAsync(
        IReadOnlyList<ProgramEnrollment> allItems,
        string? sortBy,
        bool isDescending,
        int page,
        int pageSize,
        bool includeSuperseded)
    {
        var byId = allItems.ToDictionary(pe => pe.Id);
        var visible = includeSuperseded
            ? allItems.ToList()
            : SelectCurrentEnrollments(allItems);

        var sorted = ApplySort(visible, sortBy, isDescending);
        var totalCount = sorted.Count;
        var pageItems = sorted
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        var programIds = pageItems.Select(pe => pe.ProgramId).Distinct().ToList();
        var programs = await _unitOfWork.Programs.GetAllAsync(p => programIds.Contains(p.Id) && !p.IsDeleted);
        var programsById = programs.ToDictionary(p => p.Id);

        var dtos = new List<ProgramEnrollmentResponseDto>(pageItems.Count);
        foreach (var enrollment in pageItems)
        {
            if (!programsById.TryGetValue(enrollment.ProgramId, out var program))
            {
                throw ErrorHelper.NotFound($"Program with id '{enrollment.ProgramId}' not found.");
            }

            dtos.Add(MapToDto(enrollment, program, byId));
        }

        return new Pagination<ProgramEnrollmentResponseDto>(dtos, totalCount, page, pageSize);
    }

    /// <summary>
    /// One row per (student, program): prefer PendingPayment / Active / Deferred; otherwise the
    /// latest non-superseded terminal. Open rebuy rows hide prior terminals without requiring
    /// <see cref="ProgramEnrollment.SupersededByEnrollmentId"/> yet (that is set only on Active).
    /// </summary>
    internal static List<ProgramEnrollment> SelectCurrentEnrollments(
        IEnumerable<ProgramEnrollment> enrollments)
    {
        return enrollments
            .GroupBy(pe => (pe.StudentId, pe.ProgramId))
            .Select(PickCurrentEnrollment)
            .ToList();
    }

    internal static ProgramEnrollment PickCurrentEnrollment(
        IEnumerable<ProgramEnrollment> group)
    {
        var list = group.ToList();

        var open = list
            .Where(pe => pe.Status is EnrollmentStatus.PendingPayment
                or EnrollmentStatus.Active
                or EnrollmentStatus.Deferred)
            .OrderByDescending(CurrentSortKey)
            .FirstOrDefault();
        if (open != null)
        {
            return open;
        }

        var nonSupersededTerminal = list
            .Where(pe => pe.SupersededByEnrollmentId == null)
            .OrderByDescending(CurrentSortKey)
            .FirstOrDefault();
        if (nonSupersededTerminal != null)
        {
            return nonSupersededTerminal;
        }

        return list.OrderByDescending(CurrentSortKey).First();
    }

    private static DateTime CurrentSortKey(ProgramEnrollment pe)
        => pe.EndedAt ?? pe.CompletedAt ?? pe.EnrolledAt ?? pe.CreatedAt;

    private static List<ProgramEnrollment> ApplySort(
        IEnumerable<ProgramEnrollment> enrollments,
        string? sortBy,
        bool isDescending)
    {
        return sortBy?.ToLower() switch
        {
            "progresspercent" => isDescending
                ? enrollments.OrderByDescending(pe => pe.ProgressPercent).ToList()
                : enrollments.OrderBy(pe => pe.ProgressPercent).ToList(),
            "status" => isDescending
                ? enrollments.OrderByDescending(pe => pe.Status).ToList()
                : enrollments.OrderBy(pe => pe.Status).ToList(),
            "createdat" => isDescending
                ? enrollments.OrderByDescending(pe => pe.CreatedAt).ToList()
                : enrollments.OrderBy(pe => pe.CreatedAt).ToList(),
            "enrolledat" or _ => isDescending
                ? enrollments.OrderByDescending(pe => pe.EnrolledAt).ToList()
                : enrollments.OrderBy(pe => pe.EnrolledAt).ToList(),
        };
    }

    private static ProgramEnrollmentResponseDto MapToDto(
        ProgramEnrollment enrollment,
        Program program,
        IReadOnlyDictionary<Guid, ProgramEnrollment> byId)
    {
        ProgramEnrollment? source = null;
        if (enrollment.SourceProgramEnrollmentId is Guid sourceId)
        {
            byId.TryGetValue(sourceId, out source);
        }

        return new ProgramEnrollmentResponseDto
        {
            Id = enrollment.Id,
            StudentId = enrollment.StudentId,
            ProgramId = enrollment.ProgramId,
            Status = enrollment.Status,
            ProgressPercent = enrollment.ProgressPercent,
            EnrolledAt = enrollment.EnrolledAt,
            StartedAt = enrollment.StartedAt,
            CompletedAt = enrollment.CompletedAt,
            EndReason = enrollment.EndReason,
            EndedModuleId = enrollment.EndedModuleId,
            EndedAt = enrollment.EndedAt,
            SourceProgramEnrollmentId = enrollment.SourceProgramEnrollmentId,
            IsRebuy = enrollment.SourceProgramEnrollmentId.HasValue,
            AttemptNumber = ResolveAttemptNumber(enrollment, byId),
            PriorStatus = source?.Status,
            PriorEndReason = source?.EndReason,
            IsSuperseded = enrollment.SupersededByEnrollmentId.HasValue,
            SupersededByEnrollmentId = enrollment.SupersededByEnrollmentId,
            CreatedAt = enrollment.CreatedAt,
            UpdatedAt = enrollment.UpdatedAt,
            Code = program.Code,
            Name = program.Name,
            SeriesName = program.SeriesName,
            Description = program.Description,
            Level = program.Level,
            EstimatedDuration = program.EstimatedDuration,
            SkillsGained = program.SkillsGained,
            Rating = program.Rating,
            TotalReviews = program.TotalReviews,
            ThumbnailUrl = program.ThumbnailUrl,
            ProgramStatus = program.Status,
            Price = program.Price,
        };
    }

    internal static int ResolveAttemptNumber(
        ProgramEnrollment enrollment,
        IReadOnlyDictionary<Guid, ProgramEnrollment> byId)
    {
        var attempt = 1;
        var current = enrollment;
        var guard = 0;
        while (current.SourceProgramEnrollmentId is Guid sourceId
               && byId.TryGetValue(sourceId, out var source)
               && guard++ < 50)
        {
            attempt++;
            current = source;
        }

        if (enrollment.SourceProgramEnrollmentId.HasValue && attempt == 1)
        {
            return 2;
        }

        return attempt;
    }

    public async Task<ProgramEnrollmentClassDto> GetProgramEnrollmentClassAsync(Guid enrollmentId)
    {
        _logger.LogInformation(
            "[GetProgramEnrollmentClassAsync] Start — enrollmentId: {EnrollmentId}",
            enrollmentId);

        await EnrollmentAccessValidator.GetCurrentUserForGetAsync(
            _unitOfWork,
            _claimsService,
            ProgramEnrollmentValidator.ViewListForbiddenMessage);

        var enrollment = await _unitOfWork.ProgramEnrollments.GetByIdAsync(enrollmentId);
        if (enrollment == null || enrollment.IsDeleted)
        {
            _logger.LogWarning("[GetProgramEnrollmentClassAsync] Enrollment {Id} not found.", enrollmentId);
            throw ErrorHelper.NotFound($"Program enrollment with id '{enrollmentId}' not found.");
        }

        await EnrollmentAccessValidator.EnsureCanViewEnrollmentAsync(
            _unitOfWork,
            _claimsService,
            enrollment.StudentId,
            ProgramEnrollmentValidator.ViewEnrollmentForbiddenMessage);

        var activeClassEnrollments = await _unitOfWork.ClassEnrollments.GetAllAsync(
            ce => ce.ProgramEnrollmentId == enrollmentId
                  && ce.Status == ClassEnrollmentStatus.Active
                  && !ce.IsDeleted);

        var primaryEnrollment = activeClassEnrollments
            .FirstOrDefault(ce => ce.Kind == ClassEnrollmentKind.Primary)
            ?? activeClassEnrollments.FirstOrDefault();

        var retakeSeats = await _unitOfWork.ClassEnrollments.GetAllAsync(
            ce => ce.ProgramEnrollmentId == enrollmentId
                  && ce.Kind == ClassEnrollmentKind.Retake
                  && (ce.Status == ClassEnrollmentStatus.Active
                      || ce.Status == ClassEnrollmentStatus.Completed)
                  && !ce.IsDeleted);

        var displayKind = retakeSeats.Count > 0
            ? ClassEnrollmentKind.Retake
            : primaryEnrollment?.Kind ?? ClassEnrollmentKind.Primary;

        var result = new ProgramEnrollmentClassDto
        {
            ProgramEnrollmentId = enrollmentId,
            ClassId = primaryEnrollment?.ClassId,
            ClassEnrollmentId = primaryEnrollment?.Id,
            Kind = displayKind,
        };

        _logger.LogInformation(
            "[GetProgramEnrollmentClassAsync] Enrollment {EnrollmentId} classId: {ClassId}",
            enrollmentId,
            result.ClassId);

        return result;
    }

    private async Task<IQueryable<ProgramEnrollment>> ApplyParentStudentFilterAsync(
        IQueryable<ProgramEnrollment> query,
        Guid parentId)
    {
        var parentLinks = await _unitOfWork.ParentStudents.GetAllAsync(
            ps => ps.ParentId == parentId && ps.IsVerified && !ps.IsDeleted);

        var linkedStudentIds = parentLinks.Select(ps => ps.StudentId).Distinct().ToList();

        if (linkedStudentIds.Count == 0)
        {
            return query.Where(pe => false);
        }

        return query.Where(pe => linkedStudentIds.Contains(pe.StudentId));
    }
}
