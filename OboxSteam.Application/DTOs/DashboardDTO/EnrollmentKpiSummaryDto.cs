namespace OboxSteam.Application.DTOs.DashboardDTO;

public class EnrollmentKpiSummaryDto
{
    public int TotalPrograms { get; set; }

    public int ActiveStudents { get; set; }

    public int NewEnrollmentsInRange { get; set; }

    public decimal CompletionRate { get; set; }
}
