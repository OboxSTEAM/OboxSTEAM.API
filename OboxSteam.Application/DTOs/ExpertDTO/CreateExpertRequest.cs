using System.ComponentModel.DataAnnotations;

namespace OboxSteam.Application.DTOs.ExpertDTO;

public class CreateExpertRequest
{
    [Required(ErrorMessage = "Code is required")]
    public string Code { get; set; } = null!;

    [Required(ErrorMessage = "Email is required")]
    [EmailAddress(ErrorMessage = "Invalid email format")]
    public string Email { get; set; } = null!;

    [Required(ErrorMessage = "Password is required")]
    [MinLength(6, ErrorMessage = "Password must be at least 6 characters long")]
    public string Password { get; set; } = null!;

    [Required(ErrorMessage = "Full name is required")]
    [MinLength(2, ErrorMessage = "Full name must be at least 2 characters long")]
    public string FullName { get; set; } = null!;

    [MaxLength(20)]
    public string? Phone { get; set; }

    public string? Title { get; set; }
    public string? Organization { get; set; }
    public string? Bio { get; set; }
    public string? AvatarUrl { get; set; }
    public string? LinkedInUrl { get; set; }
    public string? Achievements { get; set; }
    public string[]? Specialization { get; set; }

    /// <summary>Optional. Program board assignments; omit or send empty to create an expert without programs.</summary>
    public List<ExpertProgramAssignmentDto>? Programs { get; set; }
}
