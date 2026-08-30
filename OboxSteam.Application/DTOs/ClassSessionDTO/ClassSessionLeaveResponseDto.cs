namespace OboxSteam.Application.DTOs.ClassSessionDTO;

/// <summary>Participation summary returned by POST /api/class-sessions/{id}/leave.</summary>
public sealed class ClassSessionLeaveResponseDto
{
    public Guid ClassSessionId { get; set; }

    public Guid? AttendanceId { get; set; }

    public DateTime? CheckedInAt { get; set; }

    public DateTime? LeftAt { get; set; }

    public int? ParticipationMinutes { get; set; }
}
