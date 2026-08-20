namespace OboxSteam.Domain.Enums;

/// <summary>
/// Cohort lifecycle. Numeric values are stable for the integer column — do not
/// reorder. Logical flow: Draft → ReadyForMentor → Open → InProgress → Completed.
/// </summary>
public enum ClassStatus
{
    Draft = 0,
    Open = 1,
    InProgress = 2,
    Completed = 3,
    Cancelled = 4,

    /// <summary>
    /// Schedule covers the program curriculum. Mentors may request assignment.
    /// Students cannot enroll until the class is Open.
    /// </summary>
    ReadyForMentor = 5,
}
