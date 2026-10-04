namespace QuizManagement.Services;

public sealed record RoomPresenceSnapshot(int GroupId, string[] OnlineUserIds, long Version);
public sealed record WebsitePresenceSnapshot(int GroupId, string[] ConnectedUserIds, long Version);
public sealed record WebsitePresenceChange(int GroupId, string UserId, bool Connected, long Version);

/// <summary>Tracks room viewers and website connections. Membership remains in the database.</summary>
public sealed class RoomPresence
{
    public static readonly TimeSpan DisconnectGrace = TimeSpan.FromSeconds(5);

    private readonly TimeProvider _clock;
    private readonly Lock _sync = new();
    private readonly Dictionary<int, Dictionary<string, HashSet<string>>> _rooms = [];
    private readonly Dictionary<string, Dictionary<int, string>> _connections = [];
    private readonly Dictionary<int, long> _versions = [];
    private readonly Dictionary<string, HashSet<string>> _webUsers = [];
    private readonly Dictionary<string, string> _webConnections = [];
    private readonly Dictionary<string, DateTimeOffset> _pendingWebDisconnects = [];
    private long _webVersion;

    public RoomPresence(TimeProvider? clock = null) => _clock = clock ?? TimeProvider.System;

    public bool WebJoin(string userId, string connectionId)
    {
        lock (_sync)
        {
            if (_webConnections.ContainsKey(connectionId)) return false;
            _webConnections[connectionId] = userId;
            if (!_webUsers.TryGetValue(userId, out var connections))
                _webUsers[userId] = connections = [];
            var wasPresent = connections.Count > 0 || _pendingWebDisconnects.Remove(userId);
            connections.Add(connectionId);
            if (wasPresent) return false;
            _webVersion++;
            return true;
        }
    }

    public void WebDisconnect(string connectionId)
    {
        lock (_sync)
        {
            if (!_webConnections.Remove(connectionId, out var userId)) return;
            var connections = _webUsers[userId];
            connections.Remove(connectionId);
            if (connections.Count == 0)
                _pendingWebDisconnects[userId] = _clock.GetUtcNow() + DisconnectGrace;
        }
    }

    public List<string> ExpireWebDisconnects()
    {
        lock (_sync)
        {
            var expired = _pendingWebDisconnects
                .Where(pair => pair.Value <= _clock.GetUtcNow())
                .Select(pair => pair.Key).ToList();
            foreach (var userId in expired)
            {
                _pendingWebDisconnects.Remove(userId);
                _webUsers.Remove(userId);
                _webVersion++;
            }
            return expired;
        }
    }

    public WebsitePresenceSnapshot WebSnapshot(int groupId, IEnumerable<string> memberIds)
    {
        lock (_sync)
            return new(groupId, memberIds.Where(_webUsers.ContainsKey).ToArray(), _webVersion);
    }

    public WebsitePresenceChange WebChange(int groupId, string userId)
    {
        lock (_sync)
            return new(groupId, userId, _webUsers.ContainsKey(userId), _webVersion);
    }

    public string[] ViewerUserIds(int groupId)
    {
        lock (_sync)
            return _rooms.TryGetValue(groupId, out var participants)
                ? participants.Keys.ToArray() : [];
    }

    public string[] ViewerConnectionIds(int groupId, IEnumerable<string> eligibleUserIds)
    {
        lock (_sync)
        {
            if (!_rooms.TryGetValue(groupId, out var participants)) return [];
            return eligibleUserIds.Where(participants.ContainsKey)
                .SelectMany(userId => participants[userId]).ToArray();
        }
    }

    public (RoomPresenceSnapshot Snapshot, bool Changed) Join(
        int groupId, string userId, string connectionId)
    {
        lock (_sync)
        {
            if (!_connections.TryGetValue(connectionId, out var joinedRooms))
                _connections[connectionId] = joinedRooms = [];
            if (joinedRooms.ContainsKey(groupId)) return (SnapshotCore(groupId), false);

            joinedRooms[groupId] = userId;
            if (!_rooms.TryGetValue(groupId, out var participants))
                _rooms[groupId] = participants = [];
            var entered = !participants.TryGetValue(userId, out var connections);
            if (entered) participants[userId] = connections = [];
            connections!.Add(connectionId);

            if (entered) _versions[groupId] = _versions.GetValueOrDefault(groupId) + 1;
            return (SnapshotCore(groupId), entered);
        }
    }

    public RoomPresenceSnapshot? Leave(string connectionId, int groupId)
    {
        lock (_sync) return LeaveCore(connectionId, groupId);
    }

    public List<RoomPresenceSnapshot> Disconnect(string connectionId)
    {
        lock (_sync)
        {
            if (!_connections.TryGetValue(connectionId, out var joinedRooms)) return [];
            var changes = new List<RoomPresenceSnapshot>();
            foreach (var groupId in joinedRooms.Keys.ToArray())
            {
                var change = LeaveCore(connectionId, groupId);
                if (change is not null) changes.Add(change);
            }
            return changes;
        }
    }

    public RoomPresenceSnapshot Snapshot(int groupId)
    {
        lock (_sync) return SnapshotCore(groupId);
    }

    private RoomPresenceSnapshot? LeaveCore(string connectionId, int groupId)
    {
        if (!_connections.TryGetValue(connectionId, out var joinedRooms)
            || !joinedRooms.Remove(groupId, out var userId)) return null;
        if (joinedRooms.Count == 0) _connections.Remove(connectionId);
        var participants = _rooms[groupId];
        var connections = participants[userId];
        connections.Remove(connectionId);
        if (connections.Count > 0) return null;

        participants.Remove(userId);
        if (participants.Count == 0) _rooms.Remove(groupId);
        _versions[groupId] = _versions.GetValueOrDefault(groupId) + 1;
        return SnapshotCore(groupId);
    }

    private RoomPresenceSnapshot SnapshotCore(int groupId) => new(groupId,
        _rooms.TryGetValue(groupId, out var participants) ? participants.Keys.ToArray() : [],
        _versions.GetValueOrDefault(groupId));
}
