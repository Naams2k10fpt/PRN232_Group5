using System.Net;
using System.Net.Http.Json;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using BusinessObjects;
using DataAccessObjects;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using QuizManagement.Services;
using QuizManagement.Tests.ApiTests;
using Xunit;

namespace QuizManagement.Tests.IntegrationTests;

public sealed class VoiceRoomHubTests
{
    [Fact, Trait("Category", "PostgresIntegration")]
    public async Task RealHubEnforcesChannelMembershipCapacityAndImmediateRevocation()
    {
        var options = new DbContextOptionsBuilder<QuizManagementDbContext>()
            .UseNpgsql(PostgresTestConnection.ForDatabase($"QuizManagementTests_{Guid.NewGuid():N}")).Options;
        await using var db = new QuizManagementDbContext(options);
        try
        {
            await db.Database.MigrateAsync();
            using var host = new ApiTestHost
            {
                ConfigureServices = services =>
                {
                    services.RemoveAll<QuizManagementDbContext>();
                    services.AddScoped(_ => new QuizManagementDbContext(options));
                    services.PostConfigure<VoiceRoomOptions>(settings => settings.MaxParticipants = 2);
                }
            };
            var owner = host.AddUser("owner@voice.test");
            var member = host.AddUser("member@voice.test");
            var observer = host.AddUser("observer@voice.test");
            var outsider = host.AddUser("outsider@voice.test");
            db.Users.AddRange(owner, member, observer, outsider);
            await db.SaveChangesAsync();
            using var ownerHttp = host.CreateClient(new() { AllowAutoRedirect = false });
            using var memberHttp = host.CreateClient(new() { AllowAutoRedirect = false });
            using var observerHttp = host.CreateClient(new() { AllowAutoRedirect = false });
            using var outsiderHttp = host.CreateClient(new() { AllowAutoRedirect = false });
            Assert.Equal(HttpStatusCode.Unauthorized,
                (await ownerHttp.PostAsync("/hubs/voice/negotiate?negotiateVersion=1", null)).StatusCode);
            var ownerToken = await host.LoginAsync(ownerHttp, owner.Email);
            var memberToken = await host.LoginAsync(memberHttp, member.Email);
            var observerToken = await host.LoginAsync(observerHttp, observer.Email);
            var outsiderToken = await host.LoginAsync(outsiderHttp, outsider.Email);
            ApiTestHost.UseToken(ownerHttp, ownerToken);
            ApiTestHost.UseToken(memberHttp, memberToken);
            ApiTestHost.UseToken(observerHttp, observerToken);
            ApiTestHost.UseToken(outsiderHttp, outsiderToken);

            var group = await Json(await ownerHttp.PostAsJsonAsync("/api/groups", new { name = "Voice study" }));
            var id = group.GetProperty("id").GetInt32();
            var path = $"/api/groups/{id}";
            (await memberHttp.PostAsJsonAsync(path + "/join", new { })).EnsureSuccessStatusCode();
            (await observerHttp.PostAsJsonAsync(path + "/join", new { })).EnsureSuccessStatusCode();
            var channels = await Json(await ownerHttp.GetAsync(path + "/channels"));
            Assert.Equal(2, channels.GetArrayLength());
            Assert.All(channels.EnumerateArray(), channel => Assert.True(channel.GetProperty("isDefault").GetBoolean()));
            var textId = channels.EnumerateArray().Single(channel => channel.GetProperty("kind").GetInt32() == 0).GetProperty("id").GetInt32();
            var voiceId = channels.EnumerateArray().Single(channel => channel.GetProperty("kind").GetInt32() == 1).GetProperty("id").GetInt32();
            // Text and voice may share a name, but a channel's type cannot change after creation.
            var extra = await Json(await ownerHttp.PostAsJsonAsync(path + "/channels", new { name = "chung", kind = 1 }));
            var extraId = extra.GetProperty("id").GetInt32();
            Assert.Equal(HttpStatusCode.BadRequest, (await ownerHttp.PutAsJsonAsync(path + $"/channels/{extraId}", new { name = "chung", kind = 0 })).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await ownerHttp.PostAsJsonAsync(path + "/channels", new { name = "invalid", kind = 9 })).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await memberHttp.GetAsync(path + $"/channels/{voiceId}/messages")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await memberHttp.PostAsJsonAsync(path + "/channels", new { name = "no-permission", kind = 1 })).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await ownerHttp.DeleteAsync(path + $"/channels/{voiceId}")).StatusCode);

            using var a = await HubClient.Connect(host, ownerToken);
            using var b = await HubClient.Connect(host, memberToken);
            using var c = await HubClient.Connect(host, observerToken);
            using var d = await HubClient.Connect(host, outsiderToken);
            await Assert.ThrowsAsync<HubException>(() => d.Invoke("Watch", id));
            await a.Invoke("Watch", id); await b.Invoke("Watch", id); await c.Invoke("Watch", id);
            await Assert.ThrowsAsync<HubException>(() => a.Invoke("Join", id, textId));
            var aJoin = await a.Invoke("Join", id, voiceId);
            var aId = aJoin.GetProperty("participantId").GetString()!;
            Assert.False(aJoin.GetProperty("room").GetProperty("participants")[0].GetProperty("microphoneEnabled").GetBoolean());
            var bJoin = await b.Invoke("Join", id, extraId);
            var bId = bJoin.GetProperty("participantId").GetString()!;
            var offer = new { type = "offer", sdp = "v=0\r\ns=synthetic-signaling-test\r\n" };
            await Assert.ThrowsAsync<HubException>(() => a.Invoke("Signal", id, bId, offer));
            await b.Invoke("Leave"); await b.Invoke("Join", id, voiceId);
            await Assert.ThrowsAsync<HubException>(() => c.Invoke("Join", id, voiceId));
            await Assert.ThrowsAsync<HubException>(() => c.Invoke("SetMedia", id, true, true));
            await Assert.ThrowsAsync<HubException>(() => c.Invoke("IceConfiguration", id));
            await a.Invoke("Signal", id, bId, offer);
            var delivered = await b.Event("VoiceSignal");
            Assert.Equal(aId, delivered[1].GetString());
            Assert.Equal(offer.sdp, delivered[2].GetProperty("sdp").GetString());
            await a.Invoke("SetMedia", id, true, true);
            var state = host.Services.GetRequiredService<VoiceRoomState>();
            Assert.True(state.Participant(aId)!.MicrophoneEnabled);
            Assert.True(state.Participant(aId)!.CameraEnabled);
            await Assert.ThrowsAsync<HubException>(() => a.Invoke("Signal", id, bId, new { type = "offer", sdp = "not-sdp" }));

            await b.Invoke("Leave"); await b.Invoke("Join", id, extraId);
            (await ownerHttp.DeleteAsync(path + $"/channels/{extraId}")).EnsureSuccessStatusCode();
            var closed = await b.Event("VoiceChannelClosed");
            Assert.Equal(extraId, closed[1].GetInt32());
            Assert.Single(state.Snapshot(id).Participants);
            await Assert.ThrowsAsync<HubException>(() => b.Invoke("Join", id, extraId));
            await b.Invoke("Join", id, voiceId);
            (await ownerHttp.DeleteAsync(path + $"/members/{member.Id}")).EnsureSuccessStatusCode();
            await b.Event("VoiceAccessRevoked");
            Assert.Null(state.Participant(bId));
            await Assert.ThrowsAsync<HubException>(() => b.Invoke("Signal", id, aId, offer));
            Assert.Single(state.Snapshot(id).Participants);

            owner.IsDisabled = true;
            await db.SaveChangesAsync();
            using (var scope = host.Services.CreateScope())
                await scope.ServiceProvider.GetRequiredService<VoiceRoomService>().SynchronizeAsync(id);
            await a.Event("VoiceAccessRevoked");
            Assert.Empty(state.Snapshot(id).Participants);
        }
        finally { await db.Database.EnsureDeletedAsync(); }
    }

    private static async Task<JsonElement> Json(HttpResponseMessage response)
    {
        Assert.True(response.IsSuccessStatusCode, $"{response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).Clone();
    }

    // Talks to the actual SignalR endpoint with the JSON wire protocol, without a client SDK dependency.
    private sealed class HubClient(WebSocket socket) : IDisposable
    {
        private readonly Queue<JsonElement> _incoming = new();
        private readonly List<JsonElement> _events = [];
        private int _invocation;

        public static async Task<HubClient> Connect(ApiTestHost host, string token)
        {
            var client = host.Server.CreateWebSocketClient();
            client.ConfigureRequest = request => request.Headers.Authorization = "Bearer " + token;
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var result = new HubClient(await client.ConnectAsync(new Uri("ws://localhost/hubs/voice"), timeout.Token));
            await result.Send(new { protocol = "json", version = 1 });
            var handshake = await result.Read(timeout.Token);
            Assert.False(handshake.TryGetProperty("error", out _));
            return result;
        }

        public async Task<JsonElement> Invoke(string target, params object[] arguments)
        {
            var id = (++_invocation).ToString();
            await Send(new { type = 1, invocationId = id, target, arguments });
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            while (true)
            {
                var message = await Read(timeout.Token);
                if (message.TryGetProperty("invocationId", out var invocation) && invocation.GetString() == id)
                {
                    if (message.TryGetProperty("error", out var error)) throw new HubException(error.GetString());
                    return message.TryGetProperty("result", out var result) ? result.Clone() : default;
                }
                _events.Add(message);
            }
        }

        public async Task<JsonElement> Event(string target)
        {
            var index = _events.FindIndex(item => item.TryGetProperty("target", out var name) && name.GetString() == target);
            if (index >= 0) { var existing = _events[index]; _events.RemoveAt(index); return existing.GetProperty("arguments"); }
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            while (true)
            {
                var message = await Read(timeout.Token);
                if (message.TryGetProperty("target", out var name) && name.GetString() == target) return message.GetProperty("arguments");
                _events.Add(message);
            }
        }

        private async Task Send(object message)
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await socket.SendAsync(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(message) + '\u001e'),
                WebSocketMessageType.Text, true, timeout.Token);
        }

        private async Task<JsonElement> Read(CancellationToken cancellationToken)
        {
            if (_incoming.TryDequeue(out var queued)) return queued;
            using var stream = new MemoryStream();
            var buffer = new byte[8192];
            WebSocketReceiveResult received;
            do
            {
                received = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);
                Assert.NotEqual(WebSocketMessageType.Close, received.MessageType);
                stream.Write(buffer, 0, received.Count);
            } while (!received.EndOfMessage);
            foreach (var frame in Encoding.UTF8.GetString(stream.ToArray()).Split('\u001e', StringSplitOptions.RemoveEmptyEntries))
            {
                using var document = JsonDocument.Parse(frame);
                _incoming.Enqueue(document.RootElement.Clone());
            }
            return _incoming.Dequeue();
        }

        public void Dispose() { socket.Abort(); socket.Dispose(); }
    }
}
