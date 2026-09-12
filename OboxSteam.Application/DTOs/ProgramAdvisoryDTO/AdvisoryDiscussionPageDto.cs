namespace OboxSteam.Application.DTOs.ProgramAdvisoryDTO;

public sealed class AdvisoryDiscussionPageDto
{
    public List<AdvisoryDiscussionMessageDto> Messages { get; set; } = [];
    public string? Before { get; set; }
    public string? After { get; set; }
    public bool HasMoreBefore { get; set; }
    public bool HasMoreAfter { get; set; }
}
