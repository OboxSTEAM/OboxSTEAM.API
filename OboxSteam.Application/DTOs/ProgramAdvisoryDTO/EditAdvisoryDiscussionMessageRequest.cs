namespace OboxSteam.Application.DTOs.ProgramAdvisoryDTO;

public sealed class EditAdvisoryDiscussionMessageRequest
{
    /// <summary>Mentions are re-parsed. Empty only when the message has attachments.</summary>
    public string? Text { get; set; }
}
