// Local browser test only. No authentication, database writes or physical device capture.
import { VoicePeer } from '/js/voice-peer.js';
const members = [{ userId: 'sample-you', username: 'Bạn', role: 2 }, { userId: 'sample-peer', username: 'Thành viên mẫu', role: 0 }];
window.channelChatMembers = members;
let channels = [{ id: 101, groupId: 1, name: 'chung', topic: 'Nhắn tin và chia sẻ tài liệu', isDefault: true, kind: 0, unreadCount: 0 },
    { id: 102, groupId: 1, name: 'phong-hoc', topic: 'Cùng học qua mic và camera', isDefault: true, kind: 1, unreadCount: 0 }];
let participants = [], version = 0, voice = null, chat = null, remote = null, remoteMedia = null;
const localTracks = [], peers = [];
const NativePeer = window.RTCPeerConnection;
window.RTCPeerConnection = class extends NativePeer { constructor(config) { super(config); peers.push(this); } };
function audio() {
    const context = new AudioContext(), oscillator = context.createOscillator(), gain = context.createGain();
    gain.gain.value = 0; oscillator.connect(gain);
    const destination = context.createMediaStreamDestination(); gain.connect(destination); oscillator.start();
    const track = destination.stream.getAudioTracks()[0], stop = track.stop.bind(track);
    track.stop = () => { stop(); oscillator.stop(); context.close(); };
    context.resume(); return track;
}
function video() {
    const canvas = document.createElement('canvas'); canvas.width = 640; canvas.height = 360;
    const context = canvas.getContext('2d'); let tick = 0;
    const draw = () => {
        context.fillStyle = '#24234a'; context.fillRect(0, 0, 640, 360);
        context.fillStyle = '#8571ff'; context.beginPath(); context.arc(320 + Math.sin(tick++ / 20) * 70, 155, 50, 0, Math.PI * 2); context.fill();
        context.fillStyle = '#e2e8f0'; context.font = '22px sans-serif'; context.textAlign = 'center'; context.fillText('Camera giả lập', 320, 270);
    };
    draw(); const timer = setInterval(draw, 40), track = canvas.captureStream(24).getVideoTracks()[0], stop = track.stop.bind(track);
    track.stop = () => { clearInterval(timer); stop(); }; return track;
}
Object.defineProperty(navigator.mediaDevices, 'getUserMedia', { configurable: true, value: async constraints => {
    const tracks = [];
    if (constraints.audio) tracks.push(audio());
    if (constraints.video) tracks.push(video());
    localTracks.push(...tracks); return new MediaStream(tracks);
} });
const snapshot = () => structuredClone({ groupId: 1, participants, maxParticipants: 6, version });
function broadcast() { version++; voice?.emit('VoiceRoomChanged', snapshot()); }
function closeRemote() { remote?.close(); remote = null; remoteMedia?.getTracks().forEach(track => track.stop()); remoteMedia = null; }
class TestConnection {
    state = 'Disconnected'; connectionId; handlers = new Map();
    constructor(url) { this.url = url; this.connectionId = url.includes('voice') ? 'local-preview' : 'chat-preview'; }
    on(name, handler) { this.handlers.set(name, handler); }
    onclose(handler) { this.closeHandler = handler; }
    onreconnecting(handler) { this.reconnecting = handler; }
    onreconnected(handler) { this.reconnected = handler; }
    emit(name, ...args) { queueMicrotask(() => this.handlers.get(name)?.(...structuredClone(args))); }
    async start() { this.state = 'Connected'; if (this.url.includes('voice')) voice = this; else chat = this; }
    async stop() { this.state = 'Disconnected'; closeRemote(); participants = []; this.closeHandler?.(); }
    async invoke(method, ...args) {
        if (method === 'Watch') return snapshot();
        if (method === 'Join') {
            closeRemote(); const channelId = args[1];
            participants = [{ connectionId: this.connectionId, userId: 'sample-you', displayName: 'Bạn', channelId, microphoneEnabled: false, cameraEnabled: false },
                { connectionId: 'remote-preview', userId: 'sample-peer', displayName: 'Thành viên mẫu', channelId, microphoneEnabled: true, cameraEnabled: true }];
            broadcast();
            remote = new VoicePeer({ localId: 'remote-preview', remoteId: this.connectionId, configuration: { iceServers: [] },
                sendSignal: async signal => this.emit('VoiceSignal', 1, 'remote-preview', signal), onStream: () => {}, onState: () => {},
                onError: error => { document.getElementById('voiceTestStatus').textContent = error.message; } });
            remoteMedia = new MediaStream([audio(), video()]);
            await Promise.all(remoteMedia.getTracks().map(track => remote.setTrack(track.kind, track)));
            return { participantId: this.connectionId, channelId, room: snapshot(), iceServers: [], relayOnly: false };
        }
        if (method === 'Signal') { await remote?.receive(args[2]); return; }
        if (method === 'SetMedia') {
            const local = participants.find(item => item.connectionId === this.connectionId);
            if (local) { local.microphoneEnabled = args[1]; local.cameraEnabled = args[2]; broadcast(); }
            return;
        }
        if (method === 'Leave') { participants = []; closeRemote(); broadcast(); return; }
        if (method === 'IceConfiguration') return { iceServers: [], relayOnly: false };
        if (method === 'JoinGroup') return { groupId: 1, onlineUserIds: members.map(item => item.userId), version: 1 };
        if (method === 'GetRoomRoster') return { members, presence: { groupId: 1, onlineUserIds: members.map(item => item.userId), version: 1 }, website: { groupId: 1, connectedUserIds: [], version: 1 } };
    }
}
window.signalR = { HubConnectionState: { Connected: 'Connected' }, HubConnectionBuilder: class {
    withUrl(url) { this.url = url; return this; } withAutomaticReconnect() { return this; } build() { return new TestConnection(this.url); }
} };
const originalFetch = window.fetch.bind(window);
window.fetch = async (input, options = {}) => {
    if (typeof input !== 'string' || !input.startsWith('/api/groups/1/')) return originalFetch(input, options);
    const body = typeof options.body === 'string' ? JSON.parse(options.body) : null;
    if (input === '/api/groups/1/channels' && options.method === 'POST') {
        const channel = { ...body, id: Math.max(...channels.map(item => item.id)) + 1, groupId: 1, isDefault: false, unreadCount: 0 };
        channels.push(channel); chat?.emit('ChannelsChanged', 1); return Response.json(channel, { status: 201 });
    }
    if (input === '/api/groups/1/channels') return Response.json(channels);
    return Response.json([]);
};
const status = document.createElement('output'); status.id = 'voiceTestStatus'; status.setAttribute('role', 'status');
status.style.cssText = 'position:fixed;right:12px;bottom:3px;z-index:1100;font:10px monospace;color:#34d399;pointer-events:none';
document.body.append(status);
setInterval(async () => {
    const active = peers.filter(peer => peer.connectionState !== 'closed');
    const stats = await Promise.all(active.map(peer => peer.getStats()));
    let audioPackets = 0, videoPackets = 0;
    stats.forEach(report => report.forEach(item => { if (item.type === 'inbound-rtp') { if (item.kind === 'audio') audioPackets += item.packetsReceived || 0; if (item.kind === 'video') videoPackets += item.packetsReceived || 0; } }));
    status.textContent = `THIẾT BỊ GIẢ LẬP · peers=${active.filter(peer => peer.connectionState === 'connected').length} · audio=${audioPackets} · video=${videoPackets} · localTracks=${localTracks.filter(track => track.readyState === 'live').length}`;
}, 500);
