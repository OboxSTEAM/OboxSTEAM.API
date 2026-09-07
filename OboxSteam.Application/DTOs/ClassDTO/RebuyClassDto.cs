using OboxSteam.Application.DTOs.ClassRedeliveryDTO;
using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.DTOs.ClassDTO;

/// <summary>Open or InProgress Standard class in the continuity / rebuy picker.</summary>
public sealed class RebuyClassDto
{
    public Guid ClassId { get; set; }

    public string Code { get; set; } = null!;

    public string Name { get; set; } = null!;

    public ClassStatus Status { get; set; }

    public DateTime StartDate { get; set; }

    public DateTime EndDate { get; set; }

    public Guid? MentorId { get; set; }

    public string? MentorName { get; set; }

    public int MaxCapacity { get; set; }

    public int SeatsTaken { get; set; }

    public int SeatsRemaining { get; set; }

    public string? ScheduleSummary { get; set; }

    /// <summary>False when this class has started the stop module or a later module.</summary>
    public bool IsEligible { get; set; }

    public string? IneligibleReason { get; set; }

    public List<RebuyClassModuleProgressDto> Modules { get; set; } = [];

    /// <summary>
    /// Sessions for the stop / focus module (Active redelivery schedule cards). Empty on rebuy.
    /// </summary>
    public List<ClassRedeliveryCandidateSessionDto> ModuleSessions { get; set; } = [];
}
