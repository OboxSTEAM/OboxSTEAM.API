using Microsoft.AspNetCore.SignalR;
using OboxSteam.Application.Interfaces;
using OboxSteam.Application.Realtime;

namespace OboxSteam.API.Hubs;

/// <summary>
/// Pushes ephemeral sync events over the shared notifications hub —
/// per-user groups for resolved audiences, <c>role:{role}</c> groups for role fan-out.
/// </summary>
public sealed class SignalRSyncDispatcher : ISignalRSyncDispatcher
{
    private readonly IHubContext<NotificationHub> _hubContext;

    public SignalRSyncDispatcher(IHubContext<NotificationHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public async Task DispatchToUsersAsync(
        IReadOnlyCollection<Guid> userIds,
        SyncEvent syncEvent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(syncEvent);
        if (userIds.Count == 0)
        {
            return;
        }

        foreach (var userId in userIds)
        {
            await _hubContext.Clients
                .Group($"user:{userId}")
                .SendAsync(ISignalRSyncDispatcher.ClientEventName, syncEvent, cancellationToken);
        }
    }

    public Task DispatchToRoleGroupAsync(
        string role,
        SyncEvent syncEvent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(syncEvent);
        return _hubContext.Clients
            .Group($"role:{role}")
            .SendAsync(ISignalRSyncDispatcher.ClientEventName, syncEvent, cancellationToken);
    }

    public Task DispatchToProgramGroupAsync(
        Guid programId,
        SyncEvent syncEvent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(syncEvent);
        return _hubContext.Clients
            .Group($"program:{programId}")
            .SendAsync(ISignalRSyncDispatcher.ClientEventName, syncEvent, cancellationToken);
    }
}
