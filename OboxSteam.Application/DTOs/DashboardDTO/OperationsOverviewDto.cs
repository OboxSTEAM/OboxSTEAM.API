using OboxSteam.Application.Commons;

namespace OboxSteam.Application.DTOs.DashboardDTO;

public class OperationsOverviewDto
{
    public List<StatusCountDto> ClassesByStatus { get; set; } = new();

    /// <summary>Percentage on a 0–100 scale (see <see cref="RateUnit"/>).</summary>
    public decimal AverageCapacityUtilization { get; set; }

    /// <summary>
    /// Capacity utilization for classes overlapping the previous window (0–100 scale).
    /// </summary>
    public decimal AverageCapacityUtilizationInPreviousRange { get; set; }

    public int PendingMentorRequestsCount { get; set; }

    /// <summary>Percentage on a 0–100 scale (see <see cref="RateUnit"/>).</summary>
    public decimal AverageAttendanceRate { get; set; }

    /// <summary>
    /// Attendance rate for sessions in the previous window (0–100 scale).
    /// </summary>
    public decimal AverageAttendanceRateInPreviousRange { get; set; }

    /// <summary>Always "percent" — rate fields use a 0–100 scale, not 0–1.</summary>
    public string RateUnit { get; set; } = "percent";

    public TrendSeriesDto AttendanceTrend { get; set; } = null!;

    public Pagination<MentorUtilizationDto> MentorUtilization { get; set; } = null!;
}
