using OboxSteam.Application.Notifications;

namespace OboxSteam.Application.Interfaces;

/// <summary>Single entry point for business services to emit in-app notifications.</summary>
public interface INotificationPublisher
{
    Task PublishAsync(NotificationCommand command, CancellationToken cancellationToken = default);

    Task PublishManyAsync(IReadOnlyList<NotificationCommand> commands, CancellationToken cancellationToken = default);
}
