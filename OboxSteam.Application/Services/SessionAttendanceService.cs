using Microsoft.Extensions.Logging;
using OboxSteam.Application.Commons;
using OboxSteam.Application.DTOs.SessionAttendanceDTO;
using OboxSteam.Application.Interfaces;
using OboxSteam.Application.Notifications;
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
    private readonly ICurrentTime _currentTime;
    private readonly ILogger<SessionAttendanceService> _logger;
    private readonly INotificationPublisher _notificationPublisher;

    public SessionAttendanceService(
        IUnitOfWork unitOfWork,
        IClaimsService claimsService,
        ICurrentTime currentTime,
        ILogger<SessionAttendanceService> logger,
        INotificationPublisher notificationPublisher)
    {
        _unitOfWork = unitOfWork;
        _claimsService = claimsService;
        _currentTime = currentTime;
        _logger = logger;
        _notificationPublisher = notificationPublisher;
    }

    public async Task<Pagination<SessionAttendanceResponseDto>> GetSessionAttendancesByClassSessionIdAsync(
        Guid classSessionId,
        string? sortBy,
        bool isDescending,
        int page,
        int pageSize,
        AttendanceStatus? status = null,
        Guid? studentId = null)
    {
        _logger.LogInformation(
            "[GetSessionAttendancesByClassSessionIdAsync] Start — classSessionId: {ClassSessionId}, page: {Page}, pageSize: {PageSize}",
            classSessionId,
            page,
            pageSize);

        ClassSessionValidator.ValidatePagination(page, pageSize);

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
        else if (studentId.HasValue)
        {
            query = query.Where(sa => sa.StudentId == studentId.Value);
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
        Guid classId,
        Guid sessionId,
        Guid studentId,
        UpdateSessionAttendanceRequestDto request)
    {
        _logger.LogInformation(
            "[UpdateSessionAttendanceAsync] Start — classId: {ClassId}, sessionId: {SessionId}, studentId: {StudentId}",
            classId,
            sessionId,
            studentId);

        SessionAttendanceValidator.ValidateUpdateRequest(request);

        var classSession = await _unitOfWork.ClassSessions.GetByIdAsync(sessionId);
        ClassSessionValidator.ValidateClassSessionExists(classSession, sessionId);

        if (classSession!.ClassId != classId)
        {
            throw ErrorHelper.NotFound($"Class session with ID '{sessionId}' not found.");
        }

        if (!classSession.RequiresAttendance)
        {
            throw ErrorHelper.BadRequest("This class session does not require attendance tracking.");
        }

        var currentUser = await SessionAttendanceValidator.EnsureCanUpdateSessionAttendanceAsync(
            _unitOfWork,
            _claimsService,
            classSession);

        var now = _currentTime.GetCurrentTime();
        if (currentUser.Role == RoleType.Mentor && now > classSession.EndTime)
        {
            throw ErrorHelper.Forbidden("Mentors can only update attendance while the class session is ongoing.");
        }

        var classEnrollments = await _unitOfWork.ClassEnrollments.GetAllAsync(
            ce => ce.ClassId == classId
                  && ce.StudentId == studentId
                  && ce.Status == ClassEnrollmentStatus.Active
                  && !ce.IsDeleted);

        if (classEnrollments.Count == 0)
        {
            throw ErrorHelper.BadRequest("Student is not enrolled in this class.");
        }

        var classEnrollment = classEnrollments.First();

        var moduleEnrollment = await _unitOfWork.ModuleEnrollments.FirstOrDefaultAsync(
            me => me.StudentId == studentId
                  && me.ModuleId == classSession.ModuleId
                  && me.ProgramEnrollmentId == classEnrollment.ProgramEnrollmentId
                  && me.Status == EnrollmentStatus.Active
                  && !me.IsDeleted);

        if (moduleEnrollment == null)
        {
            throw ErrorHelper.BadRequest("Student does not have an active module enrollment for this session.");
        }

        var attendance = await _unitOfWork.SessionAttendances.FirstOrDefaultAsync(
            sa => sa.ClassSessionId == sessionId
                  && sa.StudentId == studentId
                  && !sa.IsDeleted);

        var isNewAttendance = attendance == null;
        if (isNewAttendance)
        {
            attendance = new SessionAttendance
            {
                ClassSessionId = sessionId,
                StudentId = studentId,
            };
        }

        attendance!.ModuleEnrollmentId = moduleEnrollment.Id;
        attendance.Status = request.Status;
        attendance.CheckedInAt = now;
        attendance.RecordedBy = currentUser.Id;

        if (isNewAttendance)
        {
            await _unitOfWork.SessionAttendances.AddAsync(attendance);
        }
        else
        {
            await _unitOfWork.SessionAttendances.Update(attendance);
        }

        await _unitOfWork.SaveChangesAsync();

        await _notificationPublisher.PublishAsync(
            NotificationCatalog.AttendanceMarked(
                attendance.Status,
                studentId,
                sessionId,
                classId,
                currentUser.Id));

        if (request.Status == AttendanceStatus.Absent)
        {
            await TryFailModuleForExcessAbsencesAsync(moduleEnrollment);
        }

        _logger.LogInformation(
            "[UpdateSessionAttendanceAsync] Attendance updated — sessionId: {SessionId}, studentId: {StudentId}, status: {Status}, by: {UserId}.",
            sessionId,
            studentId,
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

    private async Task TryFailModuleForExcessAbsencesAsync(ModuleEnrollment moduleEnrollment)
    {
        if (moduleEnrollment.Status != EnrollmentStatus.Active)
        {
            return;
        }

        var missed = await ModuleAbsencePolicy.CountMissedAsync(_unitOfWork, moduleEnrollment.Id);
        var total = await ModuleAbsencePolicy.CountSessionActivitiesAsync(
            _unitOfWork,
            moduleEnrollment.ModuleId);

        if (!ModuleAbsencePolicy.ShouldFail(missed, total))
        {
            return;
        }

        moduleEnrollment.Status = EnrollmentStatus.Failed;
        await _unitOfWork.ModuleEnrollments.Update(moduleEnrollment);
        await _unitOfWork.SaveChangesAsync();

        var module = await _unitOfWork.Modules.GetByIdAsync(moduleEnrollment.ModuleId);

        await _notificationPublisher.PublishAsync(
            NotificationCatalog.ModuleFailed(
                moduleEnrollment.StudentId,
                moduleEnrollment.ModuleId,
                moduleEnrollment.Id,
                module?.ProgramId,
                module?.Name));

        _logger.LogWarning(
            "[TryFailModuleForExcessAbsencesAsync] Module enrollment {EnrollmentId} failed — student {StudentId} missed {Missed}/{Total} session activities (>= {Threshold}%).",
            moduleEnrollment.Id,
            moduleEnrollment.StudentId,
            missed,
            total,
            ModuleAbsencePolicy.MaxAbsencePercent);
    }
}
