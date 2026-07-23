namespace OboxSteam.Application.DTOs.DashboardDTO;

public class AssessmentKpiSummaryDto
{
    public int TotalSubmissions { get; set; }

    public int GradingBacklogCount { get; set; }

    public decimal PassRate { get; set; }

    public decimal AverageScore { get; set; }
}
