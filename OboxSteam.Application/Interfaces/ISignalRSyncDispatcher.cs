using OboxSteam.Application.Realtime;

namespace OboxSteam.Application.Interfaces;

/// <summary>
/// Realtime sync push channel; implemented in the API layer over the shared
/// notifications hub (<c>/hubs/notifications</c>) so web and mobile keep one connection.
/// </summary>
public interface ISignalRSyncDispatcher
{
    /// <summary>Client-side event name for <see cref="SyncEvent"/> messages.</summary>
    const string ClientEventName = "syncEvent";

    Task DispatchToUsersAsync(
        IReadOnlyCollection<Guid> userIds,
        SyncEvent syncEvent,
        CancellationToken cancellationToken = default);

    /// <summary>Fan-out to a hub role group, e.g. <c>role:Manager</c>.</summary>
    Task DispatchToRoleGroupAsync(
        string role,
        SyncEvent syncEvent,
        CancellationToken cancellationToken = default);

    Task DispatchToProgramGroupAsync(
        Guid programId,
        SyncEvent syncEvent,
        CancellationToken cancellationToken = default);
}
