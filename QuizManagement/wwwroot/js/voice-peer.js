// WebRTC perfect negotiation: either caller can enable media without offer collisions.
export class VoicePeer {
    constructor({ localId, remoteId, configuration, sendSignal, onStream, onState, onError,
        PeerConnection = RTCPeerConnection, Stream = MediaStream }) {
        this.closed = false;
        this.polite = localId > remoteId;
        this.sendSignal = sendSignal;
        this.onError = onError;
        this.makingOffer = false;
        this.ignoreOffer = false;
        this.settingAnswer = false;
        this.pendingCandidates = [];
        this.queue = Promise.resolve();
        this.restarts = 0;
        this.stream = new Stream();
        const pc = this.pc = new PeerConnection({ ...configuration, bundlePolicy: 'max-bundle' });
        this.tracks = { audio: null, video: null };
        this.senders = {};
        // Only one side creates the initial media sections. Creating them on both
        // sides before an offer is applied can leave duplicate, unassociated
        // transceivers after rollback, with conflicting bundled header extensions.
        if (!this.polite) {
            for (const kind of ['audio', 'video'])
                this.senders[kind] = pc.addTransceiver(kind, { direction: 'sendrecv' }).sender;
        }
        pc.ontrack = event => {
            if (this.closed) return;
            if (!this.stream.getTrackById(event.track.id)) this.stream.addTrack(event.track);
            event.track.onunmute = () => { if (!this.closed) onStream(this.stream); };
            onStream(this.stream);
        };
        pc.onicecandidate = event => {
            if (event.candidate && event.candidate.candidate && !this.closed)
                sendSignal({ type: 'ice', candidate: event.candidate.toJSON() }).catch(error => this.fail(error));
        };
        pc.onnegotiationneeded = async () => {
            if (this.closed || (this.polite && !pc.remoteDescription)) return;
            try {
                this.makingOffer = true;
                await pc.setLocalDescription();
                if (!this.closed) await sendSignal({ type: pc.localDescription.type, sdp: pc.localDescription.sdp });
            } catch (error) { this.fail(error); }
            finally { this.makingOffer = false; }
        };
        const updateState = () => {
            if (this.closed) return;
            const state = (pc.connectionState === 'failed' || pc.iceConnectionState === 'failed')
                ? 'failed'
                : (pc.connectionState === 'connected' || pc.iceConnectionState === 'connected')
                    ? 'connected'
                    : pc.connectionState;
            onState(state);
            if (state === 'failed' && this.restarts++ === 0) pc.restartIce();
        };
        pc.onconnectionstatechange = updateState;
        pc.oniceconnectionstatechange = updateState;
    }

    async setTrack(kind, track) {
        if (this.closed) return;
        this.tracks[kind] = track;
        const sender = this.senders[kind];
        if (!sender) return; // The answering side waits for the initial offer.
        try { await sender.replaceTrack(track); }
        catch (error) { if (!this.closed) throw error; }
        if (kind === 'video' && track && !this.closed) {
            const parameters = sender.getParameters();
            if (parameters.encodings?.length) {
                parameters.encodings[0].maxBitrate = 750000;
                parameters.encodings[0].maxFramerate = 24;
                // Some browsers cannot change encoding parameters before the first answer.
                await sender.setParameters(parameters).catch(() => {});
            }
        }
    }

    receive(signal) {
        this.queue = this.queue.then(() => this.receiveCore(signal)).catch(error => this.fail(error));
        return this.queue;
    }

    async receiveCore(signal) {
        if (this.closed) return;
        const pc = this.pc;
        if (signal.type === 'offer' || signal.type === 'answer') {
            const ready = !this.makingOffer && (pc.signalingState === 'stable' || this.settingAnswer);
            this.ignoreOffer = !this.polite && signal.type === 'offer' && !ready;
            if (this.ignoreOffer) { this.pendingCandidates = []; return; }
            this.settingAnswer = signal.type === 'answer';
            try { await pc.setRemoteDescription({ type: signal.type, sdp: signal.sdp }); }
            finally { this.settingAnswer = false; }
            if (this.closed) return;
            for (const candidate of this.pendingCandidates.splice(0)) await pc.addIceCandidate(candidate);
            if (signal.type === 'offer') {
                for (const transceiver of pc.getTransceivers()) {
                    const kind = transceiver.receiver.track.kind;
                    if (!this.senders[kind]) {
                        this.senders[kind] = transceiver.sender;
                        transceiver.direction = 'sendrecv';
                        await this.setTrack(kind, this.tracks[kind]);
                    }
                }
                await pc.setLocalDescription();
                if (!this.closed) await this.sendSignal({ type: pc.localDescription.type, sdp: pc.localDescription.sdp });
            }
        } else if (signal.type === 'ice' && !this.ignoreOffer) {
            if (!pc.remoteDescription) {
                if (this.pendingCandidates.length >= 128) throw new Error('Quá nhiều tín hiệu kết nối.');
                this.pendingCandidates.push(signal.candidate);
            } else {
                try {
                    await pc.addIceCandidate(signal.candidate);
                } catch (err) {
                    if (!this.ignoreOffer) {
                        // Tolerate duplicate or expired candidates from offer collisions
                    }
                }
            }
        }
    }

    fail(error) { if (!this.closed) this.onError(error); }

    close() {
        if (this.closed) return;
        this.closed = true;
        this.pendingCandidates = [];
        this.pc.ontrack = this.pc.onicecandidate = this.pc.onnegotiationneeded = this.pc.onconnectionstatechange = this.pc.oniceconnectionstatechange = null;
        this.pc.close();
        this.stream.getTracks().forEach(track => track.stop());
    }
}
