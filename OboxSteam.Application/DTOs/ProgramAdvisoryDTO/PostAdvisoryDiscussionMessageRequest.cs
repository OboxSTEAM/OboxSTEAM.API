namespace OboxSteam.Application.DTOs.ProgramAdvisoryDTO;

public sealed class PostAdvisoryDiscussionMessageRequest
{
    public string Text { get; set; } = null!;
    public List<Guid>? ReferenceIds { get; set; }
    public string ClientMessageId { get; set; } = null!;
}
