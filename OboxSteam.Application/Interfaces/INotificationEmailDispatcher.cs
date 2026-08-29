using OboxSteam.Application.DTOs.NotificationDTO;

namespace OboxSteam.Application.Interfaces;

/// <summary>
/// Email channel for priority inbox events; implemented in Infrastructure with Resend.
/// </summary>
public interface INotificationEmailDispatcher
{
    Task DispatchManyAsync(
        IReadOnlyList<NotificationDto> notifications,
        CancellationToken cancellationToken = default);
}
