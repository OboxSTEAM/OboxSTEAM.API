namespace OboxSteam.Application.DTOs.EnrollmentDTO;

using OboxSteam.Domain.Enums;

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

    /// <summary>
    /// Primary cohort is always returned in <see cref="ClassId"/>. When the student also has
    /// an Active or Completed <see cref="ClassEnrollmentKind.Retake"/> seat on this program
    /// enrollment, this is <c>Retake</c> so the client can show the retake badge on the
    /// primary class bar; otherwise it matches the primary seat kind (usually Primary).
    /// </summary>
    public ClassEnrollmentKind Kind { get; set; } = ClassEnrollmentKind.Primary;
}
