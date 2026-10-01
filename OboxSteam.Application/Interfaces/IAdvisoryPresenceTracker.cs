namespace OboxSteam.Application.Interfaces;

/// <summary>
/// Tracks which users have a live hub connection in a program's advisory group.
/// In-memory and process-local: it only sees connections of this API instance.
/// </summary>
public interface IAdvisoryPresenceTracker
{
    void Join(Guid programId, Guid userId, string connectionId);

    void Leave(Guid programId, string connectionId);

    void Disconnect(string connectionId);

    bool IsPresent(Guid programId, Guid userId);
}
