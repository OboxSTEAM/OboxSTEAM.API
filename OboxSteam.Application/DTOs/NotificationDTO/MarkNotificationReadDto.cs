namespace OboxSteam.Application.DTOs.NotificationDTO;

/// <summary>Optional body for batch mark-read (reserved for clients that send ids).</summary>
public sealed class MarkNotificationReadDto
{
    public List<Guid>? NotificationIds { get; set; }
}
