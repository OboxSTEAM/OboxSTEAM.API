using OboxSteam.Application.Utils;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.Validation;

/// <summary>LiveOnline join/leave window and attendance grace rules.</summary>
public static class ClassSessionJoinValidator
{
    public const int JoinOpenMinutesBeforeStart = 15;
    public const int LateGraceMinutes = 10;

    public const string NotLiveOnlineMessage =
        "Only LiveOnline sessions support meeting join.";

    public const string JoinWindowClosedMessage =
        "Meeting join is only available from 15 minutes before start until the session ends.";

    public const string SessionClosedMessage =
        "This class session is not open for meeting join.";

    public static void ValidateLiveOnline(ClassSession session)
    {
        if (session.SessionKind != SessionKind.LiveOnline)
            throw ErrorHelper.BadRequest(NotLiveOnlineMessage);
    }

    public static void ValidateJoinWindow(ClassSession session, DateTime utcNow)
    {
        if (session.Status is ClassSessionStatus.Cancelled or ClassSessionStatus.Completed)
            throw ErrorHelper.BadRequest(SessionClosedMessage);

        var openFrom = session.StartTime.AddMinutes(-JoinOpenMinutesBeforeStart);
        if (utcNow < openFrom || utcNow > session.EndTime)
            throw ErrorHelper.BadRequest(JoinWindowClosedMessage);
    }

    /// <summary>
    /// Present when joining at or before start + grace; Late afterward.
    /// Joining in the early-open window (before start) is Present.
    /// </summary>
    public static AttendanceStatus ResolveJoinAttendanceStatus(ClassSession session, DateTime utcNow)
    {
        var lateAfter = session.StartTime.AddMinutes(LateGraceMinutes);
        return utcNow <= lateAfter ? AttendanceStatus.Present : AttendanceStatus.Late;
    }
}
