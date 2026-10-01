using OboxSteam.Application.Interfaces;

namespace OboxSteam.Application.Realtime;

public sealed class AdvisoryPresenceTracker : IAdvisoryPresenceTracker
{
    private readonly object _gate = new();
    private readonly Dictionary<(Guid ProgramId, Guid UserId), HashSet<string>> _connectionsByMember = new();
    private readonly Dictionary<string, Dictionary<Guid, Guid>> _membershipsByConnection = new();

    public void Join(Guid programId, Guid userId, string connectionId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionId);
        lock (_gate)
        {
            if (!_membershipsByConnection.TryGetValue(connectionId, out var memberships))
            {
                memberships = new Dictionary<Guid, Guid>();
                _membershipsByConnection[connectionId] = memberships;
            }

            if (memberships.TryGetValue(programId, out var previousUserId))
            {
                RemoveConnection(programId, previousUserId, connectionId);
            }

            memberships[programId] = userId;
            var key = (programId, userId);
            if (!_connectionsByMember.TryGetValue(key, out var connections))
            {
                connections = new HashSet<string>();
                _connectionsByMember[key] = connections;
            }

            connections.Add(connectionId);
        }
    }

    public void Leave(Guid programId, string connectionId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionId);
        lock (_gate)
        {
            if (!_membershipsByConnection.TryGetValue(connectionId, out var memberships)
                || !memberships.Remove(programId, out var userId))
            {
                return;
            }

            RemoveConnection(programId, userId, connectionId);
            if (memberships.Count == 0)
            {
                _membershipsByConnection.Remove(connectionId);
            }
        }
    }

    public void Disconnect(string connectionId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionId);
        lock (_gate)
        {
            if (!_membershipsByConnection.Remove(connectionId, out var memberships))
            {
                return;
            }

            foreach (var (programId, userId) in memberships)
            {
                RemoveConnection(programId, userId, connectionId);
            }
        }
    }

    public bool IsPresent(Guid programId, Guid userId)
    {
        lock (_gate)
        {
            return _connectionsByMember.ContainsKey((programId, userId));
        }
    }

    private void RemoveConnection(Guid programId, Guid userId, string connectionId)
    {
        var key = (programId, userId);
        if (_connectionsByMember.TryGetValue(key, out var connections)
            && connections.Remove(connectionId)
            && connections.Count == 0)
        {
            _connectionsByMember.Remove(key);
        }
    }
}
