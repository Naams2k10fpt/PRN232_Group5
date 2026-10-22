// Generate temporary local preview files; remove them from wwwroot after browser verification.
import { readFile, writeFile, copyFile } from 'node:fs/promises';
const here = new URL('.', import.meta.url), web = new URL('../../QuizManagement/', here);
const source = await readFile(new URL('Views/StudyGroups/Room.cshtml', web), 'utf8');
let room = source.slice(source.indexOf('<div id="channelChat"'), source.indexOf('@section Scripts'))
    .replaceAll('@Model.Name[..1].ToUpper()', 'N').replaceAll('@Model.Name', 'Nhóm học tập')
    .replaceAll('@Model.Description', 'Cùng học tập và chia sẻ').replaceAll('@Model.Id', '1')
    .replaceAll('@userId', 'sample-you').replaceAll('@role', '2').replaceAll('@members.Count', '2')
    .replaceAll('hidden="@(role < 1)"', '')
    .replaceAll('@Html.AntiForgeryToken()', '<input name="__RequestVerificationToken" type="hidden" value="preview-only">')
    .replace(/@if \(role >= 1\) \{.*?\}/g, '<a href="#">Quản lý nhóm ↗</a>');
if (/@Model|@if|@Html|@\(role/.test(room)) throw new Error('Unresolved Razor in preview.');
const page = `<!doctype html><html lang="vi" data-bs-theme="dark"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>Kiểm thử phòng voice — thiết bị giả lập</title><link rel="stylesheet" href="/lib/bootstrap/dist/css/bootstrap.min.css"><link rel="stylesheet" href="/css/site.css"><link rel="stylesheet" href="/css/channel-chat.css"><link rel="stylesheet" href="/css/voice-room.css"></head><body><header><nav class="navbar navbar-expand-sm"><div class="container-fluid"><a class="navbar-brand" href="#">QuizMS</a><div class="navbar-nav"><a class="nav-link" href="#">Nhóm học tập</a></div><span style="font-size:10px;color:var(--text-muted)">KIỂM THỬ · DỮ LIỆU MẪU</span></div></nav></header><div class="container page-content"><main role="main">${room}</main></div><footer class="footer">QuizManagement</footer><script type="module" src="/__voice-preview-transport.js"></script><script type="module" src="/js/channel-chat.js"></script><script type="module" src="/js/voice-room.js"></script></body></html>`;
await writeFile(new URL('wwwroot/__voice-preview.html', web), page);
await copyFile(new URL('voice-preview-transport.js', here), new URL('wwwroot/__voice-preview-transport.js', web));
console.log('Local preview: /__voice-preview.html (synthetic devices and mock signaling only).');
