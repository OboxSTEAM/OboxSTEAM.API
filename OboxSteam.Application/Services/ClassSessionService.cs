using Microsoft.Extensions.Logging;
using OboxSteam.Application.Commons;
using OboxSteam.Application.DTOs.ClassSessionDTO;
using OboxSteam.Application.Interfaces;
using OboxSteam.Application.Notifications;
using OboxSteam.Application.Utils;
using OboxSteam.Application.Validation;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;
using OboxSteam.Domain.Interfaces;

namespace OboxSteam.Application.Services;

public sealed class ClassSessionService : IClassSessionService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClaimsService _claimsService;
    private readonly ILogger<ClassSessionService> _logger;
    private readonly INotificationPublisher _notificationPublisher;

    public ClassSessionService(
        IUnitOfWork unitOfWork,
        IClaimsService claimsService,
        ILogger<ClassSessionService> logger,
        INotificationPublisher notificationPublisher)
    {
        _unitOfWork = unitOfWork;
        _claimsService = claimsService;
        _logger = logger;
        _notificationPublisher = notificationPublisher;
    }

    public async Task<Pagination<ClassSessionResponseDto>> GetClassSessionsByClassIdAsync(
        Guid classId,
        string? sortBy,
        bool isDescending,
        int page,
        int pageSize,
        Guid? moduleId = null,
        SessionKind? sessionKind = null,
        ClassSessionStatus? status = null,
        DateTime? from = null,
        DateTime? to = null)
    {
        _logger.LogInformation(
            "[GetClassSessionsByClassIdAsync] Start — classId: {ClassId}, page: {Page}, pageSize: {PageSize}",
            classId,
            page,
            pageSize);

        ClassSessionValidator.ValidatePagination(page, pageSize);

        var classEntity = await _unitOfWork.Classes.GetByIdAsync(classId);
        ClassValidator.ValidateClassExists(classEntity, classId);

        var query = _unitOfWork.ClassSessions
            .GetQueryable()
            .Where(cs => cs.ClassId == classId && !cs.IsDeleted);

        if (moduleId.HasValue)
        {
            query = query.Where(cs => cs.ModuleId == moduleId.Value);
        }

        if (sessionKind.HasValue)
        {
            query = query.Where(cs => cs.SessionKind == sessionKind.Value);
        }

        if (status.HasValue)
        {
            query = query.Where(cs => cs.Status == status.Value);
        }

        if (from.HasValue)
        {
            query = query.Where(cs => cs.EndTime >= from.Value);
        }

        if (to.HasValue)
        {
            query = query.Where(cs => cs.StartTime <= to.Value);
        }

        query = sortBy?.ToLower() switch
        {
            "title" => isDescending ? query.OrderByDescending(cs => cs.Title) : query.OrderBy(cs => cs.Title),
            "starttime" => isDescending ? query.OrderByDescending(cs => cs.StartTime) : query.OrderBy(cs => cs.StartTime),
            "endtime" => isDescending ? query.OrderByDescending(cs => cs.EndTime) : query.OrderBy(cs => cs.EndTime),
            "sessionkind" => isDescending ? query.OrderByDescending(cs => cs.SessionKind) : query.OrderBy(cs => cs.SessionKind),
            "status" => isDescending ? query.OrderByDescending(cs => cs.Status) : query.OrderBy(cs => cs.Status),
            "createdat" => isDescending ? query.OrderByDescending(cs => cs.CreatedAt) : query.OrderBy(cs => cs.CreatedAt),
            _ => isDescending ? query.OrderByDescending(cs => cs.StartTime) : query.OrderBy(cs => cs.StartTime),
        };

        var totalCount = query.Count();

        var items = query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        var dtos = items.Select(cs => new ClassSessionResponseDto
        {
            Id = cs.Id,
            ClassId = cs.ClassId,
            ModuleId = cs.ModuleId,
            ActivityId = cs.ActivityId,
            AssignmentId = cs.AssignmentId,
            SessionKind = cs.SessionKind,
            Title = cs.Title,
            Description = cs.Description,
            StartTime = cs.StartTime,
            EndTime = cs.EndTime,
            Location = cs.Location,
            RequiresAttendance = cs.RequiresAttendance,
            RequiresMentorCheckIn = cs.RequiresMentorCheckIn,
            Status = cs.Status,
            CreatedAt = cs.CreatedAt,
            UpdatedAt = cs.UpdatedAt,
        }).ToList();

        _logger.LogInformation(
            "[GetClassSessionsByClassIdAsync] Retrieved {Count}/{Total} sessions for class {ClassId}.",
            dtos.Count,
            totalCount,
            classId);

        return new Pagination<ClassSessionResponseDto>(dtos, totalCount, page, pageSize);
    }

    public async Task<ClassSessionResponseDto> GetClassSessionByIdAsync(Guid id)
    {
        _logger.LogInformation("[GetClassSessionByIdAsync] Fetching class session with Id: {Id}", id);

        var entity = await _unitOfWork.ClassSessions.GetByIdAsync(id);
        ClassSessionValidator.ValidateClassSessionExists(entity, id);

        _logger.LogInformation("[GetClassSessionByIdAsync] Class session with Id {Id} retrieved successfully.", id);

        return new ClassSessionResponseDto
        {
            Id = entity!.Id,
            ClassId = entity.ClassId,
            ModuleId = entity.ModuleId,
            ActivityId = entity.ActivityId,
            AssignmentId = entity.AssignmentId,
            SessionKind = entity.SessionKind,
            Title = entity.Title,
            Description = entity.Description,
            StartTime = entity.StartTime,
            EndTime = entity.EndTime,
            Location = entity.Location,
            RequiresAttendance = entity.RequiresAttendance,
            RequiresMentorCheckIn = entity.RequiresMentorCheckIn,
            Status = entity.Status,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt,
        };
    }

    public async Task<ClassSessionWithStudentsResponseDto> GetClassSessionWithStudentsAsync(Guid id)
    {
        _logger.LogInformation("[GetClassSessionWithStudentsAsync] Fetching class session roster for Id: {Id}", id);

        var entity = await _unitOfWork.ClassSessions.GetByIdAsync(id);
        ClassSessionValidator.ValidateClassSessionExists(entity, id);

        var session = entity!;

        var currentUser = await SessionAttendanceValidator.EnsureCanViewSessionRosterAsync(
            _unitOfWork,
            _claimsService,
            session);

        var classEnrollments = await _unitOfWork.ClassEnrollments.GetAllAsync(
            ce => ce.ClassId == session.ClassId
                  && ce.Status == ClassEnrollmentStatus.Active
                  && !ce.IsDeleted);

        if (currentUser.Role == RoleType.Student)
        {
            classEnrollments = classEnrollments
                .Where(ce => ce.StudentId == currentUser.Id)
                .ToList();
        }

        var attendances = await _unitOfWork.SessionAttendances.GetAllAsync(
            sa => sa.ClassSessionId == id && !sa.IsDeleted);
        var attendanceByStudentId = attendances.ToDictionary(sa => sa.StudentId);

        var studentIds = classEnrollments.Select(ce => ce.StudentId).Distinct().ToList();
        var students = studentIds.Any()
            ? await _unitOfWork.Users.GetAllAsync(u => studentIds.Contains(u.Id) && !u.IsDeleted)
            : new List<User>();

        var studentsById = students.ToDictionary(u => u.Id);

        var programEnrollmentIds = classEnrollments.Select(ce => ce.ProgramEnrollmentId).Distinct().ToList();
        var moduleEnrollments = studentIds.Any()
            ? await _unitOfWork.ModuleEnrollments.GetAllAsync(
                me => studentIds.Contains(me.StudentId)
                      && me.ModuleId == session.ModuleId
                      && me.ProgramEnrollmentId.HasValue
                      && programEnrollmentIds.Contains(me.ProgramEnrollmentId.Value)
                      && me.Status == EnrollmentStatus.Active
                      && !me.IsDeleted)
            : new List<ModuleEnrollment>();

        var moduleEnrollmentByStudentAndProgram = moduleEnrollments
            .Where(me => me.ProgramEnrollmentId.HasValue)
            .ToDictionary(me => (me.StudentId, me.ProgramEnrollmentId!.Value));

        var studentDtos = classEnrollments
            .Where(ce => studentsById.ContainsKey(ce.StudentId))
            .OrderBy(ce => studentsById[ce.StudentId].FullName)
            .ThenBy(ce => studentsById[ce.StudentId].Code)
            .Select(ce =>
            {
                var student = studentsById[ce.StudentId];
                attendanceByStudentId.TryGetValue(ce.StudentId, out var attendance);

                var moduleEnrollmentId = attendance?.ModuleEnrollmentId
                    ?? (moduleEnrollmentByStudentAndProgram.TryGetValue(
                            (ce.StudentId, ce.ProgramEnrollmentId),
                            out var moduleEnrollment)
                        ? moduleEnrollment.Id
                        : Guid.Empty);

                return new ClassSessionStudentResponseDto
                {
                    ClassSessionId = session.Id,
                    StudentId = student.Id,
                    StudentCode = student.Code,
                    StudentName = student.FullName,
                    Email = student.Email,
                    Phone = student.Phone,
                    AvatarUrl = student.AvatarUrl,
                    ModuleEnrollmentId = moduleEnrollmentId,
                    AttendanceStatus = attendance?.Status ?? AttendanceStatus.Expected,
                    CheckedInAt = attendance?.CheckedInAt,
                    RecordedBy = attendance?.RecordedBy,
                };
            })
            .ToList();

        _logger.LogInformation(
            "[GetClassSessionWithStudentsAsync] Class session {Id} roster retrieved — {StudentCount} student(s).",
            id,
            studentDtos.Count);

        return new ClassSessionWithStudentsResponseDto
        {
            Id = session.Id,
            ClassId = session.ClassId,
            ModuleId = session.ModuleId,
            ActivityId = session.ActivityId,
            AssignmentId = session.AssignmentId,
            SessionKind = session.SessionKind,
            Title = session.Title,
            Description = session.Description,
            StartTime = session.StartTime,
            EndTime = session.EndTime,
            Location = session.Location,
            RequiresAttendance = session.RequiresAttendance,
            RequiresMentorCheckIn = session.RequiresMentorCheckIn,
            Status = session.Status,
            CreatedAt = session.CreatedAt,
            UpdatedAt = session.UpdatedAt,
            Students = studentDtos,
        };
    }

    public async Task<ClassSessionResponseDto> CreateClassSessionAsync(CreateClassSessionRequestDto request)
    {
        _logger.LogInformation(
            "[CreateClassSessionAsync] Start creating session '{Title}' for class {ClassId}",
            request.Title,
            request.ClassId);

        ClassSessionValidator.ValidateCreateRequest(request);

        var classEntity = await _unitOfWork.Classes.GetByIdAsync(request.ClassId);
        ClassValidator.ValidateClassExists(classEntity, request.ClassId);
        ClassSessionValidator.ValidateClassSchedulable(classEntity!);
        ClassSessionValidator.ValidateSessionWithinClassDateRange(
            classEntity!,
            request.StartTime,
            request.EndTime);

        await ClassSessionValidator.ValidateReferencesAsync(
            _unitOfWork,
            classEntity!,
            request.ModuleId,
            request.ActivityId,
            request.AssignmentId);

        if (classEntity!.MentorId is null)
        {
            throw ErrorHelper.BadRequest(
                "Cannot schedule a session for a class that has no assigned mentor.");
        }

        await MentorScopeValidator.ValidateMentorSessionNoOverlapAsync(
            _unitOfWork,
            classEntity.MentorId.Value,
            request.StartTime,
            request.EndTime);

        var entity = new ClassSession
        {
            ClassId = request.ClassId,
            ModuleId = request.ModuleId,
            ActivityId = request.ActivityId,
            AssignmentId = request.AssignmentId,
            SessionKind = request.SessionKind,
            Title = request.Title.Trim(),
            Description = request.Description?.Trim(),
            StartTime = request.StartTime,
            EndTime = request.EndTime,
            Location = request.Location?.Trim(),
            RequiresAttendance = request.RequiresAttendance,
            RequiresMentorCheckIn = request.RequiresMentorCheckIn,
            Status = ClassSessionStatus.Scheduled,
        };

        await _unitOfWork.ClassSessions.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync();

        await _notificationPublisher.PublishAsync(
            NotificationCatalog.ClassSessionScheduled(entity.ClassId, entity.Id, classEntity!.ProgramId));

        _logger.LogInformation(
            "[CreateClassSessionAsync] Class session '{Title}' created with Id {Id}.",
            entity.Title,
            entity.Id);

        return new ClassSessionResponseDto
        {
            Id = entity.Id,
            ClassId = entity.ClassId,
            ModuleId = entity.ModuleId,
            ActivityId = entity.ActivityId,
            AssignmentId = entity.AssignmentId,
            SessionKind = entity.SessionKind,
            Title = entity.Title,
            Description = entity.Description,
            StartTime = entity.StartTime,
            EndTime = entity.EndTime,
            Location = entity.Location,
            RequiresAttendance = entity.RequiresAttendance,
            RequiresMentorCheckIn = entity.RequiresMentorCheckIn,
            Status = entity.Status,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt,
        };
    }

    public async Task<ClassSessionResponseDto> UpdateClassSessionAsync(
        Guid id,
        UpdateClassSessionRequestDto request)
    {
        _logger.LogInformation("[UpdateClassSessionAsync] Attempting to update class session with Id: {Id}", id);

        var entity = await _unitOfWork.ClassSessions.GetByIdAsync(id);
        ClassSessionValidator.ValidateClassSessionExists(entity, id);
        var session = entity!;
        ClassSessionValidator.ValidateSessionModifiable(session);

        var classEntity = await _unitOfWork.Classes.GetByIdAsync(session.ClassId);
        ClassValidator.ValidateClassExists(classEntity, session.ClassId);

        var originalStatus = session.Status;
        var timeChanged = request.StartTime.HasValue || request.EndTime.HasValue;

        var targetModuleId = session.ModuleId;
        var targetActivityId = session.ActivityId;
        var targetAssignmentId = session.AssignmentId;
        var targetStartTime = session.StartTime;
        var targetEndTime = session.EndTime;

        if (request.ModuleId.HasValue)
        {
            targetModuleId = request.ModuleId.Value;
            session.ModuleId = request.ModuleId.Value;
        }

        if (request.ActivityId.HasValue)
        {
            targetActivityId = request.ActivityId;
            session.ActivityId = request.ActivityId;
        }

        if (request.AssignmentId.HasValue)
        {
            targetAssignmentId = request.AssignmentId;
            session.AssignmentId = request.AssignmentId;
        }

        ClassSessionValidator.ValidateActivityOrAssignmentRequired(targetActivityId, targetAssignmentId);

        await ClassSessionValidator.ValidateReferencesAsync(
            _unitOfWork,
            classEntity!,
            targetModuleId,
            targetActivityId,
            targetAssignmentId);

        if (request.SessionKind.HasValue)
        {
            session.SessionKind = request.SessionKind.Value;
        }

        if (!string.IsNullOrWhiteSpace(request.Title))
        {
            session.Title = request.Title.Trim();
        }

        if (request.Description != null)
        {
            session.Description = string.IsNullOrWhiteSpace(request.Description)
                ? null
                : request.Description.Trim();
        }

        if (request.StartTime.HasValue)
        {
            targetStartTime = request.StartTime.Value;
            session.StartTime = request.StartTime.Value;
        }

        if (request.EndTime.HasValue)
        {
            targetEndTime = request.EndTime.Value;
            session.EndTime = request.EndTime.Value;
        }

        if (request.StartTime.HasValue || request.EndTime.HasValue)
        {
            if (targetEndTime <= targetStartTime)
            {
                throw ErrorHelper.BadRequest("EndTime must be after StartTime.");
            }

            ClassSessionValidator.ValidateSessionWithinClassDateRange(
                classEntity!,
                targetStartTime,
                targetEndTime);

            if (classEntity!.MentorId is null)
            {
                throw ErrorHelper.BadRequest(
                    "Cannot reschedule a session for a class that has no assigned mentor.");
            }

            await MentorScopeValidator.ValidateMentorSessionNoOverlapAsync(
                _unitOfWork,
                classEntity.MentorId.Value,
                targetStartTime,
                targetEndTime,
                excludeSessionId: session.Id);
        }

        if (request.Location != null)
        {
            session.Location = string.IsNullOrWhiteSpace(request.Location)
                ? null
                : request.Location.Trim();
        }

        if (request.RequiresAttendance.HasValue)
        {
            session.RequiresAttendance = request.RequiresAttendance.Value;
        }

        if (request.RequiresMentorCheckIn.HasValue)
        {
            session.RequiresMentorCheckIn = request.RequiresMentorCheckIn.Value;
        }

        if (request.Status.HasValue)
        {
            ClassSessionValidator.ValidateStatusTransition(session.Status, request.Status.Value);
            session.Status = request.Status.Value;
        }

        await _unitOfWork.ClassSessions.Update(session);
        await _unitOfWork.SaveChangesAsync();

        var sessionNotifications = new List<NotificationCommand>();

        if (request.Status.HasValue && session.Status != originalStatus)
        {
            switch (session.Status)
            {
                case ClassSessionStatus.InProgress:
                    sessionNotifications.Add(
                        NotificationCatalog.ClassSessionStarted(session.ClassId, session.Id, classEntity!.ProgramId));
                    break;
                case ClassSessionStatus.Completed:
                    sessionNotifications.Add(
                        NotificationCatalog.ClassSessionCompleted(session.ClassId, session.Id, classEntity!.ProgramId));
                    break;
                case ClassSessionStatus.Cancelled:
                    sessionNotifications.Add(
                        NotificationCatalog.ClassSessionCancelled(session.ClassId, session.Id, classEntity!.ProgramId));
                    break;
                default:
                    sessionNotifications.Add(
                        NotificationCatalog.ClassSessionRescheduled(session.ClassId, session.Id, classEntity!.ProgramId));
                    break;
            }
        }
        else if (timeChanged)
        {
            sessionNotifications.Add(
                NotificationCatalog.ClassSessionRescheduled(session.ClassId, session.Id, classEntity!.ProgramId));
        }

        if (sessionNotifications.Count > 0)
        {
            await _notificationPublisher.PublishManyAsync(sessionNotifications);
        }

        _logger.LogInformation("[UpdateClassSessionAsync] Class session Id {Id} updated successfully.", id);

        return new ClassSessionResponseDto
        {
            Id = session.Id,
            ClassId = session.ClassId,
            ModuleId = session.ModuleId,
            ActivityId = session.ActivityId,
            AssignmentId = session.AssignmentId,
            SessionKind = session.SessionKind,
            Title = session.Title,
            Description = session.Description,
            StartTime = session.StartTime,
            EndTime = session.EndTime,
            Location = session.Location,
            RequiresAttendance = session.RequiresAttendance,
            RequiresMentorCheckIn = session.RequiresMentorCheckIn,
            Status = session.Status,
            CreatedAt = session.CreatedAt,
            UpdatedAt = session.UpdatedAt,
        };
    }

    public async Task<bool> DeleteClassSessionAsync(Guid id)
    {
        _logger.LogInformation("[DeleteClassSessionAsync] Attempting to soft-delete class session Id: {Id}", id);

        var entity = await _unitOfWork.ClassSessions.GetByIdAsync(id);

        if (entity == null || entity.IsDeleted)
        {
            _logger.LogWarning("[DeleteClassSessionAsync] Class session with Id {Id} not found.", id);
            return false;
        }

        var classId = entity.ClassId;
        var sessionId = entity.Id;

        await _unitOfWork.ClassSessions.SoftRemove(entity);
        await _unitOfWork.SaveChangesAsync();

        var classEntity = await _unitOfWork.Classes.GetByIdAsync(classId);
        await _notificationPublisher.PublishAsync(
            NotificationCatalog.ClassSessionCancelled(classId, sessionId, classEntity?.ProgramId));

        _logger.LogInformation("[DeleteClassSessionAsync] Class session Id {Id} soft-deleted successfully.", id);

        return true;
    }
}
