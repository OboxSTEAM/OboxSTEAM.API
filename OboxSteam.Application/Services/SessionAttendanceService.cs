using Microsoft.Extensions.Logging;
using OboxSteam.Application.Commons;
using OboxSteam.Application.DTOs.SessionAttendanceDTO;
using OboxSteam.Application.Interfaces;
using OboxSteam.Application.Utils;
using OboxSteam.Application.Validation;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;
using OboxSteam.Domain.Interfaces;

namespace OboxSteam.Application.Services;

public sealed class SessionAttendanceService : ISessionAttendanceService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClaimsService _claimsService;
    private readonly ILogger<SessionAttendanceService> _logger;

    public SessionAttendanceService(
        IUnitOfWork unitOfWork,
        IClaimsService claimsService,
        ILogger<SessionAttendanceService> logger)
    {
        _unitOfWork = unitOfWork;
        _claimsService = claimsService;
        _logger = logger;
    }

    public async Task<SessionAttendanceResponseDto> GetSessionAttendanceByIdAsync(Guid id)
    {
        _logger.LogInformation("[GetSessionAttendanceByIdAsync] Start — id: {Id}", id);

        var attendance = await _unitOfWork.SessionAttendances.GetByIdAsync(id);
        SessionAttendanceValidator.ValidateSessionAttendanceExists(attendance, id);

        var classSession = await _unitOfWork.ClassSessions.GetByIdAsync(attendance!.ClassSessionId);
        ClassSessionValidator.ValidateClassSessionExists(classSession, attendance.ClassSessionId);

        await SessionAttendanceValidator.EnsureCanViewSessionAttendanceAsync(
            _unitOfWork,
            _claimsService,
            attendance,
            classSession!);

        return new SessionAttendanceResponseDto
        {
            Id = attendance.Id,
            ClassSessionId = attendance.ClassSessionId,
            StudentId = attendance.StudentId,
            ModuleEnrollmentId = attendance.ModuleEnrollmentId,
            Status = attendance.Status,
            CheckedInAt = attendance.CheckedInAt,
            RecordedBy = attendance.RecordedBy,
            CreatedAt = attendance.CreatedAt,
            UpdatedAt = attendance.UpdatedAt,
        };
    }

    public async Task<Pagination<SessionAttendanceResponseDto>> GetSessionAttendancesByClassSessionIdAsync(
        Guid classSessionId,
        string? sortBy,
        bool isDescending,
        int page,
        int pageSize,
        AttendanceStatus? status = null)
    {
        _logger.LogInformation(
            "[GetSessionAttendancesByClassSessionIdAsync] Start — classSessionId: {ClassSessionId}, page: {Page}, pageSize: {PageSize}",
            classSessionId,
            page,
            pageSize);

        SessionAttendanceValidator.ValidatePagination(page, pageSize);

        var classSession = await _unitOfWork.ClassSessions.GetByIdAsync(classSessionId);
        ClassSessionValidator.ValidateClassSessionExists(classSession, classSessionId);

        var currentUser = await SessionAttendanceValidator.EnsureCanViewSessionRosterAsync(
            _unitOfWork,
            _claimsService,
            classSession!);

        var query = _unitOfWork.SessionAttendances
            .GetQueryable()
            .Where(sa => sa.ClassSessionId == classSessionId && !sa.IsDeleted);

        if (currentUser.Role == RoleType.Student)
        {
            query = query.Where(sa => sa.StudentId == currentUser.Id);
        }

        if (status.HasValue)
        {
            query = query.Where(sa => sa.Status == status.Value);
        }

        query = sortBy?.ToLower() switch
        {
            "status" => isDescending ? query.OrderByDescending(sa => sa.Status) : query.OrderBy(sa => sa.Status),
            "checkedinat" => isDescending ? query.OrderByDescending(sa => sa.CheckedInAt) : query.OrderBy(sa => sa.CheckedInAt),
            "studentid" => isDescending ? query.OrderByDescending(sa => sa.StudentId) : query.OrderBy(sa => sa.StudentId),
            "createdat" => isDescending ? query.OrderByDescending(sa => sa.CreatedAt) : query.OrderBy(sa => sa.CreatedAt),
            _ => isDescending ? query.OrderByDescending(sa => sa.CreatedAt) : query.OrderBy(sa => sa.CreatedAt),
        };

        var totalCount = query.Count();

        var items = query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        var dtos = items.Select(sa => new SessionAttendanceResponseDto
        {
            Id = sa.Id,
            ClassSessionId = sa.ClassSessionId,
            StudentId = sa.StudentId,
            ModuleEnrollmentId = sa.ModuleEnrollmentId,
            Status = sa.Status,
            CheckedInAt = sa.CheckedInAt,
            RecordedBy = sa.RecordedBy,
            CreatedAt = sa.CreatedAt,
            UpdatedAt = sa.UpdatedAt,
        }).ToList();

        return new Pagination<SessionAttendanceResponseDto>(dtos, totalCount, page, pageSize);
    }

    public async Task<SessionAttendanceResponseDto> UpdateSessionAttendanceAsync(
        Guid id,
        UpdateSessionAttendanceRequestDto request)
    {
        _logger.LogInformation("[UpdateSessionAttendanceAsync] Start — id: {Id}", id);

        SessionAttendanceValidator.ValidateUpdateRequest(request);

        var attendance = await _unitOfWork.SessionAttendances.GetByIdAsync(id);
        SessionAttendanceValidator.ValidateSessionAttendanceExists(attendance, id);

        var classSession = await _unitOfWork.ClassSessions.GetByIdAsync(attendance!.ClassSessionId);
        ClassSessionValidator.ValidateClassSessionExists(classSession, attendance.ClassSessionId);

        var currentUser = await SessionAttendanceValidator.EnsureCanUpdateSessionAttendanceAsync(
            _unitOfWork,
            _claimsService,
            attendance,
            classSession!);

        attendance.Status = request.Status;
        attendance.CheckedInAt = request.CheckedInAt
            ?? (request.Status is AttendanceStatus.Present or AttendanceStatus.Late
                ? DateTime.UtcNow
                : attendance.CheckedInAt);
        attendance.RecordedBy = currentUser.Id;

        await _unitOfWork.SessionAttendances.Update(attendance);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation(
            "[UpdateSessionAttendanceAsync] Attendance {Id} updated to {Status} by {UserId}.",
            attendance.Id,
            attendance.Status,
            currentUser.Id);

        return new SessionAttendanceResponseDto
        {
            Id = attendance.Id,
            ClassSessionId = attendance.ClassSessionId,
            StudentId = attendance.StudentId,
            ModuleEnrollmentId = attendance.ModuleEnrollmentId,
            Status = attendance.Status,
            CheckedInAt = attendance.CheckedInAt,
            RecordedBy = attendance.RecordedBy,
            CreatedAt = attendance.CreatedAt,
            UpdatedAt = attendance.UpdatedAt,
        };
    }

    public async Task<List<SessionAttendanceResponseDto>> GenerateSessionAttendanceRosterAsync(Guid classSessionId)
    {
        _logger.LogInformation(
            "[GenerateSessionAttendanceRosterAsync] Start — classSessionId: {ClassSessionId}",
            classSessionId);

        var classSession = await _unitOfWork.ClassSessions.GetByIdAsync(classSessionId);
        ClassSessionValidator.ValidateClassSessionExists(classSession, classSessionId);

        await SessionAttendanceValidator.EnsureCanGenerateRosterAsync(
            _unitOfWork,
            _claimsService,
            classSession!);

        if (!classSession!.RequiresAttendance)
        {
            throw ErrorHelper.BadRequest("This class session does not require attendance tracking.");
        }

        var classEnrollments = await _unitOfWork.ClassEnrollments.GetAllAsync(
            ce => ce.ClassId == classSession.ClassId
                  && ce.Status == ClassEnrollmentStatus.Active
                  && !ce.IsDeleted);

        var created = new List<SessionAttendance>();

        foreach (var classEnrollment in classEnrollments)
        {
            var existing = await _unitOfWork.SessionAttendances.FirstOrDefaultAsync(
                sa => sa.ClassSessionId == classSessionId
                      && sa.StudentId == classEnrollment.StudentId
                      && !sa.IsDeleted);

            if (existing != null)
            {
                continue;
            }

            var moduleEnrollment = await _unitOfWork.ModuleEnrollments.FirstOrDefaultAsync(
                me => me.StudentId == classEnrollment.StudentId
                      && me.ModuleId == classSession.ModuleId
                      && me.ProgramEnrollmentId == classEnrollment.ProgramEnrollmentId
                      && me.Status == EnrollmentStatus.Active
                      && !me.IsDeleted);

            if (moduleEnrollment == null)
            {
                _logger.LogWarning(
                    "[GenerateSessionAttendanceRosterAsync] Skipping student {StudentId} — no active module enrollment for module {ModuleId}.",
                    classEnrollment.StudentId,
                    classSession.ModuleId);
                continue;
            }

            var attendance = new SessionAttendance
            {
                ClassSessionId = classSessionId,
                StudentId = classEnrollment.StudentId,
                ModuleEnrollmentId = moduleEnrollment.Id,
                Status = AttendanceStatus.Expected,
            };

            await _unitOfWork.SessionAttendances.AddAsync(attendance);
            created.Add(attendance);
        }

        if (created.Count > 0)
        {
            await _unitOfWork.SaveChangesAsync();
        }

        _logger.LogInformation(
            "[GenerateSessionAttendanceRosterAsync] Created {Count} attendance rows for class session {ClassSessionId}.",
            created.Count,
            classSessionId);

        return created.Select(sa => new SessionAttendanceResponseDto
        {
            Id = sa.Id,
            ClassSessionId = sa.ClassSessionId,
            StudentId = sa.StudentId,
            ModuleEnrollmentId = sa.ModuleEnrollmentId,
            Status = sa.Status,
            CheckedInAt = sa.CheckedInAt,
            RecordedBy = sa.RecordedBy,
            CreatedAt = sa.CreatedAt,
            UpdatedAt = sa.UpdatedAt,
        }).ToList();
    }
}
