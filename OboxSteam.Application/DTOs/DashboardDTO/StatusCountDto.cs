namespace OboxSteam.Application.DTOs.DashboardDTO;

/// <summary>
/// Zero-filled status bucket ordered by enum ordinal for stable bar charts.
/// </summary>
public class StatusCountDto
{
    public string Status { get; set; } = null!;

    public int Count { get; set; }
}
