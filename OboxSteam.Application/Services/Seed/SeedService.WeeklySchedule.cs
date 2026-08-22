using Microsoft.Extensions.Logging;
using OboxSteam.Application.Validation;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.Services;

public partial class SeedService
{
    private const string ScheduleFixtureStudentCode = "STD-001";
    private const string ScheduleFixtureClassCode = "CLS-ROBOTICS-CURRENT";
    private const string ScheduleFixtureTitlePrefix = "[SCH-WK]";

    /// <summary>
    /// Time/location templates for the current Asia/Ho_Chi_Minh week. Does not introduce
    /// new curriculum links — existing LiveOnline/Offline sessions on the student's class
    /// are moved into these slots so <c>GET /api/schedules/weekly</c> is populated without
    /// violating 1:1 activity sessions or SelfPaced rules.
    /// </summary>
    private sealed record WeeklyScheduleSlot(
        int DaysFromMonday,
        TimeOnly Start,
        string Location,
        string MeetingUrlSuffix);

    private async Task SeedWeeklyScheduleFixtureAsync()
    {
        _loggerService.LogInformation(
            "Ensuring weekly schedule fixture for {StudentCode}",
            ScheduleFixtureStudentCode);

        var student = await _unitOfWork.Users.FirstOrDefaultAsync(
            u => u.Code == ScheduleFixtureStudentCode && !u.IsDeleted);
        if (student == null)
        {
            _loggerService.LogWarning(
                "{StudentCode} not found. Skipping weekly schedule fixture.",
                ScheduleFixtureStudentCode);
            return;
        }

        var classEntity = await ResolveScheduleFixtureClassAsync(student.Id);
        if (classEntity == null)
        {
            _loggerService.LogWarning(
                "No active class found for {StudentCode}. Skipping weekly schedule fixture.",
                ScheduleFixtureStudentCode);
            return;
        }

        // Remove legacy fixture rows that linked SelfPaced activities or duplicated curriculum items.
        await SoftDeleteLegacyWeeklyScheduleFixturesAsync(classEntity.Id);

        var vietnam = ResolveVietnamTimeZone();
        var monday = ResolveCurrentMonday(vietnam);
        var seedTime = DateTime.UtcNow;
        var nowUtc = DateTime.SpecifyKind(seedTime, DateTimeKind.Utc);

        var slots = GetWeeklyScheduleSlots();
        var movableSessions = await LoadMovableCurriculumSessionsAsync(classEntity.Id);
        if (movableSessions.Count == 0)
        {
            _loggerService.LogWarning(
                "Class {ClassCode} has no LiveOnline/Offline sessions to place on the weekly grid.",
                classEntity.Code);
            return;
        }

        var activityIds = movableSessions
            .Where(s => s.ActivityId.HasValue)
            .Select(s => s.ActivityId!.Value)
            .Distinct()
            .ToList();
        var activitiesById = (await _unitOfWork.Activities.GetAllAsync(
                a => activityIds.Contains(a.Id) && !a.IsDeleted))
            .ToDictionary(a => a.Id);

        var count = Math.Min(slots.Count, movableSessions.Count);
        var fixtureSessions = new List<ClassSession>(count);

        for (var i = 0; i < count; i++)
        {
            var slot = slots[i];
            var session = movableSessions[i];
            if (!session.ActivityId.HasValue
                || !activitiesById.TryGetValue(session.ActivityId.Value, out var activity))
            {
                continue;
            }

            if (activity.ActivityType == ActivityType.SelfPaced)
            {
                continue;
            }

            var date = monday.AddDays(slot.DaysFromMonday);
            var startUtc = ToUtc(date, slot.Start, vietnam);
            var durationMinutes = activity.DurationMinutes is > 0
                ? activity.DurationMinutes.Value
                : 120;
            var endUtc = startUtc.AddMinutes(durationMinutes);

            // Keep sessions inside the class window so date-range rules stay valid.
            if (startUtc < classEntity.StartDate || endUtc > classEntity.EndDate)
            {
                _loggerService.LogWarning(
                    "Skipping weekly slot {Index} for class {ClassCode} — outside class date range.",
                    i,
                    classEntity.Code);
                continue;
            }

            var sessionKind = ClassSessionValidator.ResolveSessionKind(activity, forAssignment: false);
            var (fallbackLocation, fallbackMeet) = SeedTimeline.ResolveSeedVenue(
                sessionKind,
                classEntity.Code,
                i);
            session.StartTime = startUtc;
            session.EndTime = endUtc;
            session.Location = slot.Location;
            session.MeetingUrl = sessionKind == SessionKind.Lesson
                ? $"https://meet.oboxsteam.com/{classEntity.Code.ToLowerInvariant()}-{slot.MeetingUrlSuffix}"
                : fallbackMeet;
            if (string.IsNullOrWhiteSpace(session.Location))
            {
                session.Location = fallbackLocation;
            }
            session.SessionKind = sessionKind;
            session.RequiresAttendance = true;
            session.Status = SeedTimeline.ResolveSessionStatus(startUtc, endUtc, nowUtc);
            session.UpdatedAt = seedTime;
            session.UpdatedBy = Guid.Empty;

            await _unitOfWork.ClassSessions.Update(session);
            fixtureSessions.Add(session);
        }

        if (fixtureSessions.Count > 0)
        {
            await _unitOfWork.SaveChangesAsync();
        }

        await SeedWeeklyScheduleAttendanceAsync(student.Id, fixtureSessions, nowUtc, seedTime);

        _loggerService.LogInformation(
            "Weekly schedule fixture ready — class {ClassCode}, week {WeekStart}, moved {Count} curriculum session(s). Login STD-001 then GET /api/schedules/weekly.",
            classEntity.Code,
            monday,
            fixtureSessions.Count);
    }

    private async Task SoftDeleteLegacyWeeklyScheduleFixturesAsync(Guid classId)
    {
        var legacy = await _unitOfWork.ClassSessions.GetAllAsync(
            cs => cs.ClassId == classId
                  && cs.Title.StartsWith(ScheduleFixtureTitlePrefix)
                  && !cs.IsDeleted);

        if (legacy.Count == 0)
        {
            return;
        }

        foreach (var session in legacy)
        {
            session.IsDeleted = true;
            session.UpdatedAt = DateTime.UtcNow;
            session.UpdatedBy = Guid.Empty;
            await _unitOfWork.ClassSessions.Update(session);
        }

        await _unitOfWork.SaveChangesAsync();
        _loggerService.LogInformation(
            "Soft-deleted {Count} legacy [SCH-WK] fixture session(s) on class {ClassId}.",
            legacy.Count,
            classId);
    }

    private async Task<List<ClassSession>> LoadMovableCurriculumSessionsAsync(Guid classId)
    {
        var sessions = await _unitOfWork.ClassSessions.GetAllAsync(
            cs => cs.ClassId == classId
                  && !cs.IsDeleted
                  && cs.Status != ClassSessionStatus.Cancelled
                  && cs.ActivityId != null
                  && !cs.Title.StartsWith(ScheduleFixtureTitlePrefix));

        if (sessions.Count == 0)
        {
            return [];
        }

        var activityIds = sessions.Select(s => s.ActivityId!.Value).Distinct().ToList();
        var schedulableActivityIds = (await _unitOfWork.Activities.GetAllAsync(
                a => activityIds.Contains(a.Id)
                     && !a.IsDeleted
                     && (a.ActivityType == ActivityType.LiveOnline
                         || a.ActivityType == ActivityType.Offline)))
            .Select(a => a.Id)
            .ToHashSet();

        return sessions
            .Where(s => schedulableActivityIds.Contains(s.ActivityId!.Value))
            .GroupBy(s => s.ActivityId!.Value)
            .Select(g => g.OrderBy(s => s.StartTime).First())
            .OrderBy(s => s.StartTime)
            .ThenBy(s => s.Title)
            .ToList();
    }

    private async Task<Class?> ResolveScheduleFixtureClassAsync(Guid studentId)
    {
        var enrollments = await _unitOfWork.ClassEnrollments.GetAllAsync(
            ce => ce.StudentId == studentId
                  && ce.Status == ClassEnrollmentStatus.Active
                  && !ce.IsDeleted);

        if (enrollments.Count == 0)
        {
            return null;
        }

        var classIds = enrollments.Select(ce => ce.ClassId).Distinct().ToList();
        var classes = await _unitOfWork.Classes.GetAllAsync(
            c => classIds.Contains(c.Id) && !c.IsDeleted);

        return classes.FirstOrDefault(c => c.Code == ScheduleFixtureClassCode)
               ?? classes.FirstOrDefault(c => c.Status == ClassStatus.InProgress)
               ?? classes.FirstOrDefault();
    }

    private async Task SeedWeeklyScheduleAttendanceAsync(
        Guid studentId,
        List<ClassSession> sessions,
        DateTime nowUtc,
        DateTime seedTime)
    {
        if (sessions.Count == 0)
        {
            return;
        }

        var moduleIds = sessions.Select(s => s.ModuleId).Distinct().ToList();
        var moduleEnrollments = await _unitOfWork.ModuleEnrollments.GetAllAsync(
            me => me.StudentId == studentId
                  && moduleIds.Contains(me.ModuleId)
                  && !me.IsDeleted);
        var moduleEnrollmentByModuleId = moduleEnrollments
            .GroupBy(me => me.ModuleId)
            .ToDictionary(g => g.Key, g => g.First());

        var sessionIds = sessions.Select(s => s.Id).ToList();
        var existingAttendance = (await _unitOfWork.SessionAttendances.GetAllAsync(
                sa => sa.StudentId == studentId
                      && sessionIds.Contains(sa.ClassSessionId)
                      && !sa.IsDeleted))
            .ToDictionary(sa => sa.ClassSessionId);

        var attendancesToAdd = new List<SessionAttendance>();
        var index = 0;
        foreach (var session in sessions)
        {
            if (!moduleEnrollmentByModuleId.TryGetValue(session.ModuleId, out var moduleEnrollment))
            {
                index++;
                continue;
            }

            var status = session.EndTime <= nowUtc
                ? index % 3 == 0 ? AttendanceStatus.Late : AttendanceStatus.Present
                : AttendanceStatus.Expected;

            if (!existingAttendance.TryGetValue(session.Id, out var attendance))
            {
                attendancesToAdd.Add(new SessionAttendance
                {
                    Id = Guid.NewGuid(),
                    ClassSessionId = session.Id,
                    StudentId = studentId,
                    ModuleEnrollmentId = moduleEnrollment.Id,
                    Status = status,
                    CheckedInAt = status is AttendanceStatus.Present or AttendanceStatus.Late
                        ? session.StartTime.AddMinutes(10)
                        : null,
                    CreatedAt = seedTime,
                    CreatedBy = Guid.Empty,
                    IsDeleted = false,
                });
            }
            else
            {
                attendance.Status = status;
                attendance.CheckedInAt = status is AttendanceStatus.Present or AttendanceStatus.Late
                    ? session.StartTime.AddMinutes(10)
                    : null;
                attendance.UpdatedAt = seedTime;
                await _unitOfWork.SessionAttendances.Update(attendance);
            }

            index++;
        }

        if (attendancesToAdd.Count > 0)
        {
            await _unitOfWork.SessionAttendances.AddRangeAsync(attendancesToAdd);
        }

        if (attendancesToAdd.Count > 0 || existingAttendance.Count > 0)
        {
            await _unitOfWork.SaveChangesAsync();
        }
    }

    /// <summary>
    /// Exactly two weekly slots — mirrors Robotics Current (Tue/Thu morning) and the
    /// product rule of 2 buổi/tuần per program.
    /// </summary>
    private static IReadOnlyList<WeeklyScheduleSlot> GetWeeklyScheduleSlots() =>
    [
        new(1, new TimeOnly(9, 0), "NVH 602", "tue-am"),
        new(3, new TimeOnly(9, 0), "P.012", "thu-am"),
    ];

    private static DateOnly ResolveCurrentMonday(TimeZoneInfo vietnam)
    {
        var nowVietnam = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, vietnam);
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
}
