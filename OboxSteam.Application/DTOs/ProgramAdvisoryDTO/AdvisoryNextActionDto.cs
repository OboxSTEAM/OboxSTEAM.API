namespace OboxSteam.Application.DTOs.ProgramAdvisoryDTO;

public sealed class AdvisoryNextActionDto
{
    public string Code { get; set; } = "None";

    /// <summary>Manager or Advisor. Empty when there is no next action.</summary>
    public string ForRole { get; set; } = "";
}
