using OboxSteam.Application.Commons.CurriculumChanges;

namespace OboxSteam.Application.Interfaces;

/// <summary>
/// Turns pending curriculum entity changes of one save into change-log rows, the version
/// bump, approval auto-revoke, and the editing-session system message. Called by the
/// persistence layer before the save is flushed; everything it adds is written in the same
/// transaction. Notifications and advisory sync events are queued and only published once
/// the data is committed.
/// </summary>
public interface ICurriculumChangeRecorder
{
    Task RecordAsync(IReadOnlyList<CurriculumEntryChange> entries);

    /// <summary>Publishes notifications and advisory sync events queued by committed saves.</summary>
    Task FlushNotificationsAsync();

    /// <summary>Drops queued notifications and sync events after a failed save or a rolled-back transaction.</summary>
    void DiscardNotifications();
}
