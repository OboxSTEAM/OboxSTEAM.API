using System.ComponentModel.DataAnnotations;

namespace OboxSteam.Application.DTOs.ClassDTO;

public class CreateClassRequestDto
{
    [Required(ErrorMessage = "Code is required.")]
    [MaxLength(50)]
    public string Code { get; set; } = null!;

    [Required(ErrorMessage = "Name is required.")]
    [MaxLength(255)]
    public string Name { get; set; } = null!;

    [Required(ErrorMessage = "ProgramId is required.")]
    public Guid ProgramId { get; set; }

    /// <summary>
    /// Optional. When omitted, the class is open for mentor assignment requests.
    /// </summary>
    public Guid? MentorId { get; set; }

    [Required(ErrorMessage = "StartDate is required.")]
    public DateTime StartDate { get; set; }

    [Required(ErrorMessage = "EndDate is required.")]
    public DateTime EndDate { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "MaxCapacity must be at least 1.")]
    public int MaxCapacity { get; set; }

    public int MinHoursBeforeAssignmentJoin { get; set; } = 48;

    [MaxLength(255)]
    public string? ScheduleSummary { get; set; }
}
