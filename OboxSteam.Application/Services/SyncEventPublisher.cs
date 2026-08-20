using Microsoft.Extensions.Logging;
using OboxSteam.Application.Interfaces;
using OboxSteam.Application.Notifications;
using OboxSteam.Application.Realtime;
using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.Services;

/// <summary>
/// Resolves sync-event audiences per-user (same pattern as notifications) and pushes
/// ephemeral hints over SignalR without persisting anything. The Managers audience
/// fans out through the existing <c>role:Manager</c> hub group instead of per-user groups.
/// Push failures are logged and swallowed — a missed hint only means a stale screen
/// until the next refetch, never broken data.
/// </summary>
public sealed class SyncEventPublisher : ISyncEventPublisher
{
    private readonly INotificationRecipientResolver _recipientResolver;
    private readonly ISignalRSyncDispatcher _dispatcher;
    private readonly ICurrentTime _currentTime;
    private readonly ILogger<SyncEventPublisher> _logger;

    public SyncEventPublisher(
        INotificationRecipientResolver recipientResolver,
        ISignalRSyncDispatcher dispatcher,
        ICurrentTime currentTime,
        ILogger<SyncEventPublisher> logger)
    {
        _recipientResolver = recipientResolver;
        _dispatcher = dispatcher;
        _currentTime = currentTime;
        _logger = logger;
    }

    public async Task PublishAsync(
        string scope,
        NotificationAudience audience,
        string entityType,
        Guid entityId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(audience);

        var syncEvent = new SyncEvent
        {
            Scope = scope,
            EntityType = entityType,
            EntityId = entityId,
            At = new DateTimeOffset(_currentTime.GetCurrentTime(), TimeSpan.Zero),
        };

        try
        {
            if (audience.Kind == NotificationAudienceKind.Managers)
            {
                await _dispatcher.DispatchToRoleGroupAsync(
                    RoleType.Manager.ToString(),
                    syncEvent,
                    cancellationToken);
                return;
            }

            var recipients = await _recipientResolver.ResolveAsync(audience, cancellationToken);
            var userIds = recipients
                .Select(r => r.UserId)
                .Distinct()
                .ToList();

            if (userIds.Count == 0)
            {
                _logger.LogDebug(
                    "Sync event {Scope} for {EntityType} {EntityId} resolved to zero recipients; skipping.",
                    scope,
                    entityType,
                    entityId);
                return;
            }

            await _dispatcher.DispatchToUsersAsync(userIds, syncEvent, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to dispatch sync event {Scope} for {EntityType} {EntityId}.",
                scope,
                entityType,
                entityId);
        }
    }
}
