namespace OboxSteam.Application.DTOs.ProgramAdvisoryDTO;

public sealed class AdvisoryDiscussionMessageDto
{
    public Guid Id { get; set; }
    public Guid ProgramId { get; set; }
    public Guid AuthorUserId { get; set; }
    public string? AuthorName { get; set; }
    public long Sequence { get; set; }
    public string Cursor { get; set; } = null!;
    public string Text { get; set; } = null!;
    public string ClientMessageId { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
    public List<AdvisoryReferenceDto> References { get; set; } = [];
}
