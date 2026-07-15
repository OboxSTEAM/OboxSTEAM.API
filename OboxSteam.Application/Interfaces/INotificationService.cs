using OboxSteam.Application.Commons;
using OboxSteam.Application.DTOs.NotificationDTO;

namespace OboxSteam.Application.Interfaces;

/// <summary>Current-user inbox REST operations.</summary>
public interface INotificationService
{
    Task<Pagination<NotificationDto>> GetMyNotificationsAsync(int page, int pageSize, bool? unreadOnly);

    Task<NotificationUnreadCountDto> GetUnreadCountAsync();

    Task MarkReadAsync(Guid notificationId);

    Task MarkAllReadAsync();
}
