import { VoicePeer } from './voice-peer.js';

const root = document.getElementById('channelChat');
if (root) initializeVoiceRoom(root);

function initializeVoiceRoom(root) {
    const $ = id => document.getElementById(id);
    const groupId = Number(root.dataset.groupId);
    const main = root.querySelector('.cc-main');
    const connection = new signalR.HubConnectionBuilder().withUrl('/hubs/voice').withAutomaticReconnect().build();
    const peers = new Map(), tiles = new Map(), busyMedia = new Set();
    const localStream = new MediaStream();
    let room = { groupId, participants: [], maxParticipants: 6, version: -1 };
    let ready = false, joining = false, joined = false, stopped = false, participantId = null;
    let configuration = null, epoch = 0, startTimer = null;
    let pendingSignals = [], joinIntent = null;
    let voiceChannels = [], selectedChannelId = null, joinedChannelId = null;

    function element(tag, className, text) {
        const node = document.createElement(tag);
        if (className) node.className = className;
        if (text !== undefined) node.textContent = text;
        return node;
    }
    function errorText(error) {
        if (error?.name === 'NotAllowedError') return 'Chưa được cấp quyền mic/camera. Bạn có thể cho phép trong cài đặt của trình duyệt rồi thử lại.';
        if (error?.name === 'NotFoundError') return 'Không tìm thấy mic/camera. Hãy kết nối thiết bị rồi thử lại.';
        if (error?.name === 'NotReadableError') return 'Mic/camera đang bị ứng dụng khác sử dụng. Hãy đóng ứng dụng đó rồi thử lại.';
        return (error?.message || String(error)).replace(/^.*HubException:\s*/, '');
    }
    function showError(error) { $('voiceAlert').textContent = errorText(error); $('voiceAlert').hidden = false; }
    function clearError() { $('voiceAlert').hidden = true; }
    function showStage(show) {
        $('voiceStage').hidden = !show;
        main.classList.toggle('is-voice-open', show);
        root.querySelectorAll('.vc-room-link').forEach(button => button.classList.toggle('is-selected', show && Number(button.dataset.channelId) === selectedChannelId));
    }
    function activeTrack(kind) { return localStream.getTracks().find(track => track.kind === kind && track.readyState === 'live') || null; }
    function connected() { return connection.state === signalR.HubConnectionState.Connected; }

    function render() {
        const channel = voiceChannels.find(item => item.id === selectedChannelId);
        const count = room.participants.filter(member => member.channelId === selectedChannelId).length;
        const inSelectedCall = joined && joinedChannelId === selectedChannelId;
        $('voiceStageTitle').textContent = channel?.name || 'Phòng voice';
        $('editVoiceChannel').hidden = !channel || Number(root.dataset.role) < 1;
        $('voiceEmpty').hidden = voiceChannels.length > 0;
        $('voiceLobbyCount').textContent = count ? `${count} người đang ở trong phòng · Tối đa ${room.maxParticipants} người` : `Chưa có ai trong phòng · Tối đa ${room.maxParticipants} người`;
        $('voiceJoin').disabled = !channel || !ready || !!joinIntent || joining || (!inSelectedCall && count >= room.maxParticipants);
        $('voiceJoin').textContent = joinIntent || joining ? 'Đang tham gia…' : count >= room.maxParticipants ? 'Phòng đã đầy' : joined ? 'Chuyển sang kênh này' : 'Tham gia phòng';
        $('voiceStatus').textContent = !ready ? 'Đang kết nối phòng voice…' : inSelectedCall ? `${count} người đang trong cuộc gọi` : channel?.topic || 'Sẵn sàng kết nối cùng nhóm';
        $('voiceLobby').hidden = inSelectedCall;
        $('voiceGrid').hidden = !inSelectedCall;
        $('voiceControls').hidden = !inSelectedCall;
        $('voiceDock').hidden = !joined;
        renderVoiceChannels();
        for (const [action, kind, label] of [['mic', 'audio', 'mic'], ['camera', 'video', 'camera']]) {
            const enabled = !!activeTrack(kind);
            root.querySelectorAll(`[data-voice-action="${action}"]`).forEach(button => {
                button.classList.toggle('is-off', !enabled);
                button.setAttribute('aria-pressed', String(enabled));
                button.setAttribute('aria-label', `${enabled ? 'Tắt' : 'Bật'} ${label}`);
                button.disabled = !joined || busyMedia.has(kind);
                const text = button.querySelector('span');
                if (text) text.textContent = `${enabled ? 'Tắt' : 'Bật'} ${label}`;
                else button.textContent = `${enabled ? 'Tắt' : 'Bật'} ${label}`;
            });
        }
        $('voiceSidebarMembers').replaceChildren(...room.participants.filter(member => member.channelId === joinedChannelId).map(member => {
            const row = element('div', 'vc-sidebar-member');
            row.append(element('span', '', member.microphoneEnabled ? '●' : '○'), document.createTextNode(member.displayName));
            return row;
        }));
        if (joined) renderTiles();
    }

    function renderVoiceChannels() {
        $('voiceChannelList').replaceChildren(...voiceChannels.map(channel => {
            const button = element('button', 'vc-room-link');
            button.type = 'button'; button.dataset.channelId = String(channel.id);
            button.setAttribute('aria-label', `Mở kênh voice ${channel.name}`);
            button.setAttribute('aria-controls', 'voiceStage');
            button.classList.toggle('is-selected', !$('voiceStage').hidden && selectedChannelId === channel.id);
            const icon = document.createElementNS('http://www.w3.org/2000/svg', 'svg');
            icon.setAttribute('class', 'vc-icon'); icon.setAttribute('viewBox', '0 0 24 24'); icon.setAttribute('aria-hidden', 'true');
            const path = document.createElementNS('http://www.w3.org/2000/svg', 'path');
            path.setAttribute('d', 'M11 5 6 9H3v6h3l5 4zM15 8a6 6 0 0 1 0 8m3-11a10 10 0 0 1 0 14'); icon.append(path);
            const label = element('span', 'vc-room-name', channel.name);
            const count = room.participants.filter(member => member.channelId === channel.id).length;
            label.append(element('small', '', `${count} / ${room.maxParticipants} người`));
            const dot = element('span', 'vc-live-dot'); dot.hidden = count === 0;
            button.append(icon, label, dot);
            button.addEventListener('click', () => { selectedChannelId = channel.id; showStage(true); render(); });
            return button;
        }));
    }

    function loadVoiceChannels(channels) {
        voiceChannels = channels.filter(channel => channel.kind === 1);
        if (joined && !voiceChannels.some(channel => channel.id === joinedChannelId)) {
            cleanup(); if (connected()) connection.invoke('Leave').catch(() => {});
            showError(new Error('Kênh voice đã được lưu trữ.'));
        }
        if (!voiceChannels.some(channel => channel.id === selectedChannelId)) selectedChannelId = voiceChannels[0]?.id ?? null;
        render();
    }

    function createTile(member) {
        const local = member.connectionId === participantId;
        const tile = element('article', `vc-tile${local ? ' is-local is-connected' : ''}`);
        const video = element('video');
        video.autoplay = true; video.playsInline = true; video.muted = local;
        const avatar = element('div', 'vc-tile-avatar', Array.from(member.displayName)[0]?.toUpperCase() || '?');
        const caption = element('div', 'vc-tile-caption');
        const name = element('span', 'vc-tile-name'), status = element('span', 'vc-tile-state');
        caption.append(name, status); tile.append(video, avatar, caption);
        $('voiceGrid').append(tile);
        const result = { tile, video, name, status, state: local ? 'connected' : 'new' };
        tiles.set(member.connectionId, result);
        if (local) attachStream(video, localStream);
        return result;
    }
    function renderTiles() {
        const participants = room.participants.filter(member => member.channelId === joinedChannelId);
        for (const [id, tile] of tiles) {
            if (!participants.some(member => member.connectionId === id)) {
                tile.video.srcObject = null; tile.tile.remove(); tiles.delete(id);
            }
        }
        for (const member of participants) {
            const tile = tiles.get(member.connectionId) || createTile(member);
            const local = member.connectionId === participantId;
            tile.name.textContent = local ? `${member.displayName} (Bạn)` : member.displayName;
            tile.tile.classList.toggle('has-camera', local ? !!activeTrack('video') : member.cameraEnabled);
            const microphone = local ? !!activeTrack('audio') : member.microphoneEnabled;
            tile.status.textContent = !local && tile.state === 'failed' ? 'Mất kết nối' : !local && tile.state !== 'connected' ? 'Đang nối…' : microphone ? 'Mic bật' : 'Mic tắt';
        }
    }
    function attachStream(video, stream) {
        if (video.srcObject !== stream) video.srcObject = stream;
        video.play().catch(() => { if (joined && !video.muted) $('voicePlayback').hidden = false; });
    }

    function applyRoom(snapshot) {
        if (snapshot.groupId !== groupId || snapshot.version < room.version) return;
        room = snapshot;
        if (joined && !room.participants.some(member => member.connectionId === participantId && member.channelId === joinedChannelId)) {
            cleanup(); showError(new Error('Bạn không còn ở phòng voice. Hãy tham gia lại.'));
        }
        render();
        if (joined) reconcilePeers();
    }
    function reconcilePeers() {
        const others = room.participants.filter(member => member.connectionId !== participantId && member.channelId === joinedChannelId);
        for (const [id, peer] of peers) {
            if (!others.some(member => member.connectionId === id)) { peer.close(); peers.delete(id); }
        }
        for (const member of others) {
            if (peers.has(member.connectionId)) continue;
            const version = epoch;
            const peer = new VoicePeer({
                localId: participantId, remoteId: member.connectionId, configuration,
                sendSignal: signal => {
                    if (!joined || version !== epoch || !connected()) return Promise.resolve();
                    return connection.invoke('Signal', groupId, member.connectionId, signal);
                },
                onStream: stream => {
                    if (version !== epoch) return;
                    const tile = tiles.get(member.connectionId);
                    if (tile) attachStream(tile.video, stream);
                },
                onState: state => {
                    if (version !== epoch) return;
                    const tile = tiles.get(member.connectionId);
                    if (tile) { tile.state = state; tile.tile.classList.toggle('is-connected', state === 'connected'); tile.tile.classList.toggle('is-failed', state === 'failed'); renderTiles(); }
                    if (state === 'failed') showError(new Error('Không thể kết nối với một thành viên. Hãy thử vào lại phòng; mạng này có thể cần máy chủ TURN.'));
                },
                onError: error => { if (joined && version === epoch) showError(error); }
            });
            peers.set(member.connectionId, peer);
            for (const kind of ['audio', 'video']) peer.setTrack(kind, activeTrack(kind)).catch(showError);
        }
    }
    async function receiveSignal(id, signal) {
        if (joining) { if (pendingSignals.length < 200) pendingSignals.push([id, signal]); return; }
        if (!joined || !room.participants.some(member => member.connectionId === id && member.channelId === joinedChannelId)) return;
        reconcilePeers();
        await peers.get(id)?.receive(signal);
    }

    async function join() {
        showStage(true);
        if (joinIntent || joining || !ready || !selectedChannelId || (joined && joinedChannelId === selectedChannelId)) return;
        const targetChannelId = selectedChannelId;
        if (!window.isSecureContext || !window.RTCPeerConnection || !navigator.mediaDevices?.getUserMedia)
            throw new Error('Trình duyệt cần hỗ trợ WebRTC và mở trang qua HTTPS hoặc localhost để dùng mic/camera.');
        const intent = {}; joinIntent = intent; render();
        try {
            if (joined) await leave(true);
            if (joinIntent !== intent || !ready || !connected()) return;
            clearError(); joining = true; const version = ++epoch; render();
            try {
                const result = await connection.invoke('Join', groupId, targetChannelId);
                if (version !== epoch || joinIntent !== intent) return;
                participantId = result.participantId;
                joinedChannelId = result.channelId;
                configuration = { iceServers: result.iceServers, iceTransportPolicy: result.relayOnly ? 'relay' : 'all' };
                joined = true; joining = false;
                // A broadcast can arrive before the Join response; retain the newest roster.
                applyRoom(result.room.version >= room.version ? result.room : room);
                for (const [id, signal] of pendingSignals.splice(0)) await receiveSignal(id, signal);
            } catch (error) {
                if (version !== epoch || joinIntent !== intent) return;
                cleanup(true); showError(error);
                // Keep the operation locked while clearing a Join whose acknowledgement was lost.
                if (connected()) await connection.invoke('Leave').catch(() => {});
            }
        } finally { if (joinIntent === intent) { joinIntent = null; joining = false; render(); } }
    }
    function cleanup(preserveJoinIntent = false) {
        if (!preserveJoinIntent) joinIntent = null;
        epoch++; joined = false; joining = false; participantId = null; joinedChannelId = null; configuration = null; pendingSignals = [];
        localStream.getTracks().forEach(track => { track.onended = null; track.stop(); localStream.removeTrack(track); });
        peers.forEach(peer => peer.close()); peers.clear();
        tiles.forEach(tile => { tile.video.srcObject = null; tile.tile.remove(); }); tiles.clear();
        busyMedia.clear(); $('voicePlayback').hidden = true; render();
    }
    async function leave(preserveJoinIntent = false) {
        const wasActive = joined || joining;
        cleanup(preserveJoinIntent); clearError();
        if (wasActive && connected()) {
            try { await connection.invoke('Leave'); }
            catch { showError(new Error('Đã tắt mic/camera. Đang chờ cập nhật trạng thái phòng.')); }
        }
    }

    async function publishMedia() {
        if (joined && connected()) await connection.invoke('SetMedia', groupId, !!activeTrack('audio'), !!activeTrack('video'));
    }
    async function toggleMedia(kind) {
        if (!joined || busyMedia.has(kind)) return;
        const version = epoch;
        busyMedia.add(kind); clearError(); render();
        let captured = null;
        try {
            const current = activeTrack(kind);
            if (current) {
                current.onended = null; current.stop(); localStream.removeTrack(current);
                await Promise.all([...peers.values()].map(peer => peer.setTrack(kind, null)));
            } else {
                captured = await navigator.mediaDevices.getUserMedia(kind === 'audio'
                    ? { audio: { echoCancellation: true, noiseSuppression: true, autoGainControl: true }, video: false }
                    : { audio: false, video: { width: { ideal: 640 }, height: { ideal: 360 }, frameRate: { ideal: 24, max: 30 } } });
                if (!joined || epoch !== version) { captured.getTracks().forEach(track => track.stop()); return; }
                const track = captured.getTracks().find(item => item.kind === kind);
                if (!track) throw new Error('Thiết bị không cung cấp được âm thanh/hình ảnh.');
                localStream.addTrack(track);
                track.onended = () => {
                    if (epoch !== version) return;
                    localStream.removeTrack(track);
                    Promise.all([...peers.values()].map(peer => peer.setTrack(kind, null)))
                        .then(publishMedia).catch(showError);
                    render();
                };
                await Promise.all([...peers.values()].map(peer => peer.setTrack(kind, track)));
            }
            if (version !== epoch) return;
            const local = tiles.get(participantId);
            if (local) attachStream(local.video, localStream);
            await publishMedia();
        } catch (error) {
            // A failed publication must not leave a device transmitting with an "off" roster.
            if (version === epoch) {
                for (const track of localStream.getTracks().filter(item => item.kind === kind)) {
                    track.onended = null; track.stop(); localStream.removeTrack(track);
                }
                captured?.getTracks().forEach(track => track.stop());
                await Promise.allSettled([...peers.values()].map(peer => peer.setTrack(kind, null)));
                showError(error); await publishMedia().catch(() => {});
            }
        } finally {
            if (version === epoch) { busyMedia.delete(kind); render(); }
        }
    }

    root.querySelectorAll('[data-voice-action]').forEach(button => button.addEventListener('click', () => {
        const actions = { open: () => { if (joined) selectedChannelId = joinedChannelId; showStage(true); render(); }, chat: () => showStage(false), join,
            mic: () => toggleMedia('audio'), camera: () => toggleMedia('video'), leave };
        Promise.resolve().then(actions[button.dataset.voiceAction]).catch(showError);
    }));
    $('channelList').addEventListener('click', event => { if (event.target.closest('.cc-channel')) showStage(false); });
    root.addEventListener('quiz:channels-changed', event => loadVoiceChannels(event.detail));
    root.addEventListener('quiz:role-changed', render);
    $('editVoiceChannel').addEventListener('click', () => {
        const channel = voiceChannels.find(item => item.id === selectedChannelId);
        if (channel) root.dispatchEvent(new CustomEvent('quiz:edit-channel', { detail: channel }));
    });
    $('voicePlayback').addEventListener('click', async () => {
        const results = await Promise.allSettled([...tiles.values()].map(tile => tile.video.play()));
        $('voicePlayback').hidden = results.every(result => result.status === 'fulfilled');
    });
    connection.on('VoiceRoomChanged', applyRoom);
    connection.on('VoiceChannelClosed', (id, channelId) => {
        if (id === groupId && joinedChannelId === channelId) { cleanup(); showError(new Error('Kênh voice đã được lưu trữ.')); }
    });
    connection.on('VoiceSignal', (id, sender, signal) => { if (id === groupId) receiveSignal(sender, signal).catch(showError); });
    connection.on('VoiceAccessRevoked', id => {
        if (id !== groupId) return;
        stopped = true; ready = false; cleanup(); showStage(true);
        showError(new Error('Bạn không còn quyền vào phòng voice của nhóm này.')); connection.stop();
    });
    connection.onreconnecting(() => {
        ready = false; cleanup(); showError(new Error('Đã mất kết nối. Mic/camera đã tắt; hãy tham gia lại khi kết nối được khôi phục.'));
    });
    connection.onreconnected(async () => {
        try { room.version = -1; applyRoom(await connection.invoke('Watch', groupId)); ready = true; render(); }
        catch (error) { showError(error); }
    });
    connection.onclose(() => { ready = false; cleanup(); if (!stopped) startTimer = window.setTimeout(start, 5000); });
    async function start() {
        if (stopped) return;
        try {
            await connection.start();
            room.version = -1; applyRoom(await connection.invoke('Watch', groupId)); ready = true; render();
        } catch (error) {
            ready = false; render(); showError(error);
            if (connected()) await connection.stop();
            else if (!stopped) startTimer = window.setTimeout(start, 5000);
        }
    }
    async function refreshIce() {
        if (!joined || !connected()) return;
        const version = epoch;
        try {
            const result = await connection.invoke('IceConfiguration', groupId);
            if (!joined || version !== epoch) return;
            configuration = { iceServers: result.iceServers, iceTransportPolicy: result.relayOnly ? 'relay' : 'all' };
            peers.forEach(peer => peer.pc.setConfiguration(configuration));
        } catch (error) { showError(error); }
    }
    let iceTimer = window.setInterval(refreshIce, 30 * 60 * 1000);
    window.addEventListener('pagehide', () => {
        stopped = true; window.clearTimeout(startTimer); window.clearInterval(iceTimer); cleanup(); connection.stop();
    });
    window.addEventListener('pageshow', event => {
        if (!event.persisted) return;
        stopped = false; ready = false; room.version = -1;
        iceTimer = window.setInterval(refreshIce, 30 * 60 * 1000); start();
    });
    loadVoiceChannels(window.studyGroupChannels || []); start();
}
