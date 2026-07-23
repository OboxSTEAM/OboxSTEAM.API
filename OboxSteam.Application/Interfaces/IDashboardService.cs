using OboxSteam.Application.DTOs.DashboardDTO;

namespace OboxSteam.Application.Interfaces;

/// <summary>
/// Manager/SuperAdmin statistics dashboard. All five methods accept the same
/// <see cref="DashboardFilterDto"/>; status/entity filters that do not apply to a
/// section are silently ignored so the frontend can send one shared query string.
/// </summary>
public interface IDashboardService
{
    /// <summary>Landing KPI cards (trimmed summaries of all four sections).</summary>
    Task<DashboardOverviewDto> GetOverviewAsync(DashboardFilterDto filter);

    /// <summary>
    /// Revenue aggregates. Honors date range, programId/moduleId/classId, paymentStatus,
    /// and page/pageSize/sortBy for TopProgramsByRevenue. Ignores enrollment/submission/class status filters.
    /// </summary>
    Task<RevenueOverviewDto> GetRevenueOverviewAsync(DashboardFilterDto filter);

    /// <summary>
    /// Enrollment aggregates. Honors date range, programId/moduleId/classId,
    /// enrollmentStatus, classEnrollmentStatus, and pagination for TopProgramsByEnrollment.
    /// Ignores paymentStatus, submissionStatus, classStatus.
    /// </summary>
    Task<EnrollmentOverviewDto> GetEnrollmentOverviewAsync(DashboardFilterDto filter);

    /// <summary>
    /// Assessment aggregates. Honors date range, programId/moduleId/classId, submissionStatus.
    /// Ignores payment/enrollment/class status filters and pagination (no nested list).
    /// </summary>
    Task<AssessmentOverviewDto> GetAssessmentOverviewAsync(DashboardFilterDto filter);

    /// <summary>
    /// Class operations aggregates. Honors date range, programId/moduleId/classId, classStatus,
    /// and pagination for MentorUtilization. Ignores payment/enrollment/submission status filters.
    /// </summary>
    Task<OperationsOverviewDto> GetOperationsOverviewAsync(DashboardFilterDto filter);
}
