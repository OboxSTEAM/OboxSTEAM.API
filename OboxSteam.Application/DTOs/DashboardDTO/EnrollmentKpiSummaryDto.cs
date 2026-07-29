namespace OboxSteam.Application.DTOs.DashboardDTO;

public class EnrollmentKpiSummaryDto
{
    public int TotalPrograms { get; set; }

    public int ActiveStudents { get; set; }

    public int NewEnrollmentsInRange { get; set; }

    public int NewEnrollmentsInPreviousRange { get; set; }

    /// <summary>Percentage on a 0–100 scale (see <see cref="RateUnit"/>).</summary>
    public decimal CompletionRate { get; set; }

    public decimal CompletionRateInPreviousRange { get; set; }

    /// <summary>Always "percent" — rate fields use a 0–100 scale, not 0–1.</summary>
    public string RateUnit { get; set; } = "percent";
}
