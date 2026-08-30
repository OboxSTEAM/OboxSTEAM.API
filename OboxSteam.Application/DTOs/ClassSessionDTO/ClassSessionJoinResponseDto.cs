namespace OboxSteam.Application.DTOs.ClassSessionDTO;

/// <summary>Credentials returned by POST /api/class-sessions/{id}/join for the JaaS embed.</summary>
public sealed class ClassSessionJoinResponseDto
{
    public Guid ClassSessionId { get; set; }

    public string Jwt { get; set; } = string.Empty;

    /// <summary>Room name passed to the JaaS SDK (ClassSession GUID).</summary>
    public string RoomName { get; set; } = string.Empty;

    public string AppId { get; set; } = string.Empty;

    public string Domain { get; set; } = "8x8.vc";

    public bool IsModerator { get; set; }

    /// <summary>Attendance status recorded for students; null for mentor/manager join.</summary>
    public string? AttendanceStatus { get; set; }
}
