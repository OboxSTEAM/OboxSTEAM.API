namespace OboxSteam.Application.Interfaces;

/// <summary>Finds sessions starting within the reminder lead window and publishes once.</summary>
public interface ISessionReminderPublisher
{
    /// <returns>Number of sessions that received a reminder in this pass.</returns>
    Task<int> PublishDueRemindersAsync(CancellationToken cancellationToken = default);
}
