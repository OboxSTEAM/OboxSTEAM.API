using OboxSteam.Application.DTOs.NotificationDTO;

namespace OboxSteam.Application.Interfaces;

/// <summary>Real-time push channel; implemented in the API layer with SignalR.</summary>
public interface INotificationDispatcher
{
    Task DispatchAsync(NotificationDto notification, CancellationToken cancellationToken = default);

    Task DispatchManyAsync(IReadOnlyList<NotificationDto> notifications, CancellationToken cancellationToken = default);
}
