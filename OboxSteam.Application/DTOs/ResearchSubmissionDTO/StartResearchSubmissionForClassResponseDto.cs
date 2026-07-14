namespace OboxSteam.Application.DTOs.ResearchSubmissionDTO;

public class StartResearchSubmissionForClassResponseDto
{
    public Guid ClassId { get; set; }
    public Guid ResearchMilestoneId { get; set; }
    public int TotalClassStudents { get; set; }
    public int OpenedCount { get; set; }
    public int SkippedCount { get; set; }
    public List<ResearchSubmissionResponseDto> Opened { get; set; } = [];
    public List<StartResearchSubmissionForClassSkippedDto> Skipped { get; set; } = [];
}
