using OboxSteam.Application.DTOs.DashboardDTO;

namespace OboxSteam.Application.Interfaces;

/// <summary>
/// Manager/SuperAdmin statistics dashboard. All methods accept the same
/// <see cref="DashboardFilterDto"/>; status/entity filters that do not apply to a
/// section are silently ignored so the frontend can send one shared query string.
/// </summary>
public interface IDashboardService
{
    /// <summary>Trimmed KPI cards only (no trends / nested lists).</summary>
    Task<DashboardOverviewDto> GetOverviewAsync(DashboardFilterDto filter);

    /// <summary>
    /// Landing payload: full revenue/enrollment/assessment/operations sections
    /// (KPIs + trends + top-N) computed once for the page load.
    /// </summary>
    Task<DashboardLandingDto> GetLandingAsync(DashboardFilterDto filter);

    /// <summary>
    /// Revenue aggregates. Honors date range, programId/moduleId/classId, paymentStatus,
    /// and page/pageSize/sortBy for TopProgramsByRevenue. Ignores enrollment/submission/class status filters.
    /// </summary>
    Task<RevenueOverviewDto> GetRevenueOverviewAsync(DashboardFilterDto filter);

    /// <summary>
    /// Enrollment aggregates. Honors date range, programId/moduleId/classId,
    /// enrollmentStatus, classEnrollmentStatus, and pagination for TopProgramsByEnrollment.
    /// Ignores paymentStatus, submissionStatus, classStatus.
    /// Rate fields use a 0–100 percent scale (<c>RateUnit = "percent"</c>).
    /// </summary>
    Task<EnrollmentOverviewDto> GetEnrollmentOverviewAsync(DashboardFilterDto filter);

    /// <summary>
    /// Assessment aggregates. Honors date range, programId/moduleId/classId, submissionStatus.
    /// Ignores payment/enrollment/class status filters and pagination (no nested list).
    /// Rate fields use a 0–100 percent scale (<c>RateUnit = "percent"</c>).
    /// </summary>
    Task<AssessmentOverviewDto> GetAssessmentOverviewAsync(DashboardFilterDto filter);

    /// <summary>
    /// Class operations aggregates. Honors date range, programId/moduleId/classId, classStatus,
    /// and pagination for MentorUtilization. Ignores payment/enrollment/submission status filters.
    /// Rate fields use a 0–100 percent scale (<c>RateUnit = "percent"</c>).
    /// </summary>
    Task<OperationsOverviewDto> GetOperationsOverviewAsync(DashboardFilterDto filter);
}
