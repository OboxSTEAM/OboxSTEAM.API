namespace OboxSteam.Application.DTOs.ProgramAdvisoryDTO;

public sealed class AdvisoryMessageDto
{
    public Guid Id { get; set; }

    public Guid ThreadId { get; set; }

    public Guid AuthorUserId { get; set; }

    public string? AuthorName { get; set; }

    public string Message { get; set; } = null!;

    public DateTime CreatedAt { get; set; }
}
