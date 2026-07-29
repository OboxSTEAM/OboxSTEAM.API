namespace OboxSteam.Application.DTOs.DashboardDTO;

public class OperationsKpiSummaryDto
{
    public int ActiveClassCount { get; set; }

    /// <summary>Percentage on a 0–100 scale (see <see cref="RateUnit"/>).</summary>
    public decimal AverageCapacityUtilization { get; set; }

    public decimal AverageCapacityUtilizationInPreviousRange { get; set; }

    public int PendingMentorRequestsCount { get; set; }

    /// <summary>Percentage on a 0–100 scale (see <see cref="RateUnit"/>).</summary>
    public decimal AverageAttendanceRate { get; set; }

    public decimal AverageAttendanceRateInPreviousRange { get; set; }

    /// <summary>Always "percent" — rate fields use a 0–100 scale, not 0–1.</summary>
    public string RateUnit { get; set; } = "percent";
}
