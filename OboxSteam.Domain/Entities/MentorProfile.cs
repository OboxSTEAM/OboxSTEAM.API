using System.ComponentModel.DataAnnotations;

namespace OboxSteam.Domain.Entities;

/// <summary>
/// Mentor public profile — 1:1 with User (mentor).
/// Uses MentorId as both PK and FK (same shape as StudentProfile).
/// </summary>
public class MentorProfile : BaseEntity
{
    public Guid MentorId { get; set; }
    public User Mentor { get; set; } = null!;

    [MaxLength(255)]
    public string? Title { get; set; }

    [MaxLength(255)]
    public string? Organization { get; set; }

    public string? Bio { get; set; }

    public string? Achievements { get; set; }

    public string? LinkedInUrl { get; set; }
}
