namespace OboxSteam.Application.DTOs.DashboardDTO;

public class OperationsKpiSummaryDto
{
    public int ActiveClassCount { get; set; }

    public decimal AverageCapacityUtilization { get; set; }

    public int PendingMentorRequestsCount { get; set; }

    public decimal AverageAttendanceRate { get; set; }
}
