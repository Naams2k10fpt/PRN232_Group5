import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
const peerSource = await readFile(new URL('../../QuizManagement/wwwroot/js/voice-peer.js', import.meta.url), 'utf8');
const { VoicePeer } = await import('data:text/javascript;base64,' + Buffer.from(peerSource).toString('base64'));

class Stream {
    tracks = [];
    getTracks() { return this.tracks; }
    getTrackById(id) { return this.tracks.find(track => track.id === id); }
    addTrack(track) { this.tracks.push(track); }
}
class Connection {
    signalingState = 'stable';
    connectionState = 'new';
    localDescription = null;
    remoteDescription = null;
    candidates = [];
    transceivers = [];
    restarts = 0;
    closed = false;
    addTransceiver(kind, options) {
        const sender = { track: null, parameters: { encodings: [{}] },
            async replaceTrack(track) { this.track = track; },
            getParameters() { return this.parameters; },
            async setParameters(parameters) { this.parameters = parameters; } };
        this.transceivers.push({ kind, options, sender });
        return { sender };
    }
    async setLocalDescription() {
        this.localDescription = { type: this.signalingState === 'have-remote-offer' ? 'answer' : 'offer', sdp: 'v=0' };
        this.signalingState = this.localDescription.type === 'offer' ? 'have-local-offer' : 'stable';
    }
    async setRemoteDescription(description) {
        this.remoteDescription = description;
        this.signalingState = description.type === 'offer' ? 'have-remote-offer' : 'stable';
    }
    async addIceCandidate(candidate) { this.candidates.push(candidate); }
    restartIce() { this.restarts++; }
    close() { this.closed = true; }
}
function fixture(localId = 'b', remoteId = 'a') {
    const signals = [], errors = [], streams = [], states = [];
    const peer = new VoicePeer({ localId, remoteId, configuration: {}, PeerConnection: Connection, Stream,
        sendSignal: async signal => signals.push(signal), onError: error => errors.push(error),
        onStream: stream => streams.push(stream), onState: state => states.push(state) });
    return { peer, pc: peer.pc, signals, errors, streams, states };
}

test('a listener reserves audio and video so later device changes do not need a new call', () => {
    const { pc } = fixture();
    assert.deepEqual(pc.transceivers.map(item => [item.kind, item.options.direction]), [['audio', 'sendrecv'], ['video', 'sendrecv']]);
    assert.equal(pc.transceivers.every(item => item.sender.track === null), true);
});
test('ICE received before an SDP description is buffered, then applied in order', async () => {
    const { peer, pc, signals } = fixture();
    const first = { candidate: 'candidate:first' }, second = { candidate: 'candidate:second' };
    await Promise.all([peer.receive({ type: 'ice', candidate: first }), peer.receive({ type: 'ice', candidate: second }),
        peer.receive({ type: 'offer', sdp: 'v=0' })]);
    assert.deepEqual(pc.candidates, [first, second]);
    assert.equal(signals[0].type, 'answer');
});
test('the polite peer accepts a colliding offer and answers it', async () => {
    const { peer, pc, signals } = fixture('b', 'a');
    await pc.setLocalDescription();
    await peer.receive({ type: 'offer', sdp: 'v=0 remote' });
    assert.equal(pc.remoteDescription.sdp, 'v=0 remote');
    assert.equal(pc.signalingState, 'stable');
    assert.equal(signals[0].type, 'answer');
});
test('the impolite peer ignores a colliding offer and its ICE without breaking its own offer', async () => {
    const { peer, pc, signals, errors } = fixture('a', 'b');
    await pc.setLocalDescription();
    await peer.receive({ type: 'offer', sdp: 'v=0 collision' });
    await peer.receive({ type: 'ice', candidate: { candidate: 'ignored' } });
    assert.equal(pc.remoteDescription, null);
    assert.equal(pc.candidates.length, 0);
    assert.equal(signals.length, 0);
    await peer.receive({ type: 'answer', sdp: 'v=0 answer' });
    await peer.receive({ type: 'ice', candidate: { candidate: 'accepted' } });
    assert.equal(pc.candidates[0].candidate, 'accepted');
    assert.deepEqual(errors, []);
});
test('tracks without a stream ID are placed in a playable remote stream', () => {
    const { pc, streams } = fixture();
    const track = { id: 'remote-audio', stop() {} };
    pc.ontrack({ track, streams: [] });
    pc.ontrack({ track, streams: [] });
    assert.equal(streams.at(-1).getTracks().length, 1);
});
test('turning video off detaches the sender and encoding caps bound upload bandwidth', async () => {
    const { peer } = fixture();
    const track = { id: 'camera' };
    await peer.setTrack('video', track);
    assert.equal(peer.senders.video.track, track);
    assert.equal(peer.senders.video.parameters.encodings[0].maxBitrate, 750000);
    await peer.setTrack('video', null);
    assert.equal(peer.senders.video.track, null);
});
test('an unsupported encoding update does not stop camera transmission', async () => {
    const { peer, errors } = fixture();
    peer.senders.video.setParameters = async () => { throw new Error('unsupported'); };
    await peer.setTrack('video', { id: 'camera' });
    assert.equal(peer.senders.video.track.id, 'camera');
    assert.deepEqual(errors, []);
});

test('a peer leaving during a track update does not turn off the caller camera for other peers', async () => {
    const { peer } = fixture();
    let reject;
    peer.senders.video.replaceTrack = () => new Promise((_, fail) => { reject = fail; });
    const update = peer.setTrack('video', { id: 'camera' });
    peer.close();
    reject(new Error('The peer connection is closed'));
    await assert.doesNotReject(update);
});

test('a failed track update on a live peer still reports the device error', async () => {
    const { peer } = fixture();
    peer.senders.audio.replaceTrack = async () => { throw new Error('Cannot send audio'); };
    await assert.rejects(peer.setTrack('audio', { id: 'mic' }), /Cannot send audio/);
});
test('a failed connection makes one bounded ICE restart attempt', () => {
    const { pc, states } = fixture();
    pc.connectionState = 'failed'; pc.onconnectionstatechange(); pc.onconnectionstatechange();
    assert.equal(pc.restarts, 1);
    assert.deepEqual(states, ['failed', 'failed']);
});
test('leaving while an answer is pending cannot send an answer into a later call', async () => {
    const { peer, pc, signals } = fixture();
    let finish;
    pc.setRemoteDescription = () => new Promise(resolve => { finish = resolve; });
    const task = peer.receive({ type: 'offer', sdp: 'v=0' });
    await Promise.resolve();
    peer.close(); finish(); await task;
    assert.equal(signals.length, 0);
    assert.equal(pc.closed, true);
});
test('an unbounded stream of candidates cannot accumulate before a description', async () => {
    const { peer, errors } = fixture();
    for (let i = 0; i < 140; i++) await peer.receive({ type: 'ice', candidate: { candidate: `candidate:${i}` } });
    assert.equal(peer.pendingCandidates.length, 128);
    assert.equal(errors.length, 12);
});
