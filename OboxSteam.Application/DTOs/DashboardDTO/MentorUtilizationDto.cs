namespace OboxSteam.Application.DTOs.DashboardDTO;

public class MentorUtilizationDto
{
    public Guid MentorId { get; set; }

    public string MentorName { get; set; } = null!;

    public int Assigned { get; set; }

    public int Pending { get; set; }

    public int Max { get; set; }
}
