using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using QuizManagement.Services;
using System.Security.Claims;

namespace QuizManagement.Hubs;

[Authorize]
public sealed class ChatHub : Hub
{
    private readonly StudyGroupService _groups;

    public ChatHub(StudyGroupService groups) => _groups = groups;

    public async Task JoinGroup(int groupId)
    {
        var userId = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null || !await _groups.IsMemberAsync(groupId, userId))
            throw new HubException("Bạn chưa tham gia nhóm này.");
        await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(groupId));
    }

    public Task LeaveGroup(int groupId) =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupName(groupId));

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

    public static string GroupName(int groupId) => $"study-group:{groupId}";
}
