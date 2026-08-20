namespace OboxSteam.Application.DTOs.ScheduleDTO;

/// <summary>
/// One calendar week of the current student's class sessions, grouped by local date.
/// </summary>
public sealed class StudentWeeklyScheduleResponseDto
{
    /// <summary>Monday of the requested week in Asia/Ho_Chi_Minh (yyyy-MM-dd).</summary>
    public DateOnly WeekStart { get; set; }

    /// <summary>Sunday of the requested week in Asia/Ho_Chi_Minh (yyyy-MM-dd).</summary>
    public DateOnly WeekEnd { get; set; }

    public string Timezone { get; set; } = "Asia/Ho_Chi_Minh";

    /// <summary>Always seven days, Monday through Sunday. Days with no sessions have an empty list.</summary>
    public List<StudentScheduleDayDto> Days { get; set; } = new();
}
