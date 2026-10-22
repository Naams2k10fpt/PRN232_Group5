using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using QuizManagement.Services;
using System.Security.Claims;

namespace QuizManagement.Hubs;

[Authorize(AuthenticationSchemes = CookieAuthenticationDefaults.AuthenticationScheme + "," + JwtBearerDefaults.AuthenticationScheme)]
public sealed class VoiceHub(VoiceRoomService voice) : Hub
{
    public Task<VoiceRoomSnapshot> Watch(int groupId) => voice.WatchAsync(groupId, UserId(), Context.ConnectionId);
    public Task<VoiceJoinResult> Join(int groupId, int channelId) => voice.JoinAsync(groupId, channelId, UserId(),
        Context.User?.Identity?.Name ?? "Thành viên", Context.ConnectionId);
    public Task SetMedia(int groupId, bool microphoneEnabled, bool cameraEnabled) =>
        voice.SetMediaAsync(groupId, UserId(), Context.ConnectionId, microphoneEnabled, cameraEnabled);
    public Task Signal(int groupId, string targetId, VoiceSignal signal) =>
        voice.SignalAsync(groupId, UserId(), Context.ConnectionId, targetId, signal);
    public Task Leave() => voice.LeaveAsync(Context.ConnectionId);
    public Task<VoiceIceConfiguration> IceConfiguration(int groupId) =>
        voice.IceConfigurationAsync(groupId, UserId(), Context.ConnectionId);

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        try { await voice.DisconnectAsync(Context.ConnectionId); }
        finally { await base.OnDisconnectedAsync(exception); }
    }

    private string UserId() => Context.User?.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? throw new HubException("Bạn cần đăng nhập.");
}
