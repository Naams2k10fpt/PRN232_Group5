import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
const source = await readFile(new URL('../../QuizManagement/wwwroot/js/voice-activity.js', import.meta.url), 'utf8');
const { VoiceActivity } = await import('data:text/javascript;base64,' + Buffer.from(source).toString('base64'));

function fixture() {
    let amplitude = 0, time = 0, tick;
    const levels = [], nodes = [], contexts = [], cancelled = [];
    class Stream {
        constructor(tracks) { this.tracks = tracks; }
        getAudioTracks() { return this.tracks.filter(t => t.kind === 'audio'); }
    }
    class Context {
        constructor() { this.state = 'running'; contexts.push(this); }
        createMediaStreamSource(stream) {
            const node = { stream, disconnected: false, connect() {}, disconnect() { this.disconnected = true; } };
            nodes.push(node); return node;
        }
        createAnalyser() { return { getFloatTimeDomainData(buffer) { buffer.fill(amplitude); }, disconnect() {} }; }
        async resume() { this.state = 'running'; }
        async close() { this.state = 'closed'; }
    }
    const activity = new VoiceActivity((id, level) => levels.push([id, level]), {
        Context, Stream, now: () => time,
        schedule(callback) { tick = callback; return 1; }, cancel(id) { cancelled.push(id); }
    });
    const track = { kind: 'audio', readyState: 'live', enabled: true, muted: false };
    return { activity, track, contexts, nodes, cancelled, stream: new Stream([track]),
        sample(value, advance = 80) { amplitude = value; time += advance; tick(); return levels.at(-1)?.[1]; } };
}

test('silence stays quiet, voice peaks change the level, and brief gaps do not flicker', () => {
    const f = fixture(); f.activity.setStream('self', f.stream);
    assert.equal(f.sample(0.005), 0);
    const normal = f.sample(0.04);
    assert(normal > 0);
    assert(f.sample(0.12) > normal);
    assert(f.sample(0) > 0);
    assert.equal(f.sample(0, 201), 0);
    f.activity.clear();
});

test('muted, disabled and ended tracks immediately lose their indicator', () => {
    for (const [property, value] of [['muted', true], ['enabled', false], ['readyState', 'ended']]) {
        const f = fixture(); f.activity.setStream('remote', f.stream);
        assert(f.sample(0.08) > 0);
        f.track[property] = value;
        assert.equal(f.sample(0.08), 0, property);
        f.activity.clear();
    }
});

test('updating the same stream reuses its analyser; replacing its audio track disconnects the old one', () => {
    const f = fixture(); f.activity.setStream('remote', f.stream);
    f.activity.setStream('remote', f.stream);
    assert.equal(f.nodes.length, 1);
    const replacement = { ...f.track };
    f.stream.tracks = [replacement];
    f.activity.setStream('remote', f.stream);
    assert.equal(f.nodes[0].disconnected, true);
    assert.equal(f.activity.sources.size, 1);
    assert.equal(f.nodes[1].stream.tracks[0], replacement);
    f.activity.clear();
});

test('removing a participant and leaving release analysers, timers and the audio context', () => {
    const f = fixture(); f.activity.setStream('self', f.stream);
    f.activity.setStream('remote', f.stream);
    f.activity.remove('remote');
    assert.equal(f.activity.sources.size, 1);
    f.activity.clear();
    assert.equal(f.activity.sources.size, 0);
    assert.equal(f.contexts[0].state, 'closed');
    assert.equal(f.activity.context, null);
    assert(f.nodes.every(node => node.disconnected));
    assert.deepEqual(f.cancelled, [1]);
    assert.equal(f.track.readyState, 'live', 'meter must not stop a call-owned track');
});

test('video-only streams and browsers without Web Audio do not break the call', () => {
    const f = fixture(); f.stream.tracks = [{ kind: 'video' }];
    f.activity.setStream('self', f.stream);
    assert.equal(f.contexts.length, 0);
    const unsupported = new VoiceActivity(() => assert.fail('no activity expected'), { Context: null });
    unsupported.setStream('remote', { getAudioTracks: () => [f.track] });
    unsupported.clear();
});
