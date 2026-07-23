namespace OboxSteam.Application.DTOs.DashboardDTO;

/// <summary>
/// Landing-page KPI summary combining the four dashboard sections (no trends / nested lists).
/// </summary>
public class DashboardOverviewDto
{
    public RevenueKpiSummaryDto Revenue { get; set; } = null!;

    public EnrollmentKpiSummaryDto Enrollment { get; set; } = null!;

    public AssessmentKpiSummaryDto Assessment { get; set; } = null!;

    public OperationsKpiSummaryDto Operations { get; set; } = null!;
}
