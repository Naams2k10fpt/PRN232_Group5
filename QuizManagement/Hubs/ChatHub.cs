using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using QuizManagement.Services;
using System.Security.Claims;

namespace QuizManagement.Hubs;

[Authorize(AuthenticationSchemes =
    CookieAuthenticationDefaults.AuthenticationScheme + "," + JwtBearerDefaults.AuthenticationScheme)]
public sealed class ChatHub : Hub
{
    private readonly StudyGroupService _groups;
    private readonly RoomPresence _presence;
    private readonly PresenceBroadcaster _broadcaster;

    public ChatHub(StudyGroupService groups, RoomPresence presence, PresenceBroadcaster broadcaster)
    {
        _groups = groups;
        _presence = presence;
        _broadcaster = broadcaster;
    }

    public override async Task OnConnectedAsync()
    {
        var userId = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        var hasWebCookie = Context.GetHttpContext() is { } httpContext
            && (await httpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme)).Succeeded;
        if (userId is not null && hasWebCookie && _presence.WebJoin(userId, Context.ConnectionId))
            await _broadcaster.BroadcastWebsiteChangeAsync(userId);
        await base.OnConnectedAsync();
    }

    public async Task<RoomPresenceSnapshot> JoinGroup(int groupId)
    {
        var userId = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null || !await _groups.IsMemberAsync(groupId, userId))
            throw new HubException("Bạn chưa tham gia nhóm này.");
        var (snapshot, changed) = _presence.Join(groupId, userId, Context.ConnectionId);
        if (changed) await _broadcaster.BroadcastRoomAsync(snapshot);
        return snapshot;
    }

    public async Task LeaveGroup(int groupId)
    {
        var change = _presence.Leave(Context.ConnectionId, groupId);
        if (change is not null) await _broadcaster.BroadcastRoomAsync(change);
    }

    public async Task<RoomRoster> GetRoomRoster(int groupId)
    {
        var userId = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        var members = userId is null ? null : await _groups.MembersAsync(groupId, userId);
        if (members is null) throw new HubException("Bạn chưa tham gia nhóm này.");
        return new RoomRoster(members, _presence.Snapshot(groupId),
            _presence.WebSnapshot(groupId, members.Select(member => member.UserId)));
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        try
        {
            var roomChanges = _presence.Disconnect(Context.ConnectionId);
            _presence.WebDisconnect(Context.ConnectionId);
            foreach (var change in roomChanges)
                await _broadcaster.BroadcastRoomAsync(change);
        }
        finally
        {
            await base.OnDisconnectedAsync(exception);
        }
    }

    public async Task<ChatMessage> SendMessage(int groupId, string content)
    {
        var userId = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null) throw new HubException("Bạn cần đăng nhập.");
        if (string.IsNullOrWhiteSpace(content) || content.Trim().Length > 2000)
            throw new HubException("Tin nhắn phải có từ 1 đến 2000 ký tự.");

        var message = await _groups.SendAsync(groupId, userId, content);
        if (message is null) throw new HubException("Bạn chưa tham gia nhóm này.");
        // Resolve recipients for every send: a removed member's existing connection must not
        // continue receiving messages through an old SignalR group subscription.
        var recipients = await _groups.RecipientIdsAsync(groupId);
        await Clients.Users(recipients).SendAsync("ReceiveMessage", message);
        return message;
    }

}

public sealed record RoomRoster(List<GroupMemberSummary> Members, RoomPresenceSnapshot Presence,
    WebsitePresenceSnapshot Website);
