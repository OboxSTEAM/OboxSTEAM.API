namespace OboxSteam.Application.DTOs.DashboardDTO;

public class TopProgramEnrollmentDto
{
    public Guid ProgramId { get; set; }

    public string ProgramName { get; set; } = null!;

    public int Count { get; set; }
}
