namespace OboxSteam.Application.DTOs.ScheduleDTO;

/// <summary>One local calendar day in the weekly schedule grid.</summary>
public sealed class StudentScheduleDayDto
{
    /// <summary>Calendar date in Asia/Ho_Chi_Minh.</summary>
    public DateOnly Date { get; set; }

    public DayOfWeek DayOfWeek { get; set; }

    /// <summary>Sessions whose local start date matches <see cref="Date"/>, ordered by start time.</summary>
    public List<StudentScheduleSessionDto> Sessions { get; set; } = new();
}
