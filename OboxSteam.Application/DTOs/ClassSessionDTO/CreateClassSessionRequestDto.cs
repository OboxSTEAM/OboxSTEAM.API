using System.ComponentModel.DataAnnotations;
using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.DTOs.ClassSessionDTO;

/// <summary>
/// Request to schedule a cohort session.
/// Exactly one of <c>ActivityId</c> or <c>AssignmentId</c> must be set (enforced in the service layer).
/// For activity sessions, <see cref="EndTime"/> is ignored — the server derives it from
/// <c>StartTime + Activity.DurationMinutes</c>. Assignment sessions require both times.
/// </summary>
public class CreateClassSessionRequestDto
{
    [Required(ErrorMessage = "ClassId is required.")]
    public Guid ClassId { get; set; }

    [Required(ErrorMessage = "ModuleId is required.")]
    public Guid ModuleId { get; set; }

    public Guid? ActivityId { get; set; }

    public Guid? AssignmentId { get; set; }

    /// <summary>
    /// Ignored — derived by the server from ActivityType / Assignment
    /// (LiveOnline → LiveOnline, Offline → Offline, Assignment → AssignmentWindow).
    /// Sending a value returns 400.
    /// </summary>
    public SessionKind? SessionKind { get; set; }

    [Required(ErrorMessage = "Title is required.")]
    [MaxLength(255)]
    public string Title { get; set; } = null!;

    public string? Description { get; set; }

    [Required(ErrorMessage = "StartTime is required.")]
    public DateTime StartTime { get; set; }

    /// <summary>
    /// Required for assignment sessions only. Ignored when <see cref="ActivityId"/> is set —
    /// end is derived from the activity's DurationMinutes.
    /// </summary>
    public DateTime? EndTime { get; set; }

    [MaxLength(500)]
    public string? Location { get; set; }

    [MaxLength(2048)]
    public string? MeetingUrl { get; set; }

    /// <summary>Offline venue latitude. Must be provided together with <see cref="Longitude"/>.</summary>
    [Range(-90, 90, ErrorMessage = "Latitude must be between -90 and 90.")]
    public double? Latitude { get; set; }

    /// <summary>Offline venue longitude. Must be provided together with <see cref="Latitude"/>.</summary>
    [Range(-180, 180, ErrorMessage = "Longitude must be between -180 and 180.")]
    public double? Longitude { get; set; }

    public bool RequiresAttendance { get; set; } = true;

    public bool RequiresMentorCheckIn { get; set; }
}
