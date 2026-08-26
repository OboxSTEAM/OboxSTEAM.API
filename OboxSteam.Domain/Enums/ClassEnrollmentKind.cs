namespace OboxSteam.Domain.Enums;

/// <summary>
/// Seat kind on a class. Primary counts toward student load; Retake is a
/// parallel remedial-class enrollment that does not replace the source class.
/// </summary>
public enum ClassEnrollmentKind
{
    Primary,
    Retake
}
