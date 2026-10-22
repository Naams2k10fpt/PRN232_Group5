# Study group voice and video channels

Every new group has a default text channel `#chung` and a default voice channel `phong-hoc`.
Managers choose **Chat** or **Voice** when adding a channel. Type is immutable after creation;
names, topics and archive actions work for either type. Default channels cannot be archived.
Text and voice channels can share a name. The existing 30-channel limit covers both types.

Migration `20261022130005_AddVoiceChannels` marks existing channels as text and supplies missing
defaults for every old group. Existing messages and read cursors remain attached to their original
text channels; renamed default text channels retain their names. Demo seeding also creates both defaults.

```powershell
dotnet ef database update --project DataAccessObjects --startup-project QuizManagement
```

Open a group's voice channel, then choose **Tham gia phòng**. Joining starts as a listener.
**Bật mic** and **Bật camera** each request only their own device permission. Turning either off
stops its captured track. Returning to text chat keeps the call running; the sidebar offers quick
mute and leave controls. Changing voice rooms leaves the previous call first.

Leaving, closing the page or losing signaling stops local devices and closes peer connections.
A reconnect requires an explicit rejoin with devices off. Membership removal immediately revokes
the call; a five-second sweep also removes disabled accounts and archived channels. Remaining peers
close connections to removed participants. Call membership and media state are transient; audio/video
are not recorded or stored. One user may join one voice channel across all tabs/devices at a time.

The web implementation uses WebRTC media and authenticated `/hubs/voice` SignalR signaling. Default
capacity is **six users per voice channel**, configurable between two and eight. This is a mesh for
small study groups, on the same single-instance deployment model as current presence. Larger rooms
or multiple app instances need a shared media/signaling architecture. Flutter continues to show text
channels; a native mobile calling UI is not included in this web implementation.

## HTTPS and TURN

Use HTTPS on deployment, or `localhost` in development. Other devices opening a plain HTTP LAN IP
cannot request mic/camera. A STUN server is configured by default. For reliable calls between separate
networks, configure a TURN service; STUN alone cannot traverse every NAT/firewall.

Optional `appsettings.Local.json` or deployment environment settings:

```json
{
  "VoiceRooms": {
    "MaxParticipants": 6,
    "TurnUrls": [
      "turn:turn.example.com:3478?transport=udp",
      "turns:turn.example.com:5349?transport=tcp"
    ],
    "TurnSharedSecret": "set-your-coturn-shared-secret-here",
    "RelayOnly": false
  }
}
```

The TURN server must support coturn REST authentication with that shared secret. It stays on the
server; joined members receive HMAC credentials valid for one hour, refreshed every 30 minutes.
Managed TURN providers that issue fixed credentials can instead use `VoiceRooms:IceServers` entries
with `Urls`, `Username` and `Credential`. Never use an open unauthenticated TURN relay.

Environment equivalents include `VoiceRooms__TurnUrls__0`, `VoiceRooms__TurnSharedSecret`,
`VoiceRooms__MaxParticipants` and `VoiceRooms__RelayOnly`. Set `RelayOnly=true` to verify TURN routing.
Providing these settings does not provision a TURN server. No external service is purchased by this change.

## Verification

```powershell
dotnet test QuizManagement.Tests --filter "FullyQualifiedName~Voice|FullyQualifiedName~ChatChannelsApiTests"
node --test QuizManagement.Tests/ClientTests/*.test.mjs
```

For a local browser check with synthetic devices, run
`node QuizManagement.Tests/ClientTests/create-voice-preview.mjs`, then open
`http://localhost:5039/__voice-preview.html` with the development app running. It uses the real UI and
WebRTC peer implementation, synthetic audio/canvas video, and mock signaling. Its status output reports
connected peers, received RTP packets and live local tracks. Delete `wwwroot/__voice-preview.html` and
`wwwroot/__voice-preview-transport.js` after testing; these temporary files must not be deployed.

PostgreSQL tests use disposable databases. Coverage includes legacy backfill, both default channel types,
per-channel capacity under concurrent joins, real SignalR WebSocket signaling, membership checks,
cross-channel signal rejection, archive/kick/disable revocation, and TURN credentials. Browser negotiation
tests cover offer collisions, early ICE, incoming stream-less tracks, camera replacement, bounded ICE
restarts and teardown during an in-flight offer. Controller tests also exercise delayed channel switches,
cancelling a pending join, and a stale Join failure arriving after a new call has started.

Manually check two accounts on separate devices/networks with real microphone/camera permissions and
your TURN service before deployment. Browser synthetic media checks do not validate hardware or NAT traversal.

References: [WebRTC perfect negotiation](https://developer.mozilla.org/en-US/docs/Web/API/WebRTC_API/Perfect_negotiation),
[TURN connectivity](https://webrtc.org/getting-started/turn-server),
[coturn REST credentials](https://github.com/coturn/coturn/blob/master/README.turnserver).
The supplied `D:\swd\phase5-rbl\realtime-audio` was used as a room/state reference; its Agora endpoint
is a scaffold, so this implementation uses the existing .NET identity and SignalR services.
