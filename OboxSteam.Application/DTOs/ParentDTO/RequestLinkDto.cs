using System.ComponentModel.DataAnnotations;

namespace OboxSteam.Application.DTOs.ParentDTO;

public class RequestLinkDto
{
    [Required(ErrorMessage = "Parent email is required.")]
    [EmailAddress(ErrorMessage = "Invalid email format.")]
    public string ParentEmail { get; set; } = null!;
}
