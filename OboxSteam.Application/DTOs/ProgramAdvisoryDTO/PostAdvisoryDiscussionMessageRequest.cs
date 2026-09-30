namespace OboxSteam.Application.DTOs.ProgramAdvisoryDTO;

public sealed class PostAdvisoryDiscussionMessageRequest
{
    /// <summary>May contain mention tokens <c>@[Type:uuid]</c>. Empty only when attachments are present.</summary>
    public string? Text { get; set; }

    /// <summary>Unsent attachments uploaded by the caller for this program.</summary>
    public List<Guid>? AttachmentIds { get; set; }

    public string ClientMessageId { get; set; } = null!;
}
