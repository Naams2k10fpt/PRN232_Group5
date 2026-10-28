// Optional real-browser regression check. Requires Playwright and Chrome.
// node QuizManagement.Tests/ClientTests/voice-peer.browser.cjs
const assert = require('node:assert/strict');
const { readFileSync } = require('node:fs');
const { createServer } = require('node:http');
const { chromium } = require('playwright');

(async () => {
    const source = readFileSync(require('node:path').join(__dirname, '../../QuizManagement/wwwroot/js/voice-peer.js'), 'utf8');
    const server = createServer((req, res) => res.end('<!doctype html><title>Voice regression</title>'));
    await new Promise(resolve => server.listen(0, '127.0.0.1', resolve));
    let browser;
    try {
        browser = await chromium.launch({ channel: process.env.VOICE_BROWSER_CHANNEL || 'chrome', headless: true });
        const page = await browser.newPage();
        await page.goto(`http://127.0.0.1:${server.address().port}`);
        const result = await page.evaluate(async source => {
            const { VoicePeer } = await import('data:text/javascript;charset=utf-8,' + encodeURIComponent(source));
            const errors = [], states = [], peers = [], streams = [], signals = [];
            const audio = new AudioContext();
            await audio.resume();
            const destination = audio.createMediaStreamDestination();
            const oscillator = audio.createOscillator();
            oscillator.connect(destination); oscillator.start();
            const canvas = document.createElement('canvas');
            canvas.width = 160; canvas.height = 90;
            const paint = setInterval(() => {
                canvas.getContext('2d').fillRect(0, 0, 160, 90);
            }, 40);
            const camera = canvas.captureStream(24);
            const tracks = { audio: destination.stream.getAudioTracks()[0], video: camera.getVideoTracks()[0] };
            async function until(check, label) {
                const deadline = Date.now() + 15000;
                while (!check()) {
                    if (errors.length) throw new Error(errors.join('; '));
                    if (Date.now() > deadline) throw new Error('Timed out: ' + label);
                    await new Promise(resolve => setTimeout(resolve, 50));
                }
            }
            try {
                // Both listeners join at once. Only the elected initiator offers.
                for (let i = 0; i < 2; i++) {
                    peers[i] = new VoicePeer({ localId: String(i), remoteId: String(1 - i), configuration: { iceServers: [] },
                        sendSignal: async signal => { signals.push([i, signal.type]); setTimeout(() => peers[1 - i].receive(signal), 0); },
                        onError: error => errors.push(error.message), onStream: stream => streams[i] = stream,
                        onState: state => states.push([i, state]) });
                }
                await until(() => peers.every(peer => peer.pc.connectionState === 'connected'), 'listener connection');
                if (!peers.every(peer => peer.pc.getTransceivers().length === 2)) throw new Error('Duplicated media sections');
                peers.forEach(peer => peer.pc.setConfiguration({ ...peer.pc.getConfiguration(), iceServers: [] }));
                for (const peer of peers) {
                    await peer.setTrack('audio', tracks.audio);
                    await peer.setTrack('video', tracks.video);
                }
                await until(() => streams.every(stream => stream?.getTracks().length === 2), 'remote tracks');
                async function receivedMedia() {
                    const stats = await Promise.all(peers.map(peer => peer.pc.getStats()));
                    return stats.every(report => ['audio', 'video'].every(kind => [...report.values()].some(
                        stat => stat.type === 'inbound-rtp' && stat.kind === kind && stat.packetsReceived > 0)));
                }
                await until(() => streams.length === 2, 'streams');
                const deadline = Date.now() + 15000;
                while (!await receivedMedia()) {
                    if (Date.now() > deadline) throw new Error('No incoming audio/video RTP');
                    await new Promise(resolve => setTimeout(resolve, 100));
                }
                for (const peer of peers) {
                    await peer.setTrack('audio', null); await peer.setTrack('video', null);
                    await peer.setTrack('audio', tracks.audio); await peer.setTrack('video', tracks.video);
                }
                const previousOffers = signals.filter(([, type]) => type === 'offer').length;
                peers.forEach(peer => peer.pc.restartIce());
                await until(() => signals.filter(([, type]) => type === 'offer').length > previousOffers, 'ICE restart offer');
                await until(() => peers.every(peer => peer.pc.signalingState === 'stable'), 'ICE renegotiation');
                await new Promise(resolve => setTimeout(resolve, 500));
                await until(() => peers.every(peer => peer.pc.connectionState === 'connected'), 'ICE restart');
                if (!peers.every(peer => peer.pc.getTransceivers().length === 2)) throw new Error('Duplicated media after restart');
                return { errors, states, connected: true, receivedAudioAndVideo: await receivedMedia() };
            } catch (error) { return { errors: [...errors, error.message], states, signals, peerStates: peers.map(peer => ({ signaling: peer.pc.signalingState, ice: peer.pc.iceConnectionState, mediaSections: peer.pc.getTransceivers().length })), connected: false }; }
            finally {
                peers.forEach(peer => peer.close());
                Object.values(tracks).forEach(track => track.stop());
                clearInterval(paint); oscillator.stop(); await audio.close();
            }
        }, source);
        console.log(JSON.stringify({ browser: browser.version(), ...result }, null, 2));
        assert.deepEqual(result.errors, []);
        assert.equal(result.connected, true);
        assert.equal(result.receivedAudioAndVideo, true);
    } finally { await browser?.close(); await new Promise(resolve => server.close(resolve)); }
})().catch(error => { console.error(error); process.exitCode = 1; });
