using System.ComponentModel.DataAnnotations;

namespace OboxSteam.Application.DTOs.MaterialDTO;

public class UploadMaterialRequestDto
{
    /// <summary>
    /// The module this material belongs to (required).
    /// </summary>
    [Required]
    public Guid ModuleId { get; set; }

    /// <summary>
    /// Specific course within the module (optional).
    /// </summary>
    public Guid? CourseId { get; set; }

    /// <summary>
    /// Specific activity (optional).
    /// </summary>
    public Guid? ActivityId { get; set; }

    /// <summary>
    /// Display title of the material.
    /// </summary>
    [Required, MaxLength(255)]
    public string Title { get; set; } = null!;
}
