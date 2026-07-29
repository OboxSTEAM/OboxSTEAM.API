namespace OboxSteam.Application.DTOs.DashboardDTO;

/// <summary>
/// Single-request landing payload: full section DTOs (KPIs + trends + top-N lists)
/// computed in one pass to avoid N+1 section fetches.
/// </summary>
public class DashboardLandingDto
{
    public RevenueOverviewDto Revenue { get; set; } = null!;

    public EnrollmentOverviewDto Enrollment { get; set; } = null!;

    public AssessmentOverviewDto Assessment { get; set; } = null!;

    public OperationsOverviewDto Operations { get; set; } = null!;
}
