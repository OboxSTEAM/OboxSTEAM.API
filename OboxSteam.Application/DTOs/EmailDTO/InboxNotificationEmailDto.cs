namespace OboxSteam.Application.DTOs.EmailDTO;

public sealed class InboxNotificationEmailDto
{
    public string To { get; set; } = null!;

    public string Title { get; set; } = null!;

    public string? Body { get; set; }
}
