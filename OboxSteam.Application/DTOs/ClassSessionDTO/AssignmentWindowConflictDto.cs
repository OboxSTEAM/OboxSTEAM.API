namespace OboxSteam.Application.DTOs.ClassSessionDTO;

/// <summary>
/// Returned in <c>value.data</c> of a 409 ASSIGNMENT_WINDOW_NOT_OPEN / ASSIGNMENT_WINDOW_CLOSED
/// so the client can show when the class window opens or closed (UTC).
/// </summary>
public sealed class AssignmentWindowConflictDto
{
    public Guid ClassSessionId { get; set; }
    public Guid ClassId { get; set; }
    public Guid AssignmentId { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
}
