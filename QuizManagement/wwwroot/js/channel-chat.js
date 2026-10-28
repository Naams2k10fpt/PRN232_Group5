/* Study groups are servers; every channel shares the group's membership and moderation rules. */
(() => {
    'use strict';
    const root = document.getElementById('channelChat');
    if (!root) return;
    const groupId = Number(root.dataset.groupId), userId = root.dataset.userId;
    const base = `/api/groups/${groupId}/channels`;
    const $ = id => document.getElementById(id);
    const timeline = $('chatTimeline'), input = $('chatInput');
    const csrf = document.querySelector('#channelMessageForm input[name="__RequestVerificationToken"]').value;
    const emojis = ['👍', '❤️', '😂', '🎉', '🤔', '👀', '✅', '❌'];
    const messages = new Map(), drafts = new Map();
    let channels = [], active = null, role = Number(root.dataset.role), members = window.channelChatMembers || [];
    let files = [], reply = null, editing = null, beforeEdit = null, sending = false, generation = 0, older = true;
    let online = new Set(), website = new Set(), presenceVersion = -1, websiteVersions = new Map();
    let lastRead = 0, unseen = 0, editorId = null, nonce = null, signature = null, loadingOlder = false, roomJoined = false;
    const connection = new signalR.HubConnectionBuilder().withUrl('/hubs/chat').withAutomaticReconnect().build();
    const node = (tag, className, text) => {
        const result = document.createElement(tag);
        if (className) result.className = className;
        if (text !== undefined) result.textContent = text;
        return result;
    };
    function button(text, title, action, className = '') {
        const element = node('button', className, text);
        element.type = 'button'; element.title = title; element.setAttribute('aria-label', title);
        element.addEventListener('click', () => Promise.resolve(action()).catch(showError));
        return element;
    }
    function actionButton(icon, title, action, className = '') {
        const paths = {
            reply: 'M9 10 4 15l5 5M4 15h10a6 6 0 0 0 0-12h-2',
            smile: 'M8 14s1.5 2 4 2 4-2 4-2M8 8h.01M16 8h.01',
            copy: 'M9 8h10v13H9zM5 16H3V3h10v2',
            edit: 'm16 3 5 5-12 12-6 1 1-6zM14 5l5 5',
            delete: 'M3 6h18M9 6V3h6v3M5 6l1 15h12l1-15M10 10v7M14 10v7'
        };
        const result = button('', title, action, className);
        const svg = document.createElementNS('http://www.w3.org/2000/svg', 'svg');
        svg.setAttribute('viewBox', '0 0 24 24'); svg.setAttribute('class', 'cc-action-icon'); svg.setAttribute('aria-hidden', 'true');
        if (icon === 'smile') {
            const circle = document.createElementNS(svg.namespaceURI, 'circle');
            circle.setAttribute('cx', '12'); circle.setAttribute('cy', '12'); circle.setAttribute('r', '9'); svg.append(circle);
        }
        const path = document.createElementNS(svg.namespaceURI, 'path'); path.setAttribute('d', paths[icon]); svg.append(path);
        result.append(svg); return result;
    }
    function avatar(name, url) {
        const initial = Array.from(name.trim())[0]?.toUpperCase() || '?';
        const result = node('div', 'cc-avatar', initial);
        result.dataset.tone = String(Array.from(name).reduce((sum, char) => sum + char.codePointAt(0), 0) % 4);
        result.setAttribute('aria-hidden', 'true');
        if (url && (/^\/(?!\/)/.test(url) || /^https:\/\//i.test(url))) {
            const image = node('img'); image.src = url; image.alt = ''; image.loading = 'lazy';
            image.addEventListener('error', () => image.remove()); result.append(image);
        }
        return result;
    }
    function showError(error) { $('chatAlert').hidden = false; $('chatAlert').textContent = error.message || String(error); }
    async function api(path, options = {}) {
        const headers = { RequestVerificationToken: csrf, ...options.headers };
        if (options.body && !(options.body instanceof FormData)) { headers['Content-Type'] = 'application/json'; options.body = JSON.stringify(options.body); }
        const response = await fetch(path, { ...options, headers, credentials: 'same-origin' });
        if (!response.ok) {
            const error = await response.json().catch(() => ({}));
            if (response.status === 401) window.location.href = '/Account/Login';
            throw new Error(error.message || `Thao tác thất bại (${response.status}).`);
        }
        return response.status === 204 ? null : response.json();
    }
    const path = () => `${base}/${active.id}`;
    const atBottom = () => timeline.scrollHeight - timeline.scrollTop - timeline.clientHeight < 120;
    function markRead() {
        if (!active || document.hidden || !atBottom() || messages.size === 0) return;
        const id = Math.max(...messages.keys()), channelId = active.id;
        if (id === lastRead) return;
        lastRead = id;
        api(`${base}/${channelId}/read`, { method: 'POST', body: { lastMessageId: id } }).then(() => {
            if (active?.id !== channelId) return;
            active.unreadCount = 0; unseen = 0; $('newMessages').hidden = true; renderChannels();
        }).catch(() => { if (lastRead === id) lastRead = 0; });
    }
    function scrollBottom() { timeline.scrollTop = timeline.scrollHeight; markRead(); }
    function merge(page) {
        for (const message of page) {
            if (message.groupId !== groupId || (message.channelId && message.channelId !== active?.id)) continue;
            const current = messages.get(message.id);
            if (!current || (message.revision || 0) >= (current.revision || 0)) messages.set(message.id, message);
        }
    }
    function renderChannels() {
        $('channelList').replaceChildren(...channels.map(channel => {
            const item = button('', `Mở kênh ${channel.name}`, () => selectChannel(channel), `cc-channel${active?.id === channel.id ? ' is-selected' : ''}`);
            item.setAttribute('aria-current', active?.id === channel.id ? 'page' : 'false');
            item.append(node('span', '', '#'), node('span', 'cc-channel-name', channel.name));
            if (channel.unreadCount > 0) item.append(node('span', 'cc-unread', String(channel.unreadCount)));
            item.disabled = sending; return item;
        }));
    }
    async function loadChannels() {
        const allChannels = await api(base);
        window.studyGroupChannels = allChannels;
        root.dispatchEvent(new CustomEvent('quiz:channels-changed', { detail: allChannels }));
        channels = allChannels.filter(channel => (channel.kind || 0) === 0);
        const current = channels.find(channel => channel.id === active?.id);
        if (!current && channels.length) await selectChannel(channels[0], true);
        else if (current) { active = current; $('channelTitle').textContent = active.name; $('channelTopic').textContent = active.topic || 'Cùng nhau học tập và chia sẻ'; }
        renderChannels();
    }
    async function selectChannel(channel, force = false) {
        if ((!force && sending) || channel.id === active?.id) return;
        if (active) drafts.set(active.id, { text: input.value, files, reply, nonce, signature });
        const draft = drafts.get(channel.id);
        active = channel; files = draft?.files || []; reply = draft?.reply || null; input.value = draft?.text || '';
        nonce = draft?.nonce || null; signature = draft?.signature || null; editing = null; beforeEdit = null;
        const version = ++generation;
        messages.clear(); older = true; lastRead = 0; unseen = 0;
        $('channelTitle').textContent = channel.name; $('channelTopic').textContent = channel.topic || 'Cùng nhau học tập và chia sẻ';
        input.placeholder = `Nhắn vào #${channel.name}`; $('newMessages').hidden = true; $('chatAlert').hidden = true;
        renderChannels(); renderComposer(); renderMessages();
        try {
            const page = await api(`${path()}/messages?limit=30`);
            if (version !== generation) return;
            merge(page); older = page.length === 30; renderMessages(); scrollBottom();
        } catch (error) { if (version === generation) showError(error); }
    }
    async function refreshMessages() {
        if (!active) return;
        const version = generation, endpoint = path(), stick = atBottom();
        const oldFirst = messages.size ? Math.min(...messages.keys()) : null;
        let cursor = messages.size ? Math.max(...messages.keys()) : null;
        while (version === generation) {
            const page = await api(`${endpoint}/messages?limit=30${cursor === null ? '' : `&afterId=${cursor}`}`);
            if (version !== generation) return;
            merge(page);
            if (cursor === null) older = page.length === 30;
            if (cursor === null || page.length < 30) break;
            const next = page.at(-1).id;
            if (next <= cursor) break;
            cursor = next;
        }
        // Reconcile edits/reactions/deletions in the loaded range after an offline interval.
        let before = null;
        do {
            const page = await api(`${endpoint}/messages?limit=100${before === null ? '' : `&beforeId=${before}`}`);
            if (version !== generation) return;
            merge(page);
            if (!page.length || oldFirst === null || page[0].id <= oldFirst || (before !== null && page[0].id >= before)) break;
            before = page[0].id;
        } while (version === generation);
        renderMessages(); if (stick) scrollBottom();
    }
    async function loadOlder() {
        if (!active || !messages.size || loadingOlder) return;
        loadingOlder = true;
        const version = generation, endpoint = path(), height = timeline.scrollHeight, offset = timeline.scrollTop;
        try {
            const page = await api(`${endpoint}/messages?limit=30&beforeId=${Math.min(...messages.keys())}`);
            if (version !== generation) return;
            merge(page); older = page.length === 30; renderMessages();
            timeline.scrollTop = offset + timeline.scrollHeight - height;
        } finally { loadingOlder = false; }
    }
    function attachment(file) {
        const block = node('div', 'cc-attachment');
        const url = `/api/chat/attachments/${encodeURIComponent(file.id)}`;
        if (file.contentType.startsWith('image/')) {
            const image = node('img'); image.src = url; image.alt = file.fileName; image.loading = 'lazy';
            image.addEventListener('load', () => { if (atBottom()) scrollBottom(); });
            const preview = node('a'); preview.href = url; preview.target = '_blank'; preview.rel = 'noopener'; preview.title = 'Mở ảnh'; preview.append(image); block.append(preview);
        }
        const link = node('a', 'cc-file-link'); link.href = `${url}?download=true`; link.download = file.fileName;
        link.append(node('span', '', '↓'), node('span', 'cc-file-name', file.fileName), node('small', '', file.size >= 1048576 ? `${(file.size / 1048576).toFixed(1)} MB` : `${Math.ceil(file.size / 1024)} KB`));
        block.append(link); return block;
    }
    function renderMessage(message) {
        if (message.kind !== 0) return node('div', 'cc-system', `${message.senderName} ${message.kind === 1 ? 'đã vào nhóm' : 'đã rời nhóm'}`);
        const row = node('article', 'cc-message'); row.dataset.messageId = message.id; row.tabIndex = 0;
        row.append(avatar(message.senderName, message.senderAvatarUrl));
        const body = node('div', 'cc-message-body'), meta = node('div', 'cc-message-meta');
        const timestamp = node('time', 'cc-message-time', new Date(message.sentAt).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' }));
        timestamp.dateTime = message.sentAt; timestamp.title = new Date(message.sentAt).toLocaleString();
        meta.append(node('span', 'cc-message-author', message.senderName), timestamp); body.append(meta);
        if (message.reply) {
            const target = messages.get(message.reply.id), reference = target && (target.revision || 0) >= (message.reply.revision || 0) ? { senderName: target.senderName, content: target.deletedAt ? 'Tin nhắn đã bị xóa' : target.content } : message.reply;
            const preview = node('div', 'cc-reply');
            preview.append(node('strong', '', reference.senderName), node('span', '', reference.content || 'File đính kèm'));
            body.append(preview);
        }
        if (message.deletedAt) { body.append(node('div', 'cc-message-text cc-deleted', 'Tin nhắn đã bị xóa')); row.append(body); return row; }
        const content = node('div', 'cc-message-text', message.content);
        if (message.editedAt) content.append(node('span', 'cc-edited', '(đã chỉnh sửa)'));
        body.append(content); for (const file of message.attachments || []) body.append(attachment(file));
        const reactions = node('div', 'cc-reactions');
        for (const emoji of new Set((message.reactions || []).map(item => item.emoji))) {
            const entries = message.reactions.filter(item => item.emoji === emoji), own = entries.some(item => item.userId === userId);
            reactions.append(button(`${emoji} ${entries.length}`, `Reaction ${emoji}`, () => react(message, emoji), `cc-reaction${own ? ' is-mine' : ''}`));
        }
        body.append(reactions); row.append(body);
        const tools = node('div', 'cc-message-tools');
        tools.setAttribute('role', 'group'); tools.setAttribute('aria-label', 'Thao tác tin nhắn');
        tools.append(actionButton('reply', 'Trả lời', () => { editing = null; reply = message; renderComposer(); input.focus(); }));
        tools.append(actionButton('smile', 'Thả reaction', () => {
            const current = body.querySelector('.cc-emoji-picker'); if (current) { current.remove(); return; }
            const picker = node('div', 'cc-emoji-picker');
            for (const emoji of emojis) picker.append(button(emoji, emoji, async () => { await react(message, emoji); picker.remove(); }));
            body.append(picker);
        }));
        tools.append(actionButton('copy', 'Sao chép', () => navigator.clipboard.writeText(message.content)));
        if (message.senderUserId === userId) tools.append(actionButton('edit', 'Sửa tin nhắn', () => {
            if (sending) return;
            beforeEdit = { text: input.value, files, reply }; editing = message; reply = null; input.value = message.content; renderComposer(); input.focus();
        }));
        if (message.senderUserId === userId || role >= 1) tools.append(actionButton('delete', 'Xóa tin nhắn', async () => {
            if (await QuizModal.confirm({ title: 'Xóa tin nhắn?', message: 'Nội dung và file đính kèm sẽ bị xóa.', confirmClass: 'btn-danger', confirmText: 'Xóa' })) {
                merge([await api(`${base}/${message.channelId}/messages/${message.id}`, { method: 'DELETE' })]); renderMessages();
            }
        }, 'is-danger'));
        row.append(tools); return row;
    }
    function renderMessages() {
        const offset = timeline.scrollTop;
        const fragment = document.createDocumentFragment();
        if (older && messages.size) fragment.append(button('Tải tin cũ hơn', 'Tải tin cũ hơn', loadOlder, 'cc-older'));
        const welcome = node('div', 'cc-welcome'); welcome.append(node('span', '', '#'), node('h3', '', `#${active?.name || 'chung'}`), node('p', '', active?.topic || 'Đây là nơi bắt đầu cuộc trò chuyện của bạn. Chia sẻ tài liệu và cùng nhau học tập.')); fragment.append(welcome);
        let previousDate = '';
        for (const message of [...messages.values()].sort((a, b) => a.id - b.id)) {
            const date = new Date(message.sentAt).toLocaleDateString('vi-VN', { day: 'numeric', month: 'long', year: 'numeric' });
            if (date !== previousDate) { fragment.append(node('div', 'cc-date', date)); previousDate = date; }
            fragment.append(renderMessage(message));
        }
        timeline.replaceChildren(fragment); timeline.scrollTop = offset;
    }
    async function react(message, emoji) {
        const activeReaction = !(message.reactions || []).some(item => item.emoji === emoji && item.userId === userId);
        merge([await api(`${base}/${message.channelId}/messages/${message.id}/reaction`, { method: 'PUT', body: { emoji, active: activeReaction } })]); renderMessages();
    }
    function renderComposer() {
        $('composerContext').hidden = !reply && !editing;
        $('composerContextText').textContent = editing ? 'Đang sửa tin nhắn' : reply ? `Trả lời ${reply.senderName}: ${reply.content || 'File đính kèm'}` : '';
        $('pendingFiles').replaceChildren(...(editing ? [] : files).map((file, index) => {
            const item = node('div', 'cc-pending-file'); item.append(node('span', '', file.name), button('×', `Bỏ file ${file.name}`, () => { if (!sending) { files.splice(index, 1); renderComposer(); } })); return item;
        }));
        $('sendMessage').disabled = sending || !active; $('sendMessage').textContent = sending ? 'Đang gửi…' : editing ? 'Lưu' : 'Gửi ↗';
        $('attachFile').disabled = sending || !!editing || !active;
    }
    function cancelContext() {
        if (sending) return;
        if (editing && beforeEdit) { input.value = beforeEdit.text; files = beforeEdit.files; reply = beforeEdit.reply; }
        else reply = null;
        editing = null; beforeEdit = null; renderComposer();
    }
    async function send(event) {
        event?.preventDefault();
        const text = input.value.trim();
        if (!active || sending || (!text && (!files.length || editing))) return;
        const selectedId = active.id, edit = editing;
        sending = true; renderComposer(); renderChannels();
        try {
            let message;
            if (edit) message = await api(`${base}/${selectedId}/messages/${edit.id}`, { method: 'PUT', body: { content: text } });
            else {
                const currentSignature = JSON.stringify([text, reply?.id, files.map(file => [file.name, file.size, file.lastModified])]);
                if (signature !== currentSignature) { nonce = crypto.randomUUID(); signature = currentSignature; }
                const form = new FormData(); form.set('content', text); form.set('clientMessageId', nonce);
                if (reply) form.set('replyToId', reply.id); for (const file of files) form.append('files', file);
                message = await api(`${base}/${selectedId}/messages`, { method: 'POST', body: form });
            }
            if (active.id !== selectedId) return;
            merge([message]); renderMessages();
            if (edit) { input.value = beforeEdit?.text || ''; files = beforeEdit?.files || []; reply = beforeEdit?.reply || null; }
            else { if (input.value.trim() === text) input.value = ''; files = []; reply = null; nonce = null; signature = null; }
            editing = null; beforeEdit = null; $('chatAlert').hidden = true; scrollBottom();
        } catch (error) { showError(error); }
        finally { sending = false; renderComposer(); renderChannels(); input.style.height = ''; }
    }
    $('channelMessageForm').addEventListener('submit', send);
    input.addEventListener('keydown', event => { if (event.key === 'Enter' && !event.shiftKey && !event.isComposing) { event.preventDefault(); send(); } });
    input.addEventListener('input', () => { input.style.height = 'auto'; input.style.height = `${Math.min(input.scrollHeight, 150)}px`; });
    $('cancelContext').addEventListener('click', cancelContext);
    $('attachFile').addEventListener('click', () => $('chatFiles').click());
    $('chatFiles').addEventListener('change', event => {
        const selected = [...event.target.files]; event.target.value = '';
        if (selected.length + files.length > 3) { showError(new Error('Tối đa 3 file mỗi tin nhắn.')); return; }
        if (selected.some(file => file.size < 1 || file.size > 5242880)) { showError(new Error('Mỗi file cần từ 1 byte đến 5 MB.')); return; }
        files.push(...selected); renderComposer();
    });
    $('newMessages').addEventListener('click', scrollBottom);
    timeline.addEventListener('scroll', markRead);
    $('toggleMembers').addEventListener('click', () => { const opened = $('chatMembers').classList.toggle('is-open'); $('toggleMembers').setAttribute('aria-expanded', String(opened)); });
    function renderMembers() {
        role = members.find(member => member.userId === userId)?.role ?? role;
        const previousRole = root.dataset.role;
        root.dataset.role = String(role);
        $('createChannel').hidden = role < 1; $('editChannel').hidden = role < 1; $('createVoiceChannel').hidden = role < 1;
        if (previousRole !== root.dataset.role) root.dispatchEvent(new Event('quiz:role-changed'));
        $('memberCount').textContent = members.length;
        $('presenceStatus').textContent = `${members.filter(member => online.has(member.userId)).length} trong phòng · ${members.filter(member => !online.has(member.userId) && website.has(member.userId)).length} chờ trên web`;
        $('memberList').replaceChildren(...[...members].sort((a, b) => Number(online.has(b.userId)) - Number(online.has(a.userId)) || b.role - a.role).map(member => {
            const row = node('div', 'cc-member'), info = node('div', 'cc-member-info');
            info.append(node('strong', '', member.username), node('small', '', member.role === 2 ? 'Chủ phòng' : member.role === 1 ? 'Người quản lý' : 'Thành viên'));
            row.append(avatar(member.username), info, node('span', `cc-presence-dot${online.has(member.userId) ? ' is-online' : website.has(member.userId) ? ' is-waiting' : ''}`)); return row;
        }));
    }
    function applyPresence(snapshot) { if (snapshot.groupId !== groupId || snapshot.version < presenceVersion) return; presenceVersion = snapshot.version; online = new Set(snapshot.onlineUserIds); renderMembers(); }
    async function refreshRoster() {
        const data = await connection.invoke('GetRoomRoster', groupId); members = data.members; applyPresence(data.presence);
        for (const member of members) {
            if (data.website.version < (websiteVersions.get(member.userId) ?? -1)) continue;
            websiteVersions.set(member.userId, data.website.version);
            if (data.website.connectedUserIds.includes(member.userId)) website.add(member.userId); else website.delete(member.userId);
        }
        renderMembers();
    }
    async function syncViewing() {
        if (connection.state !== signalR.HubConnectionState.Connected) return;
        if (!document.hidden && !roomJoined) { applyPresence(await connection.invoke('JoinGroup', groupId)); roomJoined = true; await refreshRoster(); }
        else if (document.hidden && roomJoined) { await connection.invoke('LeaveGroup', groupId); roomJoined = false; }
    }
    connection.on('ReceiveMessage', message => {
        if (message.groupId !== groupId) return;
        if (message.channelId !== active?.id) {
            const channel = channels.find(item => item.id === message.channelId);
            if (channel && message.senderUserId !== userId && message.kind === 0) channel.unreadCount++;
            renderChannels(); return;
        }
        const duplicate = messages.has(message.id), stick = atBottom(); merge([message]); renderMessages();
        if (stick) scrollBottom(); else if (!duplicate) { unseen++; $('newMessages').hidden = false; $('newMessages').textContent = `↓ ${unseen} tin nhắn mới`; }
    });
    connection.on('MessageUpdated', message => {
        if (message.groupId !== groupId || message.channelId !== active?.id) return;
        if (messages.has(message.id)) merge([message]);
        for (const child of messages.values()) {
            if (child.reply?.id === message.id && (child.reply.revision || 0) <= message.revision) {
                child.reply = { id: message.id, senderName: message.senderName, content: message.deletedAt ? 'Tin nhắn đã bị xóa' : message.content, isDeleted: !!message.deletedAt, revision: message.revision };
            }
        }
        renderMessages();
    });
    connection.on('ChannelsChanged', changed => { if (changed === groupId) loadChannels().catch(showError); });
    connection.on('RoomPresenceChanged', applyPresence);
    connection.on('RoomRosterChanged', changed => { if (changed === groupId) refreshRoster().catch(showError); });
    connection.on('WebsitePresenceDelta', change => {
        if (change.groupId !== groupId || change.version < (websiteVersions.get(change.userId) ?? -1)) return;
        websiteVersions.set(change.userId, change.version);
        if (change.connected) website.add(change.userId); else website.delete(change.userId); renderMembers();
    });
    connection.on('GroupAccessRevoked', changed => { if (changed === groupId) window.location.href = '/StudyGroups'; });
    function disconnected(text) {
        roomJoined = false; online.clear(); website.clear(); presenceVersion = -1; websiteVersions.clear();
        $('connectionStatus').textContent = text; $('connectionStatus').classList.remove('is-connected'); renderMembers();
    }
    connection.onreconnecting(() => disconnected('Đang kết nối lại…'));
    connection.onclose(() => { disconnected('Mất kết nối realtime.'); $('connectionStatus').append(button('Kết nối lại', 'Kết nối lại', start)); });
    async function start() {
        await connection.start(); await syncViewing(); await loadChannels(); await refreshMessages();
        $('connectionStatus').classList.add('is-connected');
    }
    connection.onreconnected(async () => { try { await syncViewing(); await loadChannels(); await refreshMessages(); $('connectionStatus').classList.add('is-connected'); } catch (error) { showError(error); } });
    let visibilityQueue = Promise.resolve();
    document.addEventListener('visibilitychange', () => { visibilityQueue = visibilityQueue.catch(() => {}).then(async () => { await syncViewing(); if (!document.hidden) { await refreshMessages(); markRead(); } }).catch(showError); });

    function openEditor(channel = null, kind = 0) {
        editorId = channel?.id ?? null; $('channelEditorTitle').textContent = channel ? 'Sửa kênh' : 'Tạo kênh';
        $('channelKind').value = String(channel?.kind ?? kind); $('channelKind').disabled = !!channel;
        $('channelName').value = channel?.name || ''; $('channelDescription').value = channel?.topic || '';
        $('archiveChannel').hidden = !channel || channel.isDefault; $('channelEditorError').textContent = '';
        $('channelEditor').showModal();
    }
    $('createChannel').addEventListener('click', () => openEditor()); $('editChannel').addEventListener('click', () => openEditor(active));
    $('createVoiceChannel').addEventListener('click', () => openEditor(null, 1));
    root.addEventListener('quiz:edit-channel', event => openEditor(event.detail));
    $('cancelChannelEditor').addEventListener('click', () => $('channelEditor').close());
    $('channelEditorForm').addEventListener('submit', async event => {
        event.preventDefault();
        const submit = event.submitter; submit.disabled = true;
        try { await api(editorId ? `${base}/${editorId}` : base, { method: editorId ? 'PUT' : 'POST', body: { name: $('channelName').value, topic: $('channelDescription').value, kind: Number($('channelKind').value) } }); $('channelEditor').close(); await loadChannels(); }
        catch (error) { $('channelEditorError').textContent = error.message; } finally { submit.disabled = false; }
    });
    $('archiveChannel').addEventListener('click', async () => {
        if (!editorId) return;
        $('channelEditor').close();
        if (!await QuizModal.confirm({ title: 'Lưu trữ kênh?', message: 'Kênh sẽ đóng và lịch sử vẫn được giữ.', confirmText: 'Lưu trữ' })) return;
        try { await api(`${base}/${editorId}`, { method: 'DELETE' }); $('channelEditor').close(); await loadChannels(); } catch (error) { $('channelEditorError').textContent = error.message; }
    });
    renderMembers(); renderComposer(); start().catch(showError);
})();
