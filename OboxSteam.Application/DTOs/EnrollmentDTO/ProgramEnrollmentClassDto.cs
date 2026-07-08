namespace OboxSteam.Application.DTOs.EnrollmentDTO;

/// <summary>
/// Links a program enrollment to the student's chosen class cohort (if any).
/// <see cref="ClassId"/> is null when the student has not joined a class yet.
/// </summary>
public sealed class ProgramEnrollmentClassDto
{
    public Guid ProgramEnrollmentId { get; set; }

    /// <summary>
    /// Active class cohort ID for this program enrollment, or null if not joined.
    /// </summary>
    public Guid? ClassId { get; set; }

    /// <summary>
    /// Active class enrollment ID when <see cref="ClassId"/> is set; otherwise null.
    /// </summary>
    public Guid? ClassEnrollmentId { get; set; }
}
