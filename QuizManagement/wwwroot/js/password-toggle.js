// Adds an eye button to every input[type=password] (opt out with data-no-toggle).
// Covers fields added after load too, via a MutationObserver.
(function () {
    'use strict';

    var SHOW_LABEL = 'Hiện mật khẩu';
    var HIDE_LABEL = 'Ẩn mật khẩu';

    function enhance(input) {
        if (input.dataset.pwToggle || input.hasAttribute('data-no-toggle')) return;
        input.dataset.pwToggle = '1';

        var wrap = document.createElement('div');
        wrap.className = 'pw-field';
        input.parentNode.insertBefore(wrap, input);
        wrap.appendChild(input);

        var btn = document.createElement('button');
        btn.type = 'button';
        btn.className = 'pw-toggle';
        btn.setAttribute('aria-label', SHOW_LABEL);
        btn.setAttribute('aria-pressed', 'false');
        btn.innerHTML =
            '<span class="pw-icon pw-icon-open"><span class="iconify" data-icon="radix-icons:eye-open"></span></span>' +
            '<span class="pw-icon pw-icon-closed" hidden><span class="iconify" data-icon="radix-icons:eye-closed"></span></span>';
        wrap.appendChild(btn);

        var open = btn.querySelector('.pw-icon-open');
        var closed = btn.querySelector('.pw-icon-closed');

        // Keep focus (and caret) in the field when the button is pressed with a pointer.
        btn.addEventListener('mousedown', function (e) { e.preventDefault(); });
        btn.addEventListener('click', function () {
            var reveal = input.type === 'password';
            var start = input.selectionStart, end = input.selectionEnd;
            input.type = reveal ? 'text' : 'password';
            btn.setAttribute('aria-pressed', String(reveal));
            btn.setAttribute('aria-label', reveal ? HIDE_LABEL : SHOW_LABEL);
            open.hidden = reveal;
            closed.hidden = !reveal;
            input.focus();
            try { input.setSelectionRange(start, end); } catch (_) { /* type change may reject it */ }
        });
    }

    function scan(root) {
        if (root.matches && root.matches('input[type=password]')) enhance(root);
        if (root.querySelectorAll) root.querySelectorAll('input[type=password]').forEach(enhance);
    }

    function init() {
        scan(document);
        new MutationObserver(function (records) {
            records.forEach(function (r) {
                r.addedNodes.forEach(function (n) { if (n.nodeType === 1) scan(n); });
            });
        }).observe(document.body, { childList: true, subtree: true });
    }

    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', init);
    else init();
})();
