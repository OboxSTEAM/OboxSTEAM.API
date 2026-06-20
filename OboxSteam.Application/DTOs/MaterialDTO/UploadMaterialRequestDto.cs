using System.ComponentModel.DataAnnotations;

namespace OboxSteam.Application.DTOs.MaterialDTO;

public class UploadMaterialRequestDto
{
    /// <summary>
    /// SelfPaced activity this material belongs to.
    /// </summary>
    [Required]
    public Guid ActivityId { get; set; }

    /// <summary>
    /// Display title of the material.
    /// </summary>
    [Required, MaxLength(255)]
    public string Title { get; set; } = null!;
}
