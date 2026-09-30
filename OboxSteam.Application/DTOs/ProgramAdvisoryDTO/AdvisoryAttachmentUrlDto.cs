namespace OboxSteam.Application.DTOs.ProgramAdvisoryDTO;

public sealed class AdvisoryAttachmentUrlDto
{
    public string Url { get; set; } = null!;
    public DateTime ExpiresAt { get; set; }
}
