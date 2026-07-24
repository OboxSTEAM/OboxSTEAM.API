namespace OboxSteam.Application.DTOs.DashboardDTO;

public class AssessmentOverviewDto
{
    public int TotalSubmissions { get; set; }

    public Dictionary<string, int> SubmissionsByStatus { get; set; } = new();

    public int GradingBacklogCount { get; set; }

    public double AverageGradingTurnaroundHours { get; set; }

    public decimal PassRate { get; set; }

    public decimal AverageScore { get; set; }

    public List<TrendPointDto> SubmissionsTrend { get; set; } = new();
}
