using System.ComponentModel.DataAnnotations;

namespace OboxSteam.Application.DTOs.MaterialDTO;

public class UpdateMaterialRequestDto
{
    /// <summary>
    /// New title for the material (optional).
    /// </summary>
    [MaxLength(255)]
    public string? Title { get; set; }
}
