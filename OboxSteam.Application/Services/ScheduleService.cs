using Microsoft.Extensions.Logging;
using OboxSteam.Application.DTOs.ScheduleDTO;
using OboxSteam.Application.Interfaces;
using OboxSteam.Application.Validation;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;
using OboxSteam.Domain.Interfaces;

namespace OboxSteam.Application.Services;

public sealed class ScheduleService : IScheduleService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClaimsService _claimsService;
    private readonly ICurrentTime _currentTime;
    private readonly ILogger<ScheduleService> _logger;

    public ScheduleService(
        IUnitOfWork unitOfWork,
        IClaimsService claimsService,
        ICurrentTime currentTime,
        ILogger<ScheduleService> logger)
    {
        _unitOfWork = unitOfWork;
        _claimsService = claimsService;
        _currentTime = currentTime;
        _logger = logger;
    }

    public async Task<WeeklyScheduleResponseDto> GetWeeklyScheduleAsync(
        DateOnly? weekStart = null,
        Guid? studentId = null)
    {
        var student = await ScheduleValidator.ResolveScheduleOwnerAsync(
            _unitOfWork,
            _claimsService,
            studentId);

        var vietnam = ResolveVietnamTimeZone();
        var monday = ResolveWeekStart(weekStart, vietnam);
        var sunday = monday.AddDays(6);
        var weekStartUtc = ToUtc(monday, TimeOnly.MinValue, vietnam);
        var weekEndExclusiveUtc = ToUtc(monday.AddDays(7), TimeOnly.MinValue, vietnam);

        var enrollments = await _unitOfWork.ClassEnrollments.GetAllAsync(
            ce => ce.StudentId == student.Id
                  && ce.Status == ClassEnrollmentStatus.Active
                  && !ce.IsDeleted);

        var classIds = enrollments.Select(ce => ce.ClassId).Distinct().ToList();
        var sessions = classIds.Count == 0
            ? []
            : await _unitOfWork.ClassSessions.GetAllAsync(
                cs => classIds.Contains(cs.ClassId)
                      && cs.Status != ClassSessionStatus.Cancelled
                      && !cs.IsDeleted
                      && cs.StartTime >= weekStartUtc
                      && cs.StartTime < weekEndExclusiveUtc);

        var classesById = await LoadClassesByIdAsync(classIds);
        var attendanceBySessionId = await LoadAttendanceBySessionIdAsync(student.Id, sessions);

        var sessionsByDate = new Dictionary<DateOnly, List<ScheduleSessionResponseDto>>();
        foreach (var session in sessions.OrderBy(s => s.StartTime))
        {
            var localDate = DateOnly.FromDateTime(
                TimeZoneInfo.ConvertTimeFromUtc(AsUtc(session.StartTime), vietnam));

            classesById.TryGetValue(session.ClassId, out var classEntity);
            attendanceBySessionId.TryGetValue(session.Id, out var attendance);

            var attendanceStatus = attendance?.Status;
            if (!sessionsByDate.TryGetValue(localDate, out var daySessions))
            {
                daySessions = [];
                sessionsByDate[localDate] = daySessions;
            }

            daySessions.Add(new ScheduleSessionResponseDto
            {
                Id = session.Id,
                ClassId = session.ClassId,
                ClassCode = classEntity?.Code ?? string.Empty,
                ClassName = classEntity?.Name ?? string.Empty,
                ProgramId = classEntity?.ProgramId ?? Guid.Empty,
                MentorId = classEntity?.MentorId,
                ModuleId = session.ModuleId,
                ActivityId = session.ActivityId,
                SessionKind = session.SessionKind,
                StartTime = session.StartTime,
                EndTime = session.EndTime,
                Location = session.Location,
                MeetingUrl = session.MeetingUrl,
                Status = session.Status,
                IsCompleted = session.Status == ClassSessionStatus.Completed,
                AttendanceStatus = attendanceStatus,
            });
        }

        var days = new List<ScheduleDayResponseDto>(7);
        for (var i = 0; i < 7; i++)
        {
            var date = monday.AddDays(i);
            days.Add(new ScheduleDayResponseDto
            {
                Date = date,
                DayOfWeek = date.DayOfWeek,
                Sessions = sessionsByDate.TryGetValue(date, out var daySessions)
                    ? daySessions
                    : [],
            });
        }

        _logger.LogInformation(
            "[GetWeeklyScheduleAsync] Student {StudentId} week {WeekStart} has {SessionCount} session(s).",
            student.Id,
            monday,
            sessions.Count);

        return new WeeklyScheduleResponseDto
        {
            StudentId = student.Id,
            WeekStart = monday,
            WeekEnd = sunday,
            Timezone = ScheduleValidator.TimezoneId,
            Days = days,
        };
    }

    private async Task<Dictionary<Guid, Class>> LoadClassesByIdAsync(List<Guid> classIds)
    {
        if (classIds.Count == 0)
        {
            return [];
        }

        var classes = await _unitOfWork.Classes.GetAllAsync(c => classIds.Contains(c.Id) && !c.IsDeleted);
        return classes.ToDictionary(c => c.Id);
    }

    private async Task<Dictionary<Guid, SessionAttendance>> LoadAttendanceBySessionIdAsync(
        Guid studentId,
        List<ClassSession> sessions)
    {
        if (sessions.Count == 0)
        {
            return [];
        }

        var sessionIds = sessions.Select(s => s.Id).ToList();
        var rows = await _unitOfWork.SessionAttendances.GetAllAsync(
            sa => sa.StudentId == studentId
                  && sessionIds.Contains(sa.ClassSessionId)
                  && !sa.IsDeleted);

        return rows
            .GroupBy(sa => sa.ClassSessionId)
            .ToDictionary(g => g.Key, g => g.First());
    }

    private DateOnly ResolveWeekStart(DateOnly? weekStart, TimeZoneInfo vietnam)
    {
        if (weekStart.HasValue)
        {
            ScheduleValidator.ValidateWeekStartIsMonday(weekStart.Value);
            return weekStart.Value;
        }

        var nowUtc = AsUtc(_currentTime.GetCurrentTime());
        var nowVietnam = TimeZoneInfo.ConvertTimeFromUtc(nowUtc, vietnam);
        var today = DateOnly.FromDateTime(nowVietnam);
        var daysFromMonday = ((int)today.DayOfWeek + 6) % 7;
        return today.AddDays(-daysFromMonday);
    }

    private static TimeZoneInfo ResolveVietnamTimeZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(ScheduleValidator.TimezoneId);
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById(ScheduleValidator.WindowsTimezoneId);
        }
        catch (InvalidTimeZoneException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById(ScheduleValidator.WindowsTimezoneId);
        }
    }

    private static DateTime ToUtc(DateOnly date, TimeOnly time, TimeZoneInfo vietnam)
    {
        var unspecified = DateTime.SpecifyKind(date.ToDateTime(time), DateTimeKind.Unspecified);
        return TimeZoneInfo.ConvertTimeToUtc(unspecified, vietnam);
    }

    private static DateTime AsUtc(DateTime value)
        => value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc),
        };
}
