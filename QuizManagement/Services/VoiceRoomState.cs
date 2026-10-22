using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;

namespace QuizManagement.Services;

public sealed record VoiceParticipant(string ConnectionId, string UserId, string DisplayName,
    bool MicrophoneEnabled = false, bool CameraEnabled = false, int ChannelId = 0);
public sealed record VoiceRoomSnapshot(int GroupId, VoiceParticipant[] Participants, int MaxParticipants, long Version);
public sealed record VoiceSession(int GroupId, string UserId);

/// <summary>Ephemeral call membership, separate from chat viewing and database membership.</summary>
public sealed class VoiceRoomState(IOptions<VoiceRoomOptions> options)
{
    private readonly Lock _sync = new();
    private readonly Dictionary<string, VoiceSession> _sessions = [];
    private readonly Dictionary<int, Dictionary<string, string>> _watchers = [];
    private readonly Dictionary<int, Dictionary<string, VoiceParticipant>> _rooms = [];
    private readonly Dictionary<string, string> _userConnections = [];
    private long _version;
    public int MaxParticipants { get; } = options.Value.MaxParticipants;

    public VoiceRoomSnapshot Watch(int groupId, string userId, string connectionId)
    {
        lock (_sync)
        {
            WatchCore(groupId, userId, connectionId);
            return SnapshotCore(groupId);
        }
    }

    public VoiceRoomSnapshot Join(int groupId, string userId, string displayName, string connectionId, int channelId = 0)
    {
        lock (_sync)
        {
            WatchCore(groupId, userId, connectionId);
            if (_userConnections.TryGetValue(userId, out var existing))
            {
                if (existing == connectionId && _rooms[groupId][connectionId].ChannelId == channelId) return SnapshotCore(groupId);
                throw new HubException("Bạn đang ở phòng voice trên một tab hoặc thiết bị khác. Hãy rời phòng đó trước.");
            }
            if (_rooms.TryGetValue(groupId, out var room) && room.Values.Count(member => member.ChannelId == channelId) >= MaxParticipants)
                throw new HubException($"Phòng voice đã đủ {MaxParticipants} người.");
            if (room is null) _rooms[groupId] = room = [];
            room.Add(connectionId, new(connectionId, userId, displayName, ChannelId: channelId));
            _userConnections.Add(userId, connectionId);
            _version++;
            return SnapshotCore(groupId);
        }
    }

    public VoiceSession? Session(string connectionId)
    {
        lock (_sync) return _sessions.GetValueOrDefault(connectionId);
    }

    public VoiceParticipant? Participant(string connectionId)
    {
        lock (_sync)
            return _sessions.TryGetValue(connectionId, out var session)
                && _rooms.TryGetValue(session.GroupId, out var room)
                ? room.GetValueOrDefault(connectionId) : null;
    }

    public void SetMedia(string connectionId, bool microphoneEnabled, bool cameraEnabled)
    {
        lock (_sync)
        {
            if (!_sessions.TryGetValue(connectionId, out var session)
                || !_rooms.TryGetValue(session.GroupId, out var room)
                || !room.TryGetValue(connectionId, out var participant))
                throw new HubException("Hãy tham gia phòng voice trước.");
            if (participant.MicrophoneEnabled == microphoneEnabled && participant.CameraEnabled == cameraEnabled) return;
            room[connectionId] = participant with { MicrophoneEnabled = microphoneEnabled, CameraEnabled = cameraEnabled };
            _version++;
        }
    }

    public int? Leave(string connectionId, int? expectedChannelId = null)
    {
        lock (_sync)
            return _sessions.TryGetValue(connectionId, out var session)
                && (!expectedChannelId.HasValue || Participant(connectionId)?.ChannelId == expectedChannelId)
                && LeaveCore(session.GroupId, connectionId)
                ? session.GroupId : null;
    }

    public int? Disconnect(string connectionId)
    {
        lock (_sync)
        {
            if (!_sessions.Remove(connectionId, out var session)) return null;
            var changed = LeaveCore(session.GroupId, connectionId);
            if (_watchers.TryGetValue(session.GroupId, out var watchers))
            {
                watchers.Remove(connectionId);
                if (watchers.Count == 0) _watchers.Remove(session.GroupId);
            }
            return changed ? session.GroupId : null;
        }
    }

    public string[] RemoveConnections(int groupId, IEnumerable<string> connectionIds)
    {
        lock (_sync)
        {
            var removed = new List<string>();
            foreach (var connectionId in connectionIds.ToArray())
            {
                if (!_sessions.TryGetValue(connectionId, out var session) || session.GroupId != groupId) continue;
                Disconnect(connectionId);
                removed.Add(connectionId);
            }
            return removed.ToArray();
        }
    }

    public KeyValuePair<string, string>[] Watchers(int groupId)
    {
        lock (_sync) return _watchers.TryGetValue(groupId, out var watchers) ? watchers.ToArray() : [];
    }

    public int[] GroupIds() { lock (_sync) return _watchers.Keys.ToArray(); }
    public VoiceRoomSnapshot Snapshot(int groupId) { lock (_sync) return SnapshotCore(groupId); }

    private void WatchCore(int groupId, string userId, string connectionId)
    {
        if (_sessions.TryGetValue(connectionId, out var session))
        {
            if (session.GroupId != groupId || session.UserId != userId)
                throw new HubException("Kết nối voice này thuộc nhóm khác.");
            return;
        }
        _sessions[connectionId] = new(groupId, userId);
        if (!_watchers.TryGetValue(groupId, out var watchers)) _watchers[groupId] = watchers = [];
        watchers[connectionId] = userId;
    }

    private bool LeaveCore(int groupId, string connectionId)
    {
        if (!_rooms.TryGetValue(groupId, out var room) || !room.Remove(connectionId, out var participant)) return false;
        _userConnections.Remove(participant.UserId);
        if (room.Count == 0) _rooms.Remove(groupId);
        _version++;
        return true;
    }

    private VoiceRoomSnapshot SnapshotCore(int groupId) => new(groupId,
        _rooms.TryGetValue(groupId, out var room) ? room.Values.OrderBy(member => member.ConnectionId).ToArray() : [],
        MaxParticipants, _version);
}
