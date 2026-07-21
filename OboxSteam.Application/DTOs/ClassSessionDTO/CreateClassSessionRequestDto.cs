using System.ComponentModel.DataAnnotations;
using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.DTOs.ClassSessionDTO;

/// <summary>
/// Request to schedule a cohort session.
/// At least one of <c>ActivityId</c> or <c>AssignmentId</c> must be set (enforced in the service layer).
/// </summary>
public class CreateClassSessionRequestDto
{
    [Required(ErrorMessage = "ClassId is required.")]
    public Guid ClassId { get; set; }

    [Required(ErrorMessage = "ModuleId is required.")]
    public Guid ModuleId { get; set; }

    public Guid? ActivityId { get; set; }

    public Guid? AssignmentId { get; set; }

    public SessionKind SessionKind { get; set; }

    [Required(ErrorMessage = "Title is required.")]
    [MaxLength(255)]
    public string Title { get; set; } = null!;

    public string? Description { get; set; }

    [Required(ErrorMessage = "StartTime is required.")]
    public DateTime StartTime { get; set; }

    [Required(ErrorMessage = "EndTime is required.")]
    public DateTime EndTime { get; set; }

    [MaxLength(500)]
    public string? Location { get; set; }

    public int? MaxCapacity { get; set; }

    public bool RequiresAttendance { get; set; } = true;

    public bool RequiresMentorCheckIn { get; set; }
}
