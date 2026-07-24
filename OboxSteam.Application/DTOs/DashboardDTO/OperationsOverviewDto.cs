using OboxSteam.Application.Commons;

namespace OboxSteam.Application.DTOs.DashboardDTO;

public class OperationsOverviewDto
{
    public Dictionary<string, int> ClassesByStatus { get; set; } = new();

    public decimal AverageCapacityUtilization { get; set; }

    public int PendingMentorRequestsCount { get; set; }

    public decimal AverageAttendanceRate { get; set; }

    public List<TrendPointDto> AttendanceTrend { get; set; } = new();

    public Pagination<MentorUtilizationDto> MentorUtilization { get; set; } = null!;
}
