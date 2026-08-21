using Microsoft.Extensions.Logging;
using OboxSteam.Application.Validation;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.Services;

public partial class SeedService
{
    private const string ScheduleFixtureStudentCode = "STD-001";
    private const string ScheduleFixtureClassCode = "CLS-OPEN-001";
    private const string ScheduleFixtureTitlePrefix = "[SCH-WK]";

    private sealed record WeeklyScheduleSlot(
        int DaysFromMonday,
        TimeOnly Start,
        TimeOnly End,
        string Location,
        string ModuleCode,
        string ActivityCode,
        string Title,
        SessionKind SessionKind,
        bool AttachSelfPacedMaterial);

    /// <summary>
    /// Places a current Asia/Ho_Chi_Minh week of sessions on STD-001's open robotics class
    /// so <c>GET /api/schedules/weekly</c> has a populated timetable. Idempotent: titles
    /// with <see cref="ScheduleFixtureTitlePrefix"/> are moved to the current week on re-seed.
    /// </summary>
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

        var vietnam = ResolveVietnamTimeZone();
        var monday = ResolveCurrentMonday(vietnam);
        var seedTime = DateTime.UtcNow;
        var nowUtc = DateTime.SpecifyKind(seedTime, DateTimeKind.Utc);

        var modulesByCode = (await _unitOfWork.Modules.GetAllAsync(m => !m.IsDeleted))
            .ToDictionary(m => m.Code, m => m, StringComparer.OrdinalIgnoreCase);
        var activitiesByCode = (await _unitOfWork.Activities.GetAllAsync(a => !a.IsDeleted))
            .ToDictionary(a => a.Code, a => a, StringComparer.OrdinalIgnoreCase);
        var selfPacedMaterialActivity = await ResolveSelfPacedMaterialActivityAsync(activitiesByCode);

        var existing = (await _unitOfWork.ClassSessions.GetAllAsync(
                cs => cs.ClassId == classEntity.Id
                      && cs.Title.StartsWith(ScheduleFixtureTitlePrefix)
                      && !cs.IsDeleted))
            .ToDictionary(cs => cs.Title, cs => cs, StringComparer.OrdinalIgnoreCase);

        var sessionsToAdd = new List<ClassSession>();
        var sessionsToUpdate = new List<ClassSession>();
        var fixtureSessions = new List<ClassSession>();

        foreach (var slot in GetWeeklyScheduleSlots())
        {
            if (!modulesByCode.TryGetValue(slot.ModuleCode, out var module))
            {
                _loggerService.LogWarning(
                    "Module {ModuleCode} not found for weekly schedule slot {Title}. Skipping.",
                    slot.ModuleCode,
                    slot.Title);
                continue;
            }

            var activityCode = slot.AttachSelfPacedMaterial && selfPacedMaterialActivity != null
                ? selfPacedMaterialActivity.Code
                : slot.ActivityCode;
            if (!activitiesByCode.TryGetValue(activityCode, out var activity))
            {
                _loggerService.LogWarning(
                    "Activity {ActivityCode} not found for weekly schedule slot {Title}. Skipping.",
                    activityCode,
                    slot.Title);
                continue;
            }

            var date = monday.AddDays(slot.DaysFromMonday);
            var startUtc = ToUtc(date, slot.Start, vietnam);
            var endUtc = ToUtc(date, slot.End, vietnam);
            var title = $"{ScheduleFixtureTitlePrefix} {slot.Title}";
            var status = endUtc <= nowUtc
                ? ClassSessionStatus.Completed
                : startUtc <= nowUtc
                    ? ClassSessionStatus.InProgress
                    : ClassSessionStatus.Scheduled;

            if (!existing.TryGetValue(title, out var session))
            {
                session = new ClassSession
                {
                    Id = Guid.NewGuid(),
                    ClassId = classEntity.Id,
                    ModuleId = module.Id,
                    ActivityId = activity.Id,
                    SessionKind = slot.SessionKind,
                    Title = title,
                    Description = "Weekly timetable fixture for STD-001.",
                    StartTime = startUtc,
                    EndTime = endUtc,
                    Location = slot.Location,
                    MeetingUrl = slot.SessionKind == SessionKind.Lesson
                        ? "https://meet.oboxsteam.com/cls-open-001-weekly"
                        : null,
                    RequiresAttendance = true,
                    Status = status,
                    CreatedAt = seedTime,
                    CreatedBy = Guid.Empty,
                    IsDeleted = false,
                };
                sessionsToAdd.Add(session);
            }
            else
            {
                session.ModuleId = module.Id;
                session.ActivityId = activity.Id;
                session.SessionKind = slot.SessionKind;
                session.StartTime = startUtc;
                session.EndTime = endUtc;
                session.Location = slot.Location;
                session.MeetingUrl = slot.SessionKind == SessionKind.Lesson
                    ? "https://meet.oboxsteam.com/cls-open-001-weekly"
                    : null;
                session.Status = status;
                session.UpdatedAt = seedTime;
                sessionsToUpdate.Add(session);
            }

            fixtureSessions.Add(session);
        }

        if (sessionsToAdd.Count > 0)
        {
            await _unitOfWork.ClassSessions.AddRangeAsync(sessionsToAdd);
        }

        foreach (var session in sessionsToUpdate)
        {
            await _unitOfWork.ClassSessions.Update(session);
        }

        if (sessionsToAdd.Count > 0 || sessionsToUpdate.Count > 0)
        {
            await _unitOfWork.SaveChangesAsync();
        }

        await SeedWeeklyScheduleAttendanceAsync(student.Id, fixtureSessions, nowUtc, seedTime);

        _loggerService.LogInformation(
            "Weekly schedule fixture ready — class {ClassCode}, week {WeekStart}, added {Added}, updated {Updated}. Login STD-001 then GET /api/schedules/weekly.",
            classEntity.Code,
            monday,
            sessionsToAdd.Count,
            sessionsToUpdate.Count);
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
               ?? classes.FirstOrDefault();
    }

    private async Task<Activity?> ResolveSelfPacedMaterialActivityAsync(
        IReadOnlyDictionary<string, Activity> activitiesByCode)
    {
        if (!activitiesByCode.TryGetValue("ACT-ROBOTICS-01-01", out var activity))
        {
            return null;
        }

        var material = await _unitOfWork.Materials.FirstOrDefaultAsync(
            m => m.ActivityId == activity.Id && !m.IsDeleted);
        return material == null ? null : activity;
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

    private static IReadOnlyList<WeeklyScheduleSlot> GetWeeklyScheduleSlots() =>
    [
        new(0, new TimeOnly(7, 30), new TimeOnly(9, 45), "NVH 602",
            "MOD-ROBOTICS-01", "ACT-ROBOTICS-01-02", "Introduction to Robotics",
            SessionKind.Lesson, AttachSelfPacedMaterial: true),
        new(0, new TimeOnly(15, 0), new TimeOnly(17, 15), "P.012",
            "MOD-ROBOTICS-01", "ACT-ROBOTICS-02-02", "Actuator Design Lecture",
            SessionKind.Lesson, AttachSelfPacedMaterial: false),
        new(1, new TimeOnly(12, 30), new TimeOnly(14, 40), "NVH 307",
            "MOD-ROBOTICS-02", "ACT-ROBOTICS-04-02", "Field Trip Preparation Briefing",
            SessionKind.Lesson, AttachSelfPacedMaterial: true),
        new(2, new TimeOnly(7, 30), new TimeOnly(9, 45), "NVH 602",
            "MOD-ROBOTICS-01", "ACT-ROBOTICS-01-03", "Components Overview Workshop",
            SessionKind.Lesson, AttachSelfPacedMaterial: false),
        new(3, new TimeOnly(15, 0), new TimeOnly(17, 15), "P.012",
            "MOD-ROBOTICS-01", "ACT-ROBOTICS-03-02", "Lab Safety Briefing",
            SessionKind.Lesson, AttachSelfPacedMaterial: false),
        new(4, new TimeOnly(12, 30), new TimeOnly(14, 40), "NVH 306",
            "MOD-ROBOTICS-02", "ACT-ROBOTICS-05-02", "Movement Trip Preparation",
            SessionKind.Lesson, AttachSelfPacedMaterial: false),
        new(6, new TimeOnly(17, 45), new TimeOnly(20, 0), "P.134",
            "MOD-ROBOTICS-02", "ACT-ROBOTICS-04-03", "Sensor Exploration Field Trip",
            SessionKind.FieldTrip, AttachSelfPacedMaterial: false),
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
