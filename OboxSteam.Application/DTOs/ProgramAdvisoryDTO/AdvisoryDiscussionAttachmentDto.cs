using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.DTOs.ProgramAdvisoryDTO;

public sealed class AdvisoryDiscussionAttachmentDto
{
    public Guid Id { get; set; }
    public string FileName { get; set; } = null!;
    public string ContentType { get; set; } = null!;
    public long SizeBytes { get; set; }
    public DiscussionAttachmentKind Kind { get; set; }
    public Guid UploaderUserId { get; set; }
    public DateTime CreatedAt { get; set; }
}
