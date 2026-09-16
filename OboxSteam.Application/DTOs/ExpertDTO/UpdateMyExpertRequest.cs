using System.ComponentModel.DataAnnotations;

namespace OboxSteam.Application.DTOs.ExpertDTO;

/// <summary>
/// Self-service profile update for the authenticated Expert.
/// Does not accept <c>code</c>, <c>programs</c>, <c>email</c>, or <c>userId</c>.
/// </summary>
public sealed class UpdateMyExpertRequest
{
    [Required(ErrorMessage = "Full name is required")]
    [MinLength(2, ErrorMessage = "Full name must be at least 2 characters long")]
    [MaxLength(255)]
    public string FullName { get; set; } = null!;

    [MaxLength(255)]
    public string? Title { get; set; }

    [MaxLength(255)]
    public string? Organization { get; set; }

    [MaxLength(4000)]
    public string? Bio { get; set; }

    [MaxLength(2048)]
    public string? LinkedInUrl { get; set; }

    [MaxLength(4000)]
    public string? Achievements { get; set; }

    /// <summary>Specialization tags (max 20 × 80 chars). Null leaves existing tags unchanged.</summary>
    public string[]? Specialization { get; set; }

    /// <summary>
    /// Optional direct URL set. Prefer <c>POST /api/experts/me/avatar</c>.
    /// Null leaves existing avatar unchanged; empty string clears it.
    /// </summary>
    [MaxLength(2048)]
    public string? AvatarUrl { get; set; }
}
