using System.ComponentModel.DataAnnotations;

namespace OboxSteam.Application.DTOs.ClassSessionDTO;

/// <summary>
/// Weekly repeat pattern used to bulk-generate class sessions from the program curriculum.
/// LiveOnline/Offline activities (ordered by module, course, then ActivityOrder) and assignments
/// are placed into consecutive weekly slots starting from the class start date.
/// Each activity session ends at <see cref="SessionStartTime"/> + the activity's DurationMinutes;
/// assignment windows use <see cref="SessionEndTime"/> − <see cref="SessionStartTime"/> as their
/// default length (assignments have no activity to carry a duration). Times are UTC.
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

    /// <summary>
    /// Only used to derive the default length of AssignmentWindow sessions
    /// (<see cref="SessionEndTime"/> − <see cref="SessionStartTime"/>). Activity sessions
    /// take their length from the activity's DurationMinutes instead.
    /// </summary>
    [Required(ErrorMessage = "SessionEndTime is required.")]
    public TimeOnly SessionEndTime { get; set; }
}
