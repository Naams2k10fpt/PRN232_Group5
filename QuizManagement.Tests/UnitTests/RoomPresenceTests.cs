using QuizManagement.Services;
using Xunit;

namespace QuizManagement.Tests.UnitTests;

public sealed class RoomPresenceTests
{
    [Fact]
    public void MultipleTabs_CountOneOnlineMemberAndOneExit()
    {
        var presence = new RoomPresence();
        var (first, entered) = presence.Join(7, "alice", "tab-1");
        Assert.True(entered);
        Assert.Equal(["alice"], first.OnlineUserIds);

        var (second, repeated) = presence.Join(7, "alice", "tab-2");
        Assert.False(repeated);
        Assert.Equal(["alice"], second.OnlineUserIds);
        Assert.Null(presence.Leave("tab-1", 7));
        Assert.Equal(["alice"], presence.Snapshot(7).OnlineUserIds);

        var exits = presence.Disconnect("tab-2");
        var exit = Assert.Single(exits);
        Assert.Empty(exit.OnlineUserIds);
        Assert.Empty(presence.Snapshot(7).OnlineUserIds);
        Assert.Empty(presence.Disconnect("tab-2"));
    }

    [Fact]
    public void Presence_IsIndependentForEachRoom()
    {
        var presence = new RoomPresence();
        presence.Join(7, "alice", "tab-1");
        presence.Join(8, "alice", "tab-1");
        presence.Join(7, "bob", "tab-2");

        Assert.Equal(2, presence.Snapshot(7).OnlineUserIds.Length);
        Assert.Single(presence.Snapshot(8).OnlineUserIds);
        presence.Disconnect("tab-1");
        Assert.Equal(["bob"], presence.Snapshot(7).OnlineUserIds);
        Assert.Empty(presence.Snapshot(8).OnlineUserIds);
    }

    [Fact]
    public void LeavingChatStaysWaitingWhileAnotherWebTabIsOpen()
    {
        var clock = new ManualClock();
        var presence = new RoomPresence(clock);
        Assert.True(presence.WebJoin("alice", "site-tab"));
        Assert.False(presence.WebJoin("alice", "chat-tab"));
        Assert.True(presence.WebJoin("bob", "bob-tab"));
        presence.Join(7, "alice", "chat-tab");

        Assert.Equal(["alice"], presence.Snapshot(7).OnlineUserIds);
        Assert.Equal(["alice", "bob"], presence.WebSnapshot(7, ["alice", "bob", "outsider"]).ConnectedUserIds);

        presence.Disconnect("chat-tab");
        presence.WebDisconnect("chat-tab");
        Assert.Empty(presence.Snapshot(7).OnlineUserIds);
        Assert.Equal(["alice", "bob"], presence.WebSnapshot(7, ["alice", "bob"]).ConnectedUserIds);

        presence.WebDisconnect("site-tab");
        Assert.Equal(["alice", "bob"], presence.WebSnapshot(7, ["alice", "bob"]).ConnectedUserIds);
        clock.Advance(RoomPresence.DisconnectGrace);
        Assert.Equal(["alice"], presence.ExpireWebDisconnects());
        Assert.Equal(["bob"], presence.WebSnapshot(7, ["alice", "bob"]).ConnectedUserIds);
    }

    [Fact]
    public void ReconnectingDuringGraceDoesNotBroadcastAnOfflineTransition()
    {
        var clock = new ManualClock();
        var presence = new RoomPresence(clock);
        Assert.True(presence.WebJoin("alice", "old-page"));
        var version = presence.WebSnapshot(7, ["alice"]).Version;

        presence.WebDisconnect("old-page");
        Assert.False(presence.WebJoin("alice", "new-page"));
        clock.Advance(RoomPresence.DisconnectGrace);

        Assert.Empty(presence.ExpireWebDisconnects());
        Assert.Equal(version, presence.WebSnapshot(7, ["alice"]).Version);
        Assert.True(presence.WebChange(7, "alice").Connected);
    }

    [Fact]
    public void PresenceUpdatesTargetOnlyEligibleRoomViewers()
    {
        var presence = new RoomPresence();
        presence.Join(7, "alice", "alice-tab");
        presence.Join(7, "bob", "bob-tab");
        presence.Join(8, "carol", "carol-tab");

        Assert.Equal(["alice-tab"], presence.ViewerConnectionIds(7, ["alice"]));
        Assert.Empty(presence.ViewerConnectionIds(7, ["carol"]));
    }

    private sealed class ManualClock : TimeProvider
    {
        private DateTimeOffset _now = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(TimeSpan duration) => _now += duration;
    }
}
