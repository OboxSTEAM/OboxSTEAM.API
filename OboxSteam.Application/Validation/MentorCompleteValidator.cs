using OboxSteam.Application.DTOs.ActivityProgressDTO;
using OboxSteam.Application.Utils;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.Validation;

/// <summary>
/// Mentor bulk-complete request and attendance-gate rules.
/// </summary>
public static class MentorCompleteValidator
{
    public const string AttendanceRequiredMessage =
        "Student must be Present, Late, or Excused for this session before mentor completion.";

    public const string SessionActivityMismatchMessage =
        "Class session is not linked to the requested activity.";

    public const string QrCheckinRequiredMessage =
        "Student has not checked in via QR code for this session.";

    public static void ValidateRequest(MentorCompleteBulkRequestDto request)
    {
        if (request.ClassSessionId == Guid.Empty)
        {
            throw ErrorHelper.BadRequest("ClassSessionId is required.");
        }

        if (request.ActivityId == Guid.Empty)
        {
            throw ErrorHelper.BadRequest("ActivityId is required.");
        }
    }

    public static void ValidateSessionLinkedToActivity(ClassSession classSession, Guid activityId)
    {
        if (classSession.ActivityId != activityId)
        {
            throw ErrorHelper.BadRequest(SessionActivityMismatchMessage);
        }
    }

    /// <summary>
    /// Returns null when attendance allows completion; otherwise a skip reason.
    /// </summary>
    public static string? GetAttendanceSkipReason(SessionAttendance? attendance)
    {
        if (attendance == null || attendance.IsDeleted)
        {
            return AttendanceRequiredMessage;
        }

        if (attendance.Status is AttendanceStatus.Present
            or AttendanceStatus.Late
            or AttendanceStatus.Excused)
        {
            return null;
        }

        return AttendanceRequiredMessage;
    }

    /// <summary>
    /// When the activity requires QR check-in, only students who checked in themselves
    /// (self-recorded attendance with a check-in timestamp) may be completed —
    /// a mentor marking the roster by hand does not count.
    /// Returns null when the requirement is satisfied or not applicable; otherwise a skip reason.
    /// </summary>
    public static string? GetQrCheckinSkipReason(Activity activity, SessionAttendance? attendance)
    {
        if (!activity.RequireQrCheckin)
        {
            return null;
        }

        var hasSelfCheckIn = attendance is { IsDeleted: false, CheckedInAt: not null }
            && attendance.RecordedBy == attendance.StudentId;

        return hasSelfCheckIn ? null : QrCheckinRequiredMessage;
    }
}
