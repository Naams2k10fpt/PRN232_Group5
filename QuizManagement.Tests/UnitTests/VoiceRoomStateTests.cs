using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;
using QuizManagement.Services;
using Xunit;

namespace QuizManagement.Tests.UnitTests;

public sealed class VoiceRoomStateTests
{
    private static VoiceRoomState State(int capacity = 6) => new(Options.Create(new VoiceRoomOptions { MaxParticipants = capacity }));

    [Fact]
    public void WatchersDoNotJoinOrEnableDevices_AndLeavingKeepsTheRoomSubscription()
    {
        var state = State();
        Assert.Empty(state.Watch(1, "alice", "a").Participants);
        var member = Assert.Single(state.Join(1, "alice", "Alice", "a").Participants);
        Assert.False(member.MicrophoneEnabled);
        Assert.False(member.CameraEnabled);
        Assert.Equal(1, state.Leave("a"));
        Assert.Empty(state.Snapshot(1).Participants);
        Assert.Single(state.Watchers(1));
        Assert.Null(state.Disconnect("a"));
        Assert.Empty(state.GroupIds());
    }

    [Fact]
    public void OneUserCannotJoinFromTwoDevicesOrRooms_AndCanJoinAfterDisconnect()
    {
        var state = State();
        state.Join(1, "alice", "Alice", "a");
        Assert.Single(state.Join(1, "alice", "Alice", "a").Participants);
        Assert.Throws<HubException>(() => state.Join(1, "alice", "Alice", "b"));
        Assert.Throws<HubException>(() => state.Join(2, "alice", "Alice", "c"));
        Assert.Throws<HubException>(() => state.Watch(2, "alice", "a"));
        state.Disconnect("a");
        Assert.Single(state.Join(2, "alice", "Alice", "c").Participants);
    }

    [Fact]
    public void ConcurrentJoinsNeverExceedCapacity_AndDisconnectFreesASeat()
    {
        var state = State(2);
        Parallel.For(0, 30, number =>
        {
            try { state.Join(1, $"user-{number}", "User", $"connection-{number}"); }
            catch (HubException) { }
        });
        var first = state.Snapshot(1);
        Assert.Equal(2, first.Participants.Length);
        state.Disconnect(first.Participants[0].ConnectionId);
        var after = state.Join(1, "new-user", "New user", "new-connection");
        Assert.Equal(2, after.Participants.Length);
        Assert.True(after.Version > first.Version);
    }

    [Fact]
    public void RemovingCheckedConnectionsDoesNotRemoveNewWatchersOrOtherRooms()
    {
        var state = State();
        state.Join(1, "alice", "Alice", "a");
        var checkedConnections = state.Watchers(1).Select(pair => pair.Key).ToArray();
        state.Join(1, "bob", "Bob", "b");
        state.Join(2, "other", "Other", "c");
        Assert.Equal(["a"], state.RemoveConnections(1, checkedConnections.Concat(["c"])));
        Assert.Equal("bob", Assert.Single(state.Snapshot(1).Participants).UserId);
        Assert.Single(state.Snapshot(2).Participants);
    }

    [Fact]
    public void CapacityIsPerChannelAndEachParticipantKeepsItsChannelIdentity()
    {
        var state = State(2);
        state.Join(1, "alice", "Alice", "a", 10);
        state.Join(1, "bob", "Bob", "b", 10);
        Assert.Throws<HubException>(() => state.Join(1, "carol", "Carol", "c", 10));
        state.Join(1, "carol", "Carol", "c", 11);
        Assert.Equal(3, state.Snapshot(1).Participants.Length);
        Assert.Equal(11, state.Participant("c")!.ChannelId);
    }

    [Fact]
    public void ClosingAnOldChannelCannotRemoveACallerWhoAlreadySwitchedChannels()
    {
        var state = State();
        state.Join(1, "alice", "Alice", "a", 10);
        var checkedParticipant = Assert.Single(state.Snapshot(1).Participants);
        state.Leave("a");
        state.Join(1, "alice", "Alice", "a", 11);

        Assert.Null(state.Leave(checkedParticipant.ConnectionId, checkedParticipant.ChannelId));
        Assert.Equal(11, Assert.Single(state.Snapshot(1).Participants).ChannelId);
        Assert.Equal(1, state.Leave("a", 11));
        Assert.Empty(state.Snapshot(1).Participants);
    }

    [Fact]
    public void OnlyJoinedConnectionsCanUpdateMedia_AndSnapshotsAreImmutable()
    {
        var state = State();
        state.Watch(1, "watcher", "w");
        Assert.Throws<HubException>(() => state.SetMedia("w", true, true));
        var before = state.Join(1, "alice", "Alice", "a");
        state.SetMedia("a", true, true);
        Assert.False(Assert.Single(before.Participants).MicrophoneEnabled);
        Assert.True(Assert.Single(state.Snapshot(1).Participants).MicrophoneEnabled);
        state.Leave("a");
        Assert.Throws<HubException>(() => state.SetMedia("a", false, false));
    }

    [Fact]
    public void TurnCredentialsExpireAndDoNotExposeTheSharedSecret()
    {
        var options = new VoiceRoomOptions { TurnUrls = ["turns:turn.example.test:5349"], TurnSharedSecret = "test-turn-secret" };
        Assert.True(options.IsValid());
        var server = options.ForUser("alice", DateTimeOffset.UnixEpoch).Last();
        Assert.Equal("3600:alice", server.Username);
        Assert.Equal("fNMsrZ6/D8Co1UtmXkOseAnc9Vs=", server.Credential);
        Assert.NotEqual(server.Credential, options.ForUser("bob", DateTimeOffset.UnixEpoch).Last().Credential);
        Assert.NotEqual(server.Credential, options.ForUser("alice", DateTimeOffset.UnixEpoch.AddHours(1)).Last().Credential);
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(9)]
    public void MeshCapacityMustStayWithinSupportedBounds(int capacity) =>
        Assert.False(new VoiceRoomOptions { MaxParticipants = capacity }.IsValid());

    [Fact]
    public void InvalidIceConfigurationFailsValidation()
    {
        Assert.False(new VoiceRoomOptions { IceServers = [new(["https://example.test"])] }.IsValid());
        Assert.False(new VoiceRoomOptions { TurnUrls = ["turn:example.test"] }.IsValid());
        Assert.False(new VoiceRoomOptions { RelayOnly = true }.IsValid());
    }

    [Fact]
    public void SignalingRejectsUnexpectedTypesOversizedSdpAndMixedPayloads()
    {
        Assert.True(new VoiceSignal("offer", "v=0\r\n").IsValid());
        Assert.True(new VoiceSignal("ice", Candidate: new("candidate:1", "0", 0)).IsValid());
        Assert.False(new VoiceSignal("other", "v=0").IsValid());
        Assert.False(new VoiceSignal("offer", "v=0" + new string('x', 65536)).IsValid());
        Assert.False(new VoiceSignal("offer", "v=0", new("candidate:1")).IsValid());
        Assert.False(new VoiceSignal("ice", Candidate: new("candidate:1", SdpMLineIndex: -1)).IsValid());
        Assert.False(new VoiceSignal("ice", Candidate: new(new string('x', 4097))).IsValid());
    }
}
