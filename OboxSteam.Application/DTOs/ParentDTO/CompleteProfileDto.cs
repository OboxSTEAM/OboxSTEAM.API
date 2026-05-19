using System.ComponentModel.DataAnnotations;

namespace OboxSteam.Application.DTOs.ParentDTO;

public class CompleteProfileDto
{
    [Required]
    [MaxLength(255)]
    public string FullName { get; set; } = null!;

    [MaxLength(20)]
    public string? Phone { get; set; }

    [Required]
    [MinLength(6, ErrorMessage = "Password must be at least 6 characters.")]
    public string Password { get; set; } = null!;
}
