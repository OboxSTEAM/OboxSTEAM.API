using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.DTOs.ClassDTO;

public class UpdateClassRequestDto
{
    public string? Code { get; set; }
    public string? Name { get; set; }
    public Guid? ProgramId { get; set; }
    public Guid? MentorId { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public int? MaxCapacity { get; set; }
    public ClassStatus? Status { get; set; }
    public int? MinHoursBeforeAssignmentJoin { get; set; }
    public string? ScheduleSummary { get; set; }

    /// <summary>
    /// When provided, replaces the class required-skill tags (empty list clears all).
    /// </summary>
    public List<Guid>? RequiredSkillIds { get; set; }
}
