using OboxSteam.Application.DTOs.NotificationDTO;

namespace OboxSteam.Application.Interfaces;

/// <summary>Development-only helper to publish every <c>NotificationType</c> via <c>NotificationCatalog</c>.</summary>
public interface INotificationSmokeTestService
{
    Task<NotificationSmokeTestResultDto> PublishAllCatalogTypesAsync(CancellationToken cancellationToken = default);
}
