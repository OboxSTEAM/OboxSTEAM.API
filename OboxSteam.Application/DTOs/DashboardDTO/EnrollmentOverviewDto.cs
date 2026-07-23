using OboxSteam.Application.Commons;

namespace OboxSteam.Application.DTOs.DashboardDTO;

public class EnrollmentOverviewDto
{
    public int TotalPrograms { get; set; }

    public int TotalModules { get; set; }

    public int TotalCourses { get; set; }

    public int ActiveStudents { get; set; }

    public int NewEnrollmentsInRange { get; set; }

    public decimal CompletionRate { get; set; }

    public Dictionary<string, int> ProgramEnrollmentsByStatus { get; set; } = new();

    public Dictionary<string, int> ModuleEnrollmentsByStatus { get; set; } = new();

    public Dictionary<string, int> ClassEnrollmentsByStatus { get; set; } = new();

    public List<TrendPointDto> EnrollmentTrend { get; set; } = new();

    public Pagination<TopProgramEnrollmentDto> TopProgramsByEnrollment { get; set; } = null!;
}
