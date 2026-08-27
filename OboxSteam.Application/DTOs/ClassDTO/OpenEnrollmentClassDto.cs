using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.DTOs.ClassDTO;

/// <summary>
/// Catalog / post-pay picker item: Open Standard class with seats and schedule.
/// </summary>
public sealed class OpenEnrollmentClassSessionDto
{
    public Guid SessionId { get; set; }
    public string Title { get; set; } = null!;
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public SessionKind SessionKind { get; set; }
    public string? Location { get; set; }
}

public sealed class OpenEnrollmentClassDto
{
    public Guid ClassId { get; set; }
    public string Code { get; set; } = null!;
    public string Name { get; set; } = null!;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public Guid? MentorId { get; set; }
    public string? MentorName { get; set; }
    public int MaxCapacity { get; set; }
    public int SeatsTaken { get; set; }
    public int SeatsRemaining { get; set; }
    public string? ScheduleSummary { get; set; }

    /// <summary>
    /// True when this class matches the optional preferredClassId soft preference.
    /// Does not lock a seat.
    /// </summary>
    public bool IsPreferred { get; set; }

    public List<OpenEnrollmentClassSessionDto> Sessions { get; set; } = [];
}
