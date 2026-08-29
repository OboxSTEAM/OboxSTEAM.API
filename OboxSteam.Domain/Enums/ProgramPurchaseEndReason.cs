namespace OboxSteam.Domain.Enums;

/// <summary>
/// Why a program purchase was closed. AcademicFail/Attendance map to
/// EnrollmentStatus.Failed; Withdraw maps to EnrollmentStatus.Dropped.
/// </summary>
public enum ProgramPurchaseEndReason
{
    AcademicFail,
    Withdraw,
    Attendance
}
