namespace OboxSteam.Application.DTOs.DashboardDTO;

public class AssessmentKpiSummaryDto
{
    public int TotalSubmissions { get; set; }

    public int SubmissionsInRange { get; set; }

    public int SubmissionsInPreviousRange { get; set; }

    public int GradingBacklogCount { get; set; }

    public int GradingBacklogThresholdHours { get; set; } = 48;

    /// <summary>Percentage on a 0–100 scale (see <see cref="RateUnit"/>).</summary>
    public decimal PassRate { get; set; }

    public decimal PassRateInPreviousRange { get; set; }

    public decimal AverageScore { get; set; }

    /// <summary>Always "percent" — rate fields use a 0–100 scale, not 0–1.</summary>
    public string RateUnit { get; set; } = "percent";
}
