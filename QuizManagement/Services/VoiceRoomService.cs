using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;
using QuizManagement.Hubs;
using BusinessObjects;
using DataAccessObjects;
using Microsoft.EntityFrameworkCore;

namespace QuizManagement.Services;

public sealed record VoiceJoinResult(string ParticipantId, int ChannelId, VoiceRoomSnapshot Room,
    List<VoiceIceServer> IceServers, bool RelayOnly);
public sealed record VoiceIceConfiguration(List<VoiceIceServer> IceServers, bool RelayOnly);
public sealed record VoiceIceCandidate(string Candidate, string? SdpMid = null,
    int? SdpMLineIndex = null, string? UsernameFragment = null);
public sealed record VoiceSignal(string Type, string? Sdp = null, VoiceIceCandidate? Candidate = null)
{
    public bool IsValid() => Type switch
    {
        "offer" or "answer" => Candidate is null && Sdp is { Length: > 0 and <= 65536 }
            && Sdp.StartsWith("v=0", StringComparison.Ordinal),
        "ice" => Sdp is null && Candidate is { Candidate.Length: >= 0 and <= 4096 }
            && Candidate.SdpMid?.Length is not > 256
            && Candidate.SdpMLineIndex is null or >= 0 and <= 32
            && Candidate.UsernameFragment?.Length is not > 256,
        _ => false
    };
}

public sealed class VoiceRoomService(StudyGroupService groups, QuizManagementDbContext db, VoiceRoomState state,
    IHubContext<VoiceHub> hub, IOptions<VoiceRoomOptions> options, TimeProvider clock)
{
    public async Task<VoiceRoomSnapshot> WatchAsync(int groupId, string userId, string connectionId)
    {
        await EnsureMemberAsync(groupId, userId);
        return state.Watch(groupId, userId, connectionId);
    }

    public async Task<VoiceJoinResult> JoinAsync(int groupId, int channelId, string userId, string displayName, string connectionId)
    {
        await EnsureMemberAsync(groupId, userId);
        await EnsureChannelAsync(groupId, channelId);
        state.Join(groupId, userId, displayName, connectionId, channelId);
        await SynchronizeAsync(groupId);
        return new(connectionId, channelId, state.Snapshot(groupId), options.Value.ForUser(userId, clock.GetUtcNow()),
            options.Value.RelayOnly);
    }

    public async Task SetMediaAsync(int groupId, string userId, string connectionId, bool microphone, bool camera)
    {
        await EnsureMemberAsync(groupId, userId);
        var participant = EnsureParticipant(groupId, userId, connectionId);
        await EnsureChannelAsync(groupId, participant.ChannelId);
        state.SetMedia(connectionId, microphone, camera);
        await SynchronizeAsync(groupId);
    }

    public async Task SignalAsync(int groupId, string userId, string connectionId, string targetId, VoiceSignal signal)
    {
        if (signal is null || !signal.IsValid() || string.IsNullOrWhiteSpace(targetId)
            || targetId.Length > 128 || targetId == connectionId)
            throw new HubException("Tín hiệu cuộc gọi không hợp lệ.");
        var sender = EnsureParticipant(groupId, userId, connectionId);
        var target = state.Session(targetId);
        if (target is null || target.GroupId != groupId || state.Participant(targetId) is not { } targetParticipant
            || targetParticipant.ChannelId != sender.ChannelId)
            throw new HubException("Người nhận không còn ở phòng voice này.");
        await EnsureChannelAsync(groupId, targetParticipant.ChannelId);
        // Never trust a stale SignalR subscription after a ban, leave or account disable.
        var eligible = await groups.EligibleViewerIdsAsync(groupId, [userId, target.UserId]);
        if (!eligible.Contains(userId) || !eligible.Contains(target.UserId))
        {
            await SynchronizeAsync(groupId);
            throw new HubException("Quyền tham gia phòng voice đã thay đổi.");
        }
        if (EnsureParticipant(groupId, userId, connectionId).ChannelId != sender.ChannelId)
            throw new HubException("Bạn đã chuyển sang phòng voice khác.");
        if (state.Participant(targetId)?.ChannelId != sender.ChannelId) throw new HubException("Người nhận đã rời phòng.");
        await hub.Clients.Client(targetId).SendAsync("VoiceSignal", groupId, connectionId, signal);
    }

    public async Task LeaveAsync(string connectionId)
    {
        if (state.Leave(connectionId) is { } groupId) await SynchronizeAsync(groupId);
    }

    public async Task DisconnectAsync(string connectionId)
    {
        if (state.Disconnect(connectionId) is { } groupId) await SynchronizeAsync(groupId);
    }

    public async Task RevokeAsync(int groupId, string userId)
    {
        var removed = state.RemoveConnections(groupId,
            state.Watchers(groupId).Where(pair => pair.Value == userId).Select(pair => pair.Key));
        if (removed.Length > 0) await hub.Clients.Clients(removed).SendAsync("VoiceAccessRevoked", groupId);
        await SynchronizeAsync(groupId);
    }

    public async Task CloseChannelAsync(int groupId, int channelId)
    {
        var callers = state.Snapshot(groupId).Participants.Where(member => member.ChannelId == channelId
            && state.Leave(member.ConnectionId, channelId).HasValue).ToArray();
        if (callers.Length > 0)
            await hub.Clients.Clients(callers.Select(member => member.ConnectionId).ToArray())
                .SendAsync("VoiceChannelClosed", groupId, channelId);
        await SynchronizeAsync(groupId);
    }

    public async Task<VoiceIceConfiguration> IceConfigurationAsync(int groupId, string userId, string connectionId)
    {
        await EnsureMemberAsync(groupId, userId);
        var participant = EnsureParticipant(groupId, userId, connectionId);
        await EnsureChannelAsync(groupId, participant.ChannelId);
        return new(options.Value.ForUser(userId, clock.GetUtcNow()), options.Value.RelayOnly);
    }

    public async Task SynchronizeAsync(int groupId, CancellationToken cancellationToken = default)
    {
        var watchers = state.Watchers(groupId);
        if (watchers.Length == 0) return;
        var eligible = (await groups.EligibleViewerIdsAsync(groupId, watchers.Select(pair => pair.Value).Distinct().ToArray()))
            .ToHashSet(StringComparer.Ordinal);
        // Only prune connections from the checked snapshot: a new watcher may have joined during the DB read.
        var removed = state.RemoveConnections(groupId,
            watchers.Where(pair => !eligible.Contains(pair.Value)).Select(pair => pair.Key));
        if (removed.Length > 0)
            await hub.Clients.Clients(removed).SendAsync("VoiceAccessRevoked", groupId, cancellationToken);
        var participants = state.Snapshot(groupId).Participants;
        if (participants.Length > 0)
        {
            var activeChannels = await db.ChatChannels.AsNoTracking().Where(channel => channel.GroupId == groupId
                && channel.Kind == ChatChannelKind.Voice && !channel.IsArchived).Select(channel => channel.Id)
                .ToListAsync(cancellationToken);
            foreach (var closed in participants.Where(member => !activeChannels.Contains(member.ChannelId)).GroupBy(member => member.ChannelId))
            {
                var departed = closed.Where(member => state.Leave(member.ConnectionId, closed.Key).HasValue)
                    .Select(member => member.ConnectionId).ToArray();
                if (departed.Length > 0)
                    await hub.Clients.Clients(departed).SendAsync("VoiceChannelClosed", groupId, closed.Key, cancellationToken);
            }
        }
        var recipients = state.Watchers(groupId).Select(pair => pair.Key).ToArray();
        if (recipients.Length > 0)
            await hub.Clients.Clients(recipients).SendAsync("VoiceRoomChanged", state.Snapshot(groupId), cancellationToken);
    }

    private async Task EnsureMemberAsync(int groupId, string userId)
    {
        if (!await groups.IsMemberAsync(groupId, userId))
            throw new HubException("Bạn cần là thành viên của nhóm để vào phòng voice.");
    }

    private async Task EnsureChannelAsync(int groupId, int channelId)
    {
        if (!await db.ChatChannels.AsNoTracking().AnyAsync(channel => channel.Id == channelId
            && channel.GroupId == groupId && channel.Kind == ChatChannelKind.Voice && !channel.IsArchived))
            throw new HubException("Kênh voice không tồn tại hoặc đã được lưu trữ.");
    }

    private VoiceParticipant EnsureParticipant(int groupId, string userId, string connectionId)
    {
        var session = state.Session(connectionId);
        var participant = state.Participant(connectionId);
        if (session?.GroupId != groupId || session.UserId != userId || participant is null)
            throw new HubException("Hãy tham gia đúng phòng voice trước.");
        return participant;
    }
}
