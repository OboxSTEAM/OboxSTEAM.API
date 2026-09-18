namespace OboxSteam.Application.DTOs.DashboardDTO;

public class MentorUtilizationDto
{
    public Guid MentorId { get; set; }

    public string MentorName { get; set; } = null!;

    /// <summary>Public avatar URL; null when the mentor has no avatar (FE falls back to initials).</summary>
    public string? AvatarUrl { get; set; }

    /// <summary>Short line under the name (e.g. mentor title / specialty).</summary>
    public string? Title { get; set; }

    public int Assigned { get; set; }

    public int Pending { get; set; }

    public int Max { get; set; }
}
