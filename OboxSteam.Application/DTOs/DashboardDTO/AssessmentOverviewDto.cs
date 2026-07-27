namespace OboxSteam.Application.DTOs.DashboardDTO;

public class AssessmentOverviewDto
{
    public int TotalSubmissions { get; set; }

    /// <summary>Submissions with SubmittedAt in the current resolved range.</summary>
    public int SubmissionsInRange { get; set; }

    /// <summary>Submissions with SubmittedAt in the previous adjacent window.</summary>
    public int SubmissionsInPreviousRange { get; set; }

    public List<StatusCountDto> SubmissionsByStatus { get; set; } = new();

    public int GradingBacklogCount { get; set; }

    /// <summary>
    /// Hours before UtcNow used as the backlog cutoff (Pending/TurnedIn older than this).
    /// </summary>
    public int GradingBacklogThresholdHours { get; set; } = 48;

    public double AverageGradingTurnaroundHours { get; set; }

    /// <summary>Percentage on a 0–100 scale (see <see cref="RateUnit"/>).</summary>
    public decimal PassRate { get; set; }

    /// <summary>
    /// Pass rate among submissions graded in the previous window (0–100 scale).
    /// </summary>
    public decimal PassRateInPreviousRange { get; set; }

    public decimal AverageScore { get; set; }

    /// <summary>Always "percent" — rate fields use a 0–100 scale, not 0–1.</summary>
    public string RateUnit { get; set; } = "percent";

    public TrendSeriesDto SubmissionsTrend { get; set; } = null!;
}
