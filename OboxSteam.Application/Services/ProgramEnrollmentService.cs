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

    public ProgramEnrollmentService(
        IUnitOfWork unitOfWork,
        IClaimsService claimsService,
        ILogger<ProgramEnrollmentService> logger,
        INotificationPublisher notificationPublisher)
    {
        _unitOfWork = unitOfWork;
        _claimsService = claimsService;
        _logger = logger;
        _notificationPublisher = notificationPublisher;
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

        var existingEnrollment = await _unitOfWork.ProgramEnrollments.FirstOrDefaultAsync(
            pe => pe.StudentId == studentId && pe.ProgramId == programId && !pe.IsDeleted);

        if (existingEnrollment != null)
        {
            if (existingEnrollment.Status != EnrollmentStatus.PendingPayment)
            {
                _logger.LogWarning(
                    "[GetOrCreatePendingEnrollmentAsync] Student {StudentId} already enrolled in program {ProgramId} with status {Status}.",
                    studentId,
                    programId,
                    existingEnrollment.Status);
                throw ErrorHelper.Conflict("Student is already enrolled in this program.");
            }

            _logger.LogInformation(
                "[GetOrCreatePendingEnrollmentAsync] Reusing existing PendingPayment enrollment {EnrollmentId} for student {StudentId} on program {ProgramId}.",
                existingEnrollment.Id,
                studentId,
                programId);

            return existingEnrollment;
        }

        var now = DateTime.UtcNow;
        var enrollment = new ProgramEnrollment
        {
            StudentId = studentId,
            ProgramId = programId,
            Status = EnrollmentStatus.PendingPayment,
            ProgressPercent = 0m,
            EnrolledAt = now,
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

    public async Task<Pagination<ProgramEnrollmentResponseDto>> GetMyProgramEnrollmentsAsync(
        Guid? programId,
        string? sortBy,
        bool isDescending,
        int page,
        int pageSize)
    {
        _logger.LogInformation(
            "[GetMyProgramEnrollmentsAsync] Start — programId: {ProgramId}, page: {Page}, pageSize: {PageSize}",
            programId,
            page,
            pageSize);

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

        query = sortBy?.ToLower() switch
        {
            "progresspercent" => isDescending
                ? query.OrderByDescending(pe => pe.ProgressPercent)
                : query.OrderBy(pe => pe.ProgressPercent),
            "status" => isDescending
                ? query.OrderByDescending(pe => pe.Status)
                : query.OrderBy(pe => pe.Status),
            "createdat" => isDescending
                ? query.OrderByDescending(pe => pe.CreatedAt)
                : query.OrderBy(pe => pe.CreatedAt),
            "enrolledat" or _ => isDescending
                ? query.OrderByDescending(pe => pe.EnrolledAt)
                : query.OrderBy(pe => pe.EnrolledAt),
        };

        var totalCount = query.Count();

        var items = query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        var programIds = items.Select(pe => pe.ProgramId).Distinct().ToList();
        var programs = await _unitOfWork.Programs.GetAllAsync(p => programIds.Contains(p.Id) && !p.IsDeleted);
        var programsById = programs.ToDictionary(p => p.Id);

        var dtos = new List<ProgramEnrollmentResponseDto>();
        foreach (var enrollment in items)
        {
            if (!programsById.TryGetValue(enrollment.ProgramId, out var program))
            {
                throw ErrorHelper.NotFound($"Program with id '{enrollment.ProgramId}' not found.");
            }

            dtos.Add(new ProgramEnrollmentResponseDto
            {
                Id = enrollment.Id,
                StudentId = enrollment.StudentId,
                ProgramId = enrollment.ProgramId,
                Status = enrollment.Status,
                ProgressPercent = enrollment.ProgressPercent,
                EnrolledAt = enrollment.EnrolledAt,
                StartedAt = enrollment.StartedAt,
                CompletedAt = enrollment.CompletedAt,
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
            });
        }

        _logger.LogInformation(
            "[GetMyProgramEnrollmentsAsync] Retrieved {Count}/{Total} enrollments for user {UserId}.",
            dtos.Count,
            totalCount,
            currentUser.Id);

        return new Pagination<ProgramEnrollmentResponseDto>(dtos, totalCount, page, pageSize);
    }

    public async Task<Pagination<ProgramEnrollmentResponseDto>> GetProgramEnrollmentsByStudentIdAsync(
        Guid studentId,
        string? sortBy,
        bool isDescending,
        int page,
        int pageSize)
    {
        _logger.LogInformation(
            "[GetProgramEnrollmentsByStudentIdAsync] Start — studentId: {StudentId}, page: {Page}, pageSize: {PageSize}",
            studentId,
            page,
            pageSize);

        ProgramEnrollmentValidator.ValidatePagination(page, pageSize);
        ProgramEnrollmentValidator.ValidateStudentIdRequired(studentId);

        await EnrollmentAccessValidator.EnsureCanViewEnrollmentAsync(
            _unitOfWork,
            _claimsService,
            studentId,
            ProgramEnrollmentValidator.ViewEnrollmentForbiddenMessage);

        var student = await _unitOfWork.Users.GetByIdAsync(studentId);
        ProgramEnrollmentValidator.ValidateStudentExists(student, studentId);

        var query = _unitOfWork.ProgramEnrollments
            .GetQueryable()
            .Where(pe => pe.StudentId == studentId && !pe.IsDeleted);

        query = sortBy?.ToLower() switch
        {
            "progresspercent" => isDescending
                ? query.OrderByDescending(pe => pe.ProgressPercent)
                : query.OrderBy(pe => pe.ProgressPercent),
            "status" => isDescending
                ? query.OrderByDescending(pe => pe.Status)
                : query.OrderBy(pe => pe.Status),
            "createdat" => isDescending
                ? query.OrderByDescending(pe => pe.CreatedAt)
                : query.OrderBy(pe => pe.CreatedAt),
            "enrolledat" or _ => isDescending
                ? query.OrderByDescending(pe => pe.EnrolledAt)
                : query.OrderBy(pe => pe.EnrolledAt),
        };

        var totalCount = query.Count();

        var items = query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        var programIds = items.Select(pe => pe.ProgramId).Distinct().ToList();
        var programs = await _unitOfWork.Programs.GetAllAsync(p => programIds.Contains(p.Id) && !p.IsDeleted);
        var programsById = programs.ToDictionary(p => p.Id);

        var dtos = new List<ProgramEnrollmentResponseDto>();
        foreach (var enrollment in items)
        {
            if (!programsById.TryGetValue(enrollment.ProgramId, out var program))
            {
                throw ErrorHelper.NotFound($"Program with id '{enrollment.ProgramId}' not found.");
            }

            dtos.Add(new ProgramEnrollmentResponseDto
            {
                Id = enrollment.Id,
                StudentId = enrollment.StudentId,
                ProgramId = enrollment.ProgramId,
                Status = enrollment.Status,
                ProgressPercent = enrollment.ProgressPercent,
                EnrolledAt = enrollment.EnrolledAt,
                StartedAt = enrollment.StartedAt,
                CompletedAt = enrollment.CompletedAt,
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
            });
        }

        _logger.LogInformation(
            "[GetProgramEnrollmentsByStudentIdAsync] Retrieved {Count}/{Total} enrollments.",
            dtos.Count,
            totalCount);

        return new Pagination<ProgramEnrollmentResponseDto>(dtos, totalCount, page, pageSize);
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

        var activeClassEnrollment = await _unitOfWork.ClassEnrollments.FirstOrDefaultAsync(
            ce => ce.ProgramEnrollmentId == enrollmentId
                  && ce.Status == ClassEnrollmentStatus.Active
                  && !ce.IsDeleted);

        var result = new ProgramEnrollmentClassDto
        {
            ProgramEnrollmentId = enrollmentId,
            ClassId = activeClassEnrollment?.ClassId,
            ClassEnrollmentId = activeClassEnrollment?.Id,
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
