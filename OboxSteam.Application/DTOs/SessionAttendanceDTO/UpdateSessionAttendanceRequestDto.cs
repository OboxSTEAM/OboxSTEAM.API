using System.ComponentModel.DataAnnotations;
using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.DTOs.SessionAttendanceDTO;

/// <summary>
/// Request to record or update attendance for a roster entry.
/// Only Mentor, Manager, and Admin may submit this request.
/// <c>RecordedBy</c> is set from the authenticated user in the service layer.
/// </summary>
public class UpdateSessionAttendanceRequestDto
{
    [Required(ErrorMessage = "Status is required.")]
    public AttendanceStatus Status { get; set; }
}
