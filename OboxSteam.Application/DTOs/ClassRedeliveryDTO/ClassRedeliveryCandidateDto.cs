using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.DTOs.ClassRedeliveryDTO;

public sealed class ClassRedeliveryCandidateSessionDto
{
    public Guid SessionId { get; set; }
    public string Title { get; set; } = null!;
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public SessionKind SessionKind { get; set; }
}

public sealed class ClassRedeliveryCandidateDto
{
    public Guid ClassId { get; set; }
    public string Code { get; set; } = null!;
    public string Name { get; set; } = null!;
    public DateTime StartDate { get; set; }
    public Guid? MentorId { get; set; }
    public string? MentorName { get; set; }
    public int MaxCapacity { get; set; }
    public int SeatsTaken { get; set; }
    public int SeatsRemaining { get; set; }
    public List<ClassRedeliveryCandidateSessionDto> ModuleSessions { get; set; } = [];
}
