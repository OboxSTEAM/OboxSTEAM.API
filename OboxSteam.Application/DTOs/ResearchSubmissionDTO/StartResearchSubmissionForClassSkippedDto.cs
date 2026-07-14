namespace OboxSteam.Application.DTOs.ResearchSubmissionDTO;

public class StartResearchSubmissionForClassSkippedDto
{
    public Guid StudentId { get; set; }
    public string Reason { get; set; } = null!;
}
