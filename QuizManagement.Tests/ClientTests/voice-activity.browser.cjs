// Optional browser check: NODE_PATH must expose Playwright; uses synthetic audio only.
const { chromium } = require('playwright');
const { readFileSync } = require('node:fs');
const { createServer } = require('node:http');
const path = require('node:path');
const assert = require('node:assert/strict');
(async () => {
    const source = readFileSync(path.join(__dirname, '../../QuizManagement/wwwroot/js/voice-activity.js'), 'utf8');
    const server = createServer((req, res) => res.end('<!doctype html><title>Voice activity regression</title>'));
    await new Promise(resolve => server.listen(0, '127.0.0.1', resolve));
    let browser;
    try {
        browser = await chromium.launch({ channel: 'chrome', headless: true, args: ['--autoplay-policy=no-user-gesture-required'] });
        const page = await browser.newPage();
        await page.goto(`http://127.0.0.1:${server.address().port}`);
        const result = await page.evaluate(async source => {
            const { VoiceActivity } = await import('data:text/javascript;base64,' + btoa(source));
            const audio = new AudioContext(); await audio.resume();
            const destination = audio.createMediaStreamDestination(), oscillator = audio.createOscillator(), gain = audio.createGain();
            gain.gain.value = 0; oscillator.connect(gain); gain.connect(destination); oscillator.start();
            const levels = new Map(), activity = new VoiceActivity((id, level) => levels.set(id, level));
            async function until(check, stage) {
                const deadline = Date.now() + 3000;
                while (!check()) {
                    if (Date.now() > deadline) throw new Error(JSON.stringify({ stage, levels: [...levels], audioState: audio.state, meterState: activity.context?.state, audioTime: audio.currentTime, meterTime: activity.context?.currentTime, sources: [...activity.sources.values()].map(item => ({ lastSound: item.lastSound, max: Math.max(...item.samples) })), tracks: destination.stream.getTracks().map(t => ({ enabled: t.enabled, muted: t.muted, state: t.readyState })) }));
                    await new Promise(resolve => setTimeout(resolve, 30));
                }
            }
            try {
                activity.setStream('local', destination.stream);
                activity.setStream('remote', destination.stream);
                await until(() => levels.has('local') && levels.has('remote'), 'initial silence');
                const silence = [...levels.values()].every(value => value === 0);
                gain.gain.value = 0.15;
                await until(() => levels.get('local') > 0 && levels.get('remote') > 0, 'first sound');
                const active = [...levels.values()];
                gain.gain.value = 0;
                await until(() => levels.get('local') === 0 && levels.get('remote') === 0, 'silence');
                gain.gain.value = 0.15;
                await until(() => levels.get('local') > 0, 'second sound');
                destination.stream.getAudioTracks()[0].enabled = false;
                await until(() => levels.get('local') === 0 && levels.get('remote') === 0, 'mute');
                activity.clear();
                return { silence, active, muted: true, sourcesAfterLeave: activity.sources.size, timerAfterLeave: activity.timer };
            } finally {
                activity.clear(); oscillator.stop(); destination.stream.getTracks().forEach(track => track.stop()); await audio.close();
            }
        }, source);
        assert.equal(result.silence, true);
        assert(result.active.every(level => level > 0));
        assert.equal(result.muted, true);
        assert.equal(result.sourcesAfterLeave, 0);
        assert.equal(result.timerAfterLeave, null);
        console.log(JSON.stringify(result, null, 2));
    } finally { await browser?.close(); await new Promise(resolve => server.close(resolve)); }
})().catch(error => { console.error(error); process.exitCode = 1; });
