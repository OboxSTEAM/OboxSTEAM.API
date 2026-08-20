using System.ComponentModel.DataAnnotations;

namespace OboxSteam.Application.DTOs.ClassSessionDTO;

/// <summary>
/// Weekly repeat pattern used to bulk-generate class sessions from the program curriculum.
/// LiveOnline/Offline activities (ordered by module, course, then ActivityOrder) and assignments
/// are placed into consecutive weekly slots starting from the class start date.
/// Times are interpreted as UTC, consistent with the rest of the scheduling API.
/// </summary>
public class GenerateClassSessionsRequestDto
{
    /// <summary>Weekdays on which sessions repeat, e.g. [Saturday, Sunday].</summary>
    [Required(ErrorMessage = "DaysOfWeek is required.")]
    [MinLength(1, ErrorMessage = "At least one day of week is required.")]
    public List<DayOfWeek> DaysOfWeek { get; set; } = new();

    /// <summary>Time of day each session starts (UTC).</summary>
    [Required(ErrorMessage = "SessionStartTime is required.")]
    public TimeOnly SessionStartTime { get; set; }

    /// <summary>Time of day each session ends (UTC). Must be after <see cref="SessionStartTime"/>.</summary>
    [Required(ErrorMessage = "SessionEndTime is required.")]
    public TimeOnly SessionEndTime { get; set; }
}
