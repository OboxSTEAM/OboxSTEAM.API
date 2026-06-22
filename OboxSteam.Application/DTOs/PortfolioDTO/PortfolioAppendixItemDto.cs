namespace OboxSteam.Application.DTOs.PortfolioDTO;

public class PortfolioAppendixItemDto
{
    public Guid Id { get; set; }
    public Guid SubmissionId { get; set; }
    public string SectionTitle { get; set; } = null!;
    public int DisplayOrder { get; set; }
    public string? ContentText { get; set; }
    public string? FileUrl { get; set; }
    public decimal? AssignedGrade { get; set; }
    public string? MilestoneTitle { get; set; }
}
