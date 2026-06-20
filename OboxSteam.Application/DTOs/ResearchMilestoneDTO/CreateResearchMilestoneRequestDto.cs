using System.ComponentModel.DataAnnotations;
using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.DTOs.ResearchMilestoneDTO;

/// <summary>
/// Creates a research milestone and its linked deliverable assignment in one request.
/// </summary>
public class CreateResearchMilestoneRequestDto
{
    [Required(ErrorMessage = "Code is required.")]
    [MaxLength(50)]
    public string Code { get; set; } = null!;

    [Required(ErrorMessage = "Title is required.")]
    [MaxLength(255)]
    public string Title { get; set; } = null!;

    public string? Description { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "MilestoneOrder must be at least 1.")]
    public int MilestoneOrder { get; set; }

    public bool IsCapstone { get; set; }

    [Required(ErrorMessage = "AssignmentCode is required.")]
    [MaxLength(50)]
    public string AssignmentCode { get; set; } = null!;

    [Required(ErrorMessage = "AssignmentTitle is required.")]
    [MaxLength(255)]
    public string AssignmentTitle { get; set; } = null!;

    public string? AssignmentDescription { get; set; }

    public AssignmentType AssignmentType { get; set; } = AssignmentType.FileUpload;

    public int MaxPoints { get; set; }

    public decimal PassScore { get; set; }

    public DateTime? DueDate { get; set; }

    public DateTime? AvailableFrom { get; set; }

    public DateTime? AvailableUntil { get; set; }

    public int MaxAttempts { get; set; } = 1;
}
