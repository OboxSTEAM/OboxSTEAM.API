using OboxSteam.Domain.Entities;

namespace OboxSteam.Application.Utils;

/// <summary>
/// Shared rules for LiveOnline participation duration on <see cref="SessionAttendance"/>.
/// </summary>
public static class SessionParticipationHelper
{
    /// <summary>
    /// Closes an open join segment. No-op when there is no check-in or leave was already recorded.
    /// </summary>
    public static void CloseOpenSegment(SessionAttendance attendance, DateTime sessionEndTime, DateTime now)
    {
        if (attendance.CheckedInAt == null || attendance.LeftAt != null)
        {
            return;
        }

        var closedAt = now < sessionEndTime ? now : sessionEndTime;
        var checkedInAt = attendance.CheckedInAt.Value;
        attendance.LeftAt = closedAt;
        attendance.ParticipationMinutes = Math.Max(
            0,
            (int)Math.Floor((closedAt - checkedInAt).TotalMinutes));
    }

    /// <summary>
    /// Closes every open join segment on a session roster. Returns how many rows were updated.
    /// </summary>
    public static int CloseOpenSegments(
        IEnumerable<SessionAttendance> attendances,
        DateTime sessionEndTime,
        DateTime now)
    {
        var closed = 0;
        foreach (var attendance in attendances)
        {
            if (attendance.CheckedInAt == null || attendance.LeftAt != null)
            {
                continue;
            }

            CloseOpenSegment(attendance, sessionEndTime, now);
            closed++;
        }

        return closed;
    }
}
