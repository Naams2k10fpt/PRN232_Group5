import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { runInNewContext } from 'node:vm';

const source = (await readFile(new URL('../../QuizManagement/wwwroot/js/voice-room.js', import.meta.url), 'utf8'))
    .replace(/^import .*;\r?\n/, '');
const flush = () => new Promise(resolve => setImmediate(resolve));
function deferred() {
    let resolve, reject;
    const promise = new Promise((yes, no) => { resolve = yes; reject = no; });
    return { promise, resolve, reject };
}

// Minimal DOM and transport let the real room controller run across delayed hub responses.
async function fixture() {
    class Element {
        dataset = {}; children = []; handlers = new Map(); hidden = false;
        classList = { toggle() {} };
        append(...nodes) { this.children.push(...nodes); }
        replaceChildren(...nodes) { this.children = nodes; }
        setAttribute() {}
        addEventListener(name, handler) { this.handlers.set(name, handler); }
        click() { this.handlers.get('click')?.({ target: this }); }
        querySelector() { return null; }
        remove() {}
        play() { return Promise.resolve(); }
    }
    const elements = new Map();
    const get = id => { if (!elements.has(id)) elements.set(id, new Element()); return elements.get(id); };
    const root = get('channelChat'); root.dataset = { groupId: '1', role: '2' };
    const buttons = ['join', 'leave', 'mic', 'camera'].map(action => {
        const button = action === 'join' ? get('voiceJoin') : new Element();
        button.dataset.voiceAction = action; return button;
    });
    root.querySelector = () => get('main');
    root.querySelectorAll = selector => selector === '[data-voice-action]' ? buttons
        : selector === '.vc-room-link' ? get('voiceChannelList').children
        : buttons.filter(button => selector === `[data-voice-action="${button.dataset.voiceAction}"]`);
    let version = 0, participants = [];
    const calls = [], handlers = new Map();
    const snapshot = () => ({ groupId: 1, participants: [...participants], maxParticipants: 6, version });
    const connection = {
        state: 'Connected', pendingLeave: null, pendingJoin: null,
        on: (name, handler) => handlers.set(name, handler),
        onclose(handler) { this.closed = handler; },
        onreconnecting(handler) { this.reconnecting = handler; },
        onreconnected(handler) { this.reconnected = handler; },
        async start() {}, async stop() {},
        async invoke(method, ...args) {
            calls.push({ method, args });
            if (method === 'Watch') return snapshot();
            if (method === 'Leave') {
                if (this.pendingLeave) await this.pendingLeave.promise;
                participants = []; version++; return;
            }
            if (method === 'Join') {
                if (this.pendingJoin) return this.pendingJoin.promise;
                participants = [{ connectionId: 'self', displayName: 'You', channelId: args[1] }]; version++;
                return { participantId: 'self', channelId: args[1], room: snapshot(), iceServers: [], relayOnly: false };
            }
        }
    };
    class Stream { getTracks() { return []; } }
    const window = {
        isSecureContext: true, RTCPeerConnection() {},
        studyGroupChannels: [{ id: 10, kind: 1, name: 'First' }, { id: 11, kind: 1, name: 'Second' }],
        setInterval() {}, clearInterval() {}, setTimeout() {}, clearTimeout() {}, addEventListener() {}
    };
    runInNewContext(source, {
        document: { getElementById: get, createElement: () => new Element(), createElementNS: () => new Element(), createTextNode: () => new Element() },
        window, navigator: { mediaDevices: { getUserMedia() {} } }, MediaStream: Stream,
        signalR: { HubConnectionState: { Connected: 'Connected' }, HubConnectionBuilder: class {
            withUrl() { return this; } withAutomaticReconnect() { return this; } build() { return connection; }
        } }
    });
    await flush();
    const click = async action => { buttons.find(button => button.dataset.voiceAction === action).click(); await flush(); };
    const selectSecond = () => get('voiceChannelList').children.find(button => button.dataset.channelId === '11').click();
    return { connection, calls, get, click, selectSecond };
}

test('repeated switch clicks cannot start another Join while the previous Leave is pending', async () => {
    const f = await fixture(); await f.click('join');
    f.connection.pendingLeave = deferred(); f.selectSecond();
    await f.click('join'); await f.click('join');
    assert.equal(f.get('voiceJoin').disabled, true);
    assert.equal(f.calls.filter(call => call.method === 'Join').length, 1);
    f.connection.pendingLeave.resolve(); await flush();
    assert.deepEqual(f.calls.filter(call => call.method === 'Join').map(call => call.args[1]), [10, 11]);
    assert.equal(f.get('voiceDock').hidden, false);
});

test('leaving during a pending switch cancels its next Join', async () => {
    const f = await fixture(); await f.click('join');
    f.connection.pendingLeave = deferred(); f.selectSecond(); await f.click('join');
    await f.click('leave');
    f.connection.pendingLeave.resolve(); await flush();
    assert.equal(f.calls.filter(call => call.method === 'Join').length, 1);
    assert.equal(f.get('voiceDock').hidden, true);
});

test('an old Join rejection after reconnect cannot leave the newly joined room', async () => {
    const f = await fixture();
    const oldJoin = f.connection.pendingJoin = deferred(); await f.click('join');
    f.connection.reconnecting();
    f.connection.pendingJoin = null; await f.connection.reconnected();
    f.selectSecond(); await f.click('join');
    oldJoin.reject(new Error('Old acknowledgement lost')); await flush();
    assert.equal(f.calls.filter(call => call.method === 'Leave').length, 0);
    assert.equal(f.get('voiceDock').hidden, false);
    assert.equal(f.get('voiceAlert').hidden, true);
});
