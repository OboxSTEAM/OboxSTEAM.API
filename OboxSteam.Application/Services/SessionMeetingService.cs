using Microsoft.Extensions.Logging;
using OboxSteam.Application.DTOs.ClassSessionDTO;
using OboxSteam.Application.Interfaces;
using OboxSteam.Application.Notifications;
using OboxSteam.Application.Utils;
using OboxSteam.Application.Validation;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;
using OboxSteam.Domain.Interfaces;

namespace OboxSteam.Application.Services;

public sealed class SessionMeetingService : ISessionMeetingService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClaimsService _claimsService;
    private readonly ICurrentTime _currentTime;
    private readonly IJaasJwtService _jaasJwtService;
    private readonly INotificationPublisher _notificationPublisher;
    private readonly ILogger<SessionMeetingService> _logger;

    public SessionMeetingService(
        IUnitOfWork unitOfWork,
        IClaimsService claimsService,
        ICurrentTime currentTime,
        IJaasJwtService jaasJwtService,
        INotificationPublisher notificationPublisher,
        ILogger<SessionMeetingService> logger)
    {
        _unitOfWork = unitOfWork;
        _claimsService = claimsService;
        _currentTime = currentTime;
        _jaasJwtService = jaasJwtService;
        _notificationPublisher = notificationPublisher;
        _logger = logger;
    }

    public async Task<ClassSessionJoinResponseDto> JoinAsync(Guid classSessionId)
    {
        _logger.LogInformation("[JoinAsync] Start — classSessionId: {ClassSessionId}", classSessionId);

        var currentUser = await SessionAttendanceValidator.GetCurrentUserAsync(_unitOfWork, _claimsService);
        var classSession = await _unitOfWork.ClassSessions.GetByIdAsync(classSessionId);
        ClassSessionValidator.ValidateClassSessionExists(classSession, classSessionId);

        ClassSessionJoinValidator.ValidateLiveOnline(classSession!);
        var now = _currentTime.GetCurrentTime();
        ClassSessionJoinValidator.ValidateJoinWindow(classSession!, now);

        var isModerator = await ResolveIsModeratorAsync(currentUser, classSession!);
        string? attendanceStatus = null;

        if (currentUser.Role == RoleType.Student)
        {
            attendanceStatus = (await RecordStudentJoinAttendanceAsync(currentUser, classSession!, now))
                .ToString();
        }
        else if (!isModerator)
        {
            throw ErrorHelper.Forbidden(
                "Only enrolled students or the class mentor (or Manager/Admin) can join this meeting.");
        }

        var displayName = string.IsNullOrWhiteSpace(currentUser.FullName)
            ? currentUser.Email
            : currentUser.FullName;

        var roomName = classSession!.Id.ToString();
        var jwt = _jaasJwtService.CreateMeetingToken(
            roomName,
            currentUser.Id,
            displayName ?? currentUser.Code,
            currentUser.Email,
            isModerator,
            now);

        return new ClassSessionJoinResponseDto
        {
            ClassSessionId = classSession.Id,
            Jwt = jwt,
            RoomName = roomName,
            AppId = _jaasJwtService.AppId,
            Domain = _jaasJwtService.Domain,
            IsModerator = isModerator,
            AttendanceStatus = attendanceStatus,
        };
    }

    public async Task<ClassSessionLeaveResponseDto> LeaveAsync(Guid classSessionId)
    {
        _logger.LogInformation("[LeaveAsync] Start — classSessionId: {ClassSessionId}", classSessionId);

        var currentUser = await SessionAttendanceValidator.GetCurrentUserAsync(_unitOfWork, _claimsService);
        var classSession = await _unitOfWork.ClassSessions.GetByIdAsync(classSessionId);
        ClassSessionValidator.ValidateClassSessionExists(classSession, classSessionId);
        ClassSessionJoinValidator.ValidateLiveOnline(classSession!);

        if (currentUser.Role != RoleType.Student)
        {
            return new ClassSessionLeaveResponseDto
            {
                ClassSessionId = classSessionId,
            };
        }

        var attendance = await _unitOfWork.SessionAttendances.FirstOrDefaultAsync(
            sa => sa.ClassSessionId == classSessionId
                  && sa.StudentId == currentUser.Id
                  && !sa.IsDeleted);

        if (attendance == null)
        {
            throw ErrorHelper.BadRequest("You have not joined this meeting yet.");
        }

        if (attendance.LeftAt != null)
        {
            return new ClassSessionLeaveResponseDto
            {
                ClassSessionId = classSessionId,
                AttendanceId = attendance.Id,
                CheckedInAt = attendance.CheckedInAt,
                LeftAt = attendance.LeftAt,
                ParticipationMinutes = attendance.ParticipationMinutes,
            };
        }

        var now = _currentTime.GetCurrentTime();
        SessionParticipationHelper.CloseOpenSegment(attendance, classSession!.EndTime, now);

        await _unitOfWork.SessionAttendances.Update(attendance);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation(
            "[LeaveAsync] Student {StudentId} left session {SessionId}; participation={Minutes} min.",
            currentUser.Id,
            classSessionId,
            attendance.ParticipationMinutes);

        return new ClassSessionLeaveResponseDto
        {
            ClassSessionId = classSessionId,
            AttendanceId = attendance.Id,
            CheckedInAt = attendance.CheckedInAt,
            LeftAt = attendance.LeftAt,
            ParticipationMinutes = attendance.ParticipationMinutes,
        };
    }

    private async Task<bool> ResolveIsModeratorAsync(User currentUser, ClassSession classSession)
    {
        if (currentUser.Role is RoleType.Admin or RoleType.Manager)
            return true;

        if (currentUser.Role != RoleType.Mentor)
            return false;

        var classEntity = await _unitOfWork.Classes.GetByIdAsync(classSession.ClassId);
        return classEntity != null
               && !classEntity.IsDeleted
               && classEntity.MentorId == currentUser.Id;
    }

    private async Task<AttendanceStatus> RecordStudentJoinAttendanceAsync(
        User student,
        ClassSession classSession,
        DateTime now)
    {
        var classEnrollment = await _unitOfWork.ClassEnrollments.FirstOrDefaultAsync(
            ce => ce.ClassId == classSession.ClassId
                  && ce.StudentId == student.Id
                  && ce.Status == ClassEnrollmentStatus.Active
                  && !ce.IsDeleted);

        if (classEnrollment == null)
            throw ErrorHelper.BadRequest("You are not enrolled in this class.");

        var moduleEnrollment = await _unitOfWork.ModuleEnrollments.FirstOrDefaultAsync(
            me => me.StudentId == student.Id
                  && me.ModuleId == classSession.ModuleId
                  && me.ProgramEnrollmentId == classEnrollment.ProgramEnrollmentId
                  && me.Status == EnrollmentStatus.Active
                  && !me.IsDeleted);

        if (moduleEnrollment == null)
            throw ErrorHelper.BadRequest("You do not have an active module enrollment for this session.");

        var attendance = await _unitOfWork.SessionAttendances.FirstOrDefaultAsync(
            sa => sa.ClassSessionId == classSession.Id
                  && sa.StudentId == student.Id
                  && !sa.IsDeleted);

        var isNew = attendance == null;
        var isFirstSelfJoin = isNew
            || attendance!.CheckedInAt == null
            || attendance.RecordedBy != student.Id;

        if (isNew)
        {
            attendance = new SessionAttendance
            {
                ClassSessionId = classSession.Id,
                StudentId = student.Id,
            };
        }

        // Idempotent: keep original CheckedInAt / status on re-join.
        if (isFirstSelfJoin)
        {
            attendance!.ModuleEnrollmentId = moduleEnrollment.Id;
            attendance.Status = ClassSessionJoinValidator.ResolveJoinAttendanceStatus(classSession, now);
            attendance.CheckedInAt = now;
            attendance.RecordedBy = student.Id;
            attendance.LeftAt = null;
            attendance.ParticipationMinutes = null;
        }
        else if (attendance!.LeftAt != null)
        {
            // Re-join after leaving re-opens the segment: the first CheckedInAt
            // stands and the final /leave writes the closing values.
            attendance.LeftAt = null;
            attendance.ParticipationMinutes = null;
        }

        if (isNew)
            await _unitOfWork.SessionAttendances.AddAsync(attendance!);
        else
            await _unitOfWork.SessionAttendances.Update(attendance!);

        await _unitOfWork.SaveChangesAsync();

        if (isFirstSelfJoin)
        {
            var classEntity = await _unitOfWork.Classes.GetByIdAsync(classSession.ClassId);
            await _notificationPublisher.PublishAsync(
                NotificationCatalog.AttendanceCheckedIn(
                    student.Id,
                    classSession.Id,
                    AppDateTime.FormatVietnamClock(now),
                    classSession.ClassId,
                    student.Id,
                    classEntity?.ProgramId,
                    classEnrollment.ProgramEnrollmentId,
                    classSession.ActivityId,
                    studentName: student.FullName,
                    actorName: student.FullName,
                    className: classEntity?.Name));
        }

        return attendance!.Status;
    }
}
