namespace OboxSteam.Application.DTOs.DashboardDTO;

public class TopProgramRevenueDto
{
    public Guid ProgramId { get; set; }

    public string ProgramName { get; set; } = null!;

    public decimal Amount { get; set; }
}
