using Microsoft.AspNetCore.SignalR;
using OboxSteam.Application.DTOs.NotificationDTO;
using OboxSteam.Application.Interfaces;

namespace OboxSteam.API.Hubs;

/// <summary>Pushes persisted notifications to the recipient's SignalR user group.</summary>
public sealed class SignalRNotificationDispatcher : INotificationDispatcher
{
    private readonly IHubContext<NotificationHub> _hubContext;

    public SignalRNotificationDispatcher(IHubContext<NotificationHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public Task DispatchAsync(NotificationDto notification, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(notification);
        return DispatchManyAsync(new[] { notification }, cancellationToken);
    }

    public async Task DispatchManyAsync(
        IReadOnlyList<NotificationDto> notifications,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(notifications);
        if (notifications.Count == 0)
        {
            return;
        }

        foreach (var group in notifications.GroupBy(n => n.RecipientUserId))
        {
            foreach (var dto in group)
            {
                await _hubContext.Clients
                    .Group($"user:{dto.RecipientUserId}")
                    .SendAsync("notificationReceived", dto, cancellationToken);
            }
        }
    }
}
