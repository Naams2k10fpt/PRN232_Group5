using Microsoft.AspNetCore.SignalR;
using QuizManagement.Hubs;

namespace QuizManagement.Services;

/// <summary>Sends presence only to current room viewers who still have access.</summary>
public sealed class PresenceBroadcaster
{
    private readonly RoomPresence _presence;
    private readonly IServiceScopeFactory _scopes;
    private readonly IHubContext<ChatHub> _hub;

    public PresenceBroadcaster(RoomPresence presence, IServiceScopeFactory scopes, IHubContext<ChatHub> hub)
    {
        _presence = presence;
        _scopes = scopes;
        _hub = hub;
    }

    public async Task BroadcastRoomAsync(RoomPresenceSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        var viewers = _presence.ViewerUserIds(snapshot.GroupId);
        if (viewers.Length == 0) return;
        using var scope = _scopes.CreateScope();
        var groups = scope.ServiceProvider.GetRequiredService<StudyGroupService>();
        var eligible = await groups.EligibleViewerIdsAsync(snapshot.GroupId, viewers);
        var connections = _presence.ViewerConnectionIds(snapshot.GroupId, eligible);
        if (connections.Length > 0)
            await _hub.Clients.Clients(connections).SendAsync("RoomPresenceChanged", snapshot, cancellationToken);
    }

    public async Task BroadcastWebsiteChangeAsync(string userId, CancellationToken cancellationToken = default)
    {
        using var scope = _scopes.CreateScope();
        var groups = scope.ServiceProvider.GetRequiredService<StudyGroupService>();
        foreach (var groupId in await groups.GroupIdsForUserAsync(userId))
        {
            var viewers = _presence.ViewerUserIds(groupId);
            if (viewers.Length == 0) continue;
            var eligible = await groups.EligibleViewerIdsAsync(groupId, viewers);
            var connections = _presence.ViewerConnectionIds(groupId, eligible);
            if (connections.Length > 0)
                await _hub.Clients.Clients(connections).SendAsync("WebsitePresenceDelta",
                    _presence.WebChange(groupId, userId), cancellationToken);
        }
    }
}
