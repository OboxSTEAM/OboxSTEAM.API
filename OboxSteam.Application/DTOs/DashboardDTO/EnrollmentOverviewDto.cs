using OboxSteam.Application.Commons;

namespace OboxSteam.Application.DTOs.DashboardDTO;

public class EnrollmentOverviewDto
{
    public int TotalPrograms { get; set; }

    public int TotalModules { get; set; }

    public int TotalCourses { get; set; }

    public int ActiveStudents { get; set; }

    public int NewEnrollmentsInRange { get; set; }

    /// <summary>New program enrollments in the previous adjacent window of equal length.</summary>
    public int NewEnrollmentsInPreviousRange { get; set; }

    /// <summary>Percentage on a 0–100 scale (see <see cref="RateUnit"/>).</summary>
    public decimal CompletionRate { get; set; }

    /// <summary>
    /// Completion rate among program enrollments with EnrolledAt in the previous window
    /// (0–100 scale).
    /// </summary>
    public decimal CompletionRateInPreviousRange { get; set; }

    /// <summary>Always "percent" — rate fields use a 0–100 scale, not 0–1.</summary>
    public string RateUnit { get; set; } = "percent";

    public List<StatusCountDto> ProgramEnrollmentsByStatus { get; set; } = new();

    public List<StatusCountDto> ModuleEnrollmentsByStatus { get; set; } = new();

    public List<StatusCountDto> ClassEnrollmentsByStatus { get; set; } = new();

    public TrendSeriesDto EnrollmentTrend { get; set; } = null!;

    public Pagination<TopProgramEnrollmentDto> TopProgramsByEnrollment { get; set; } = null!;
}
