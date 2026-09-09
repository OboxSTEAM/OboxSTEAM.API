using System.ComponentModel.DataAnnotations;

namespace OboxSteam.Application.DTOs.MentorDTO;

public sealed class CreateMentorRequestDto
{
    [Required(ErrorMessage = "Email is required")]
    [EmailAddress(ErrorMessage = "Invalid email format")]
    public string Email { get; set; } = null!;

    [Required(ErrorMessage = "Full name is required")]
    [MinLength(2, ErrorMessage = "Full name must be at least 2 characters long")]
    public string FullName { get; set; } = null!;

    [MaxLength(20)]
    public string? Phone { get; set; }
}
