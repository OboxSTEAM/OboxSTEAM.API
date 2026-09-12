namespace OboxSteam.Application.DTOs.ProgramAdvisoryDTO;

public sealed class AdvisoryCapabilitiesDto
{
    public bool CanCreateSuggestion { get; set; }
    public bool CanCreateRequiredChange { get; set; }
    public bool CanDiscuss { get; set; }
    public bool CanReplyToNotes { get; set; }
    public bool CanEditCurriculum { get; set; }
    public bool CanAssignAdvisor { get; set; }
    public bool CanDecide { get; set; }
}
