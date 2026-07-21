using OboxSteam.Application.DTOs.SkillDTO;
using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.DTOs.ClassMentorRequestDTO;

public class ClassMentorBoardItemDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = null!;
    public string Name { get; set; } = null!;
    public Guid ProgramId { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public int MaxCapacity { get; set; }
    public ClassStatus Status { get; set; }
    public string? ScheduleSummary { get; set; }
    public List<SkillSummaryDto> RequiredSkills { get; set; } = new();
    public bool MatchesMySkills { get; set; }
    public bool HasPendingRequestFromMe { get; set; }
    public int PendingRequestCount { get; set; }
}
