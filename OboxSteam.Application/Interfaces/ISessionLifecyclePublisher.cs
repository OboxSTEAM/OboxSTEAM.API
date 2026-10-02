namespace OboxSteam.Application.Interfaces;

/// <summary>
/// Clock-driven status moves for LiveOnline / Offline sessions:
/// Scheduled → InProgress at StartTime, Scheduled / InProgress → Completed at EndTime.
/// </summary>
public interface ISessionLifecyclePublisher
{
    /// <returns>Number of sessions moved to InProgress in this pass.</returns>
    Task<int> StartDueSessionsAsync(CancellationToken cancellationToken = default);

    /// <returns>Number of sessions moved to Completed in this pass.</returns>
    Task<int> CompleteElapsedSessionsAsync(CancellationToken cancellationToken = default);
}
