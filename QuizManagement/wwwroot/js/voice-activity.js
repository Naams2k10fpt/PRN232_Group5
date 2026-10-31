// Analyze live audio locally; no samples are recorded or sent to the server.
export class VoiceActivity {
    constructor(onLevel, { Context = globalThis.AudioContext || globalThis.webkitAudioContext,
        Stream = globalThis.MediaStream, now = () => Date.now(),
        schedule = (callback, delay) => globalThis.setInterval(callback, delay),
        cancel = timer => globalThis.clearInterval(timer) } = {}) {
        Object.assign(this, { onLevel, Context, Stream, now, schedule, cancel });
        this.sources = new Map(); this.context = null; this.timer = null;
    }

    setStream(id, stream) {
        const track = stream?.getAudioTracks().find(item => item.readyState === 'live' && item.enabled);
        if (this.sources.get(id)?.track === track) return;
        this.remove(id);
        if (!track || !this.Context || !this.Stream) return;
        try {
            this.context ??= new this.Context();
            const source = this.context.createMediaStreamSource(new this.Stream([track]));
            const analyser = this.context.createAnalyser();
            analyser.fftSize = 512;
            source.connect(analyser);
            this.sources.set(id, { track, source, analyser, samples: new Float32Array(512), lastSound: -Infinity, level: 0 });
            this.resume();
            this.timer ??= this.schedule(() => this.sample(), 80);
        } catch { this.remove(id); }
    }

    sample() {
        const now = this.now();
        for (const [id, item] of this.sources) {
            let rms = 0;
            if (this.context.state === 'running' && item.track.readyState === 'live' && item.track.enabled && !item.track.muted) {
                item.analyser.getFloatTimeDomainData(item.samples);
                rms = Math.sqrt(item.samples.reduce((sum, sample) => sum + sample * sample, 0) / item.samples.length);
                if (rms >= 0.018) item.lastSound = now;
            }
            const speaking = this.context.state === 'running' && item.track.enabled && item.track.readyState === 'live'
                && !item.track.muted && now - item.lastSound < 200;
            item.level = speaking ? Math.max(0.25, Math.min(1, rms / 0.12), item.level * 0.65) : 0;
            this.onLevel(id, item.level);
        }
    }

    resume() { this.context?.resume().catch(() => {}); }

    remove(id) {
        const item = this.sources.get(id);
        if (!item) return;
        item.source.disconnect(); item.analyser.disconnect();
        this.sources.delete(id); this.onLevel(id, 0);
        if (!this.sources.size && this.timer !== null) { this.cancel(this.timer); this.timer = null; }
    }

    clear() {
        for (const id of this.sources.keys()) this.remove(id);
        this.context?.close().catch(() => {}); this.context = null;
    }
}
