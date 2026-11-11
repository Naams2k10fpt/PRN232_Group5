(function () {
    'use strict';

    const configElement = document.getElementById('flashcardConfig');
    if (!configElement) return;
    const config = JSON.parse(configElement.textContent);
    const token = document.querySelector('#flashcardReviewToken input').value;
    const app = document.getElementById('flashcardApp');
    const complete = document.getElementById('flashcardComplete');
    const saveStatus = document.getElementById('flashcardSaveError');
    const retry = document.getElementById('retryReviewButton');
    const cardInner = document.getElementById('flashcardInner');
    const scene = document.querySelector('.fc-scene');
    const reducedMotion = window.matchMedia('(prefers-reduced-motion: reduce)');
    const controls = ['prevCardButton', 'nextCardButton', 'againButton', 'knownButton', 'shuffleButton']
        .map(id => document.getElementById(id));
    const status = new Map();
    const directRetries = new Map();
    let index = 0;
    let flipped = false;
    let busy = false;
    let saving = false;
    let outbox;
    let storageProblem = false;
    let initialPending = [];
    try {
        outbox = new window.QuizFlashcardReviews.ReviewOutbox(localStorage, config.userId, config.deckId);
        initialPending = outbox.list();
    } catch {
        storageProblem = true;
    }
    const pendingQuestions = new Set(initialPending.map(review => review.questionId));
    const cards = config.cards.filter(card => !pendingQuestions.has(card.QuestionId));

    function showStatus(message, failed) {
        saveStatus.textContent = message;
        saveStatus.classList.toggle('d-none', !message);
        saveStatus.classList.toggle('alert-danger', failed);
        saveStatus.classList.toggle('alert-info', !failed);
        retry.classList.toggle('d-none', !failed);
    }

    function pendingCount() {
        if (storageProblem) return 0;
        try { return outbox.list().length; }
        catch { storageProblem = true; return 0; }
    }

    function updateLinks() {
        const disabled = saving || pendingCount() > 0;
        document.querySelectorAll('[data-needs-sync]').forEach(link => {
            link.classList.toggle('disabled', disabled);
            link.setAttribute('aria-disabled', String(disabled));
        });
    }

    async function send(review) {
        const response = await fetch(config.reviewUrl, {
            method: 'POST',
            credentials: 'same-origin',
            signal: AbortSignal.timeout(65000),
            headers: {
                'Content-Type': 'application/x-www-form-urlencoded;charset=UTF-8',
                'RequestVerificationToken': token
            },
            body: new URLSearchParams({
                deckId: config.deckId,
                questionId: review.questionId,
                remembered: review.remembered,
                reviewId: review.id
            })
        });
        if (response.status === 404) return; // A removed question cannot be reviewed again.
        if (!response.ok || response.redirected ||
            !response.headers.get('Content-Type')?.includes('application/json')) {
            throw new Error('Review was not confirmed.');
        }
    }

    async function sync() {
        if (saving || storageProblem) return;
        saving = true;
        updateLinks();
        try {
            if (pendingCount()) showStatus('Đang lưu tiến độ…', false);
            await outbox.flush(send);
            const remaining = pendingCount();
            showStatus(remaining ? 'Có lượt ôn đang chờ lưu. Chọn gửi lại để tiếp tục.' : '', remaining > 0);
            if (!remaining && initialPending.length && !app) window.location.reload();
        } catch {
            showStatus('Lượt ôn đã được giữ trên trình duyệt. Kết nối lại rồi chọn Gửi lại lượt ôn.', true);
        } finally {
            saving = false;
            updateLinks();
        }
    }

    function showComplete() {
        if (app) app.hidden = true;
        complete.hidden = false;
        const known = [...status.values()].filter(Boolean).length;
        document.getElementById('reviewResult').textContent = cards.length
            ? 'Đã học ' + cards.length + ' thẻ · Đã thuộc ' + known + ' · Chưa thuộc ' + (cards.length - known)
            : 'Các thẻ trước đã được đánh dấu. Bạn có thể tiếp tục sau khi lưu tiến độ.';
        if (known && window.QuizCelebration) {
            window.QuizCelebration.confetti({ particleCount: 80 });
            window.QuizCelebration.playVictoryChime();
        }
        updateLinks();
    }

    function animateCard(direction) {
        if (reducedMotion.matches || !scene.animate) return;
        scene.getAnimations().forEach(animation => animation.cancel());
        scene.animate([
            { transform: 'translateX(' + (direction * 28) + 'px)', opacity: 0.5 },
            { transform: 'translateX(0)', opacity: 1 }
        ], { duration: 160, easing: 'ease-out' });
    }

    function optionElement(option, optionIndex, answer) {
        const element = document.createElement('div');
        element.className = answer
            ? 'fc-back-option ' + (option.IsCorrect ? 'fc-back-option--correct' : 'fc-back-option--wrong')
            : 'fc-option';
        const letter = document.createElement('span');
        letter.className = 'fc-option-label';
        letter.textContent = String.fromCharCode(65 + optionIndex);
        const content = document.createElement('span');
        content.className = 'fc-option-text';
        content.textContent = option.Content || '';
        element.append(letter, content);
        return element;
    }

    function render() {
        const card = cards[index];
        flipped = false;
        cardInner.classList.remove('fc-card--flipped');
        cardInner.setAttribute('aria-pressed', 'false');
        cardInner.setAttribute('aria-label', 'Lật thẻ để xem đáp án');
        cardInner.querySelector('.fc-face--front').setAttribute('aria-hidden', 'false');
        cardInner.querySelector('.fc-face--back').setAttribute('aria-hidden', 'true');
        document.getElementById('questionTextFront').textContent = card.Content || '';
        document.getElementById('cardTypeFront').textContent = card.QuestionType === 1 ? 'Một đáp án đúng' : 'Nhiều đáp án đúng';
        document.getElementById('optionsPanel').replaceChildren(...card.AllAnswers.map((option, i) => optionElement(option, i, false)));
        document.getElementById('backAnswerList').replaceChildren(...card.AllAnswers.map((option, i) => optionElement(option, i, true)));
        document.getElementById('backExplanation').textContent = card.Explanation || '';
        document.getElementById('backExplanation').classList.toggle('d-none', !card.Explanation);
        document.getElementById('cardCounter').textContent = 'Thẻ ' + (index + 1) + '/' + cards.length;
        const known = [...status.values()].filter(Boolean).length;
        document.getElementById('studyCounter').textContent = known + ' đã thuộc · ' + (status.size - known) + ' chưa thuộc';
        for (const badge of cardInner.querySelectorAll('[id^="cardStatusBadge"]')) {
            const marked = status.has(card.QuestionId);
            badge.className = 'badge ' + (status.get(card.QuestionId) ? 'bg-success' : 'bg-danger');
            badge.textContent = status.get(card.QuestionId) ? 'Đã thuộc' : 'Chưa thuộc';
            badge.classList.toggle('d-none', !marked);
        }
        controls[2].classList.add('d-none');
        controls[3].classList.add('d-none');
    }

    function flip() {
        if (busy || app.hidden) return;
        flipped = !flipped;
        cardInner.classList.toggle('fc-card--flipped', flipped);
        cardInner.setAttribute('aria-pressed', String(flipped));
        cardInner.setAttribute('aria-label', flipped ? 'Lật lại để xem câu hỏi' : 'Lật thẻ để xem đáp án');
        cardInner.querySelector('.fc-face--front').setAttribute('aria-hidden', String(flipped));
        cardInner.querySelector('.fc-face--back').setAttribute('aria-hidden', String(!flipped));
        controls[2].classList.toggle('d-none', !flipped);
        controls[3].classList.toggle('d-none', !flipped);
    }

    function move(delta) {
        if (busy || app.hidden) return;
        index = (index + delta + cards.length) % cards.length;
        render();
        animateCard(delta);
    }

    function reviewId() {
        if (crypto.randomUUID) return crypto.randomUUID();
        const bytes = crypto.getRandomValues(new Uint8Array(16));
        bytes[6] = (bytes[6] & 15) | 64;
        bytes[8] = (bytes[8] & 63) | 128;
        const hex = Array.from(bytes, byte => byte.toString(16).padStart(2, '0')).join('');
        return [hex.slice(0, 8), hex.slice(8, 12), hex.slice(12, 16), hex.slice(16, 20), hex.slice(20)].join('-');
    }

    async function mark(remembered) {
        if (busy || !flipped || app.hidden) return;
        busy = true;
        controls.forEach(control => { control.disabled = true; });
        const questionId = cards[index].QuestionId;
        const retryKey = questionId + ':' + remembered;
        let review = directRetries.get(retryKey);
        try {
            if (!review) {
                review = { id: reviewId(), questionId, remembered, createdAt: Date.now() };
                directRetries.set(retryKey, review);
            }
            let stored = false;
            if (!storageProblem) {
                try { outbox.add(review); stored = true; }
                catch { /* Save online if the browser has no storage space. */ }
            }
            if (!stored) await send(review);
            directRetries.delete(retryKey);
            status.set(questionId, remembered);
            if (status.size === cards.length) showComplete();
            else {
                index = (index + 1) % cards.length;
                render();
                animateCard(remembered ? -1 : 1);
            }
            if (stored) sync();
            else showStatus('', false);
        } catch {
            showStatus('Chưa lưu được tiến độ. Hãy thử đánh dấu lại thẻ này khi có kết nối.', false);
        } finally {
            busy = false;
            controls.forEach(control => { control.disabled = false; });
        }
    }

    retry.addEventListener('click', sync);
    window.addEventListener('online', sync);
    window.addEventListener('storage', updateLinks);
    document.addEventListener('visibilitychange', () => { if (!document.hidden) sync(); });
    document.querySelectorAll('[data-needs-sync]').forEach(link => {
        link.addEventListener('click', event => {
            if (link.getAttribute('aria-disabled') === 'true') event.preventDefault();
        });
    });

    if (cards.length && app) {
        cardInner.addEventListener('click', flip);
        controls[0].addEventListener('click', () => move(-1));
        controls[1].addEventListener('click', () => move(1));
        controls[2].addEventListener('click', () => mark(false));
        controls[3].addEventListener('click', () => mark(true));
        controls[4].addEventListener('click', () => {
            if (busy) return;
            for (let last = cards.length - 1; last > 0; last--) {
                const random = Math.floor(Math.random() * (last + 1));
                [cards[last], cards[random]] = [cards[random], cards[last]];
            }
            index = 0;
            render();
        });
        document.addEventListener('keydown', event => {
            if (busy || app.hidden || event.altKey || event.ctrlKey || event.metaKey || event.repeat ||
                event.target.closest('input, textarea, select, [contenteditable="true"]')) return;
            if ([' ', 'Enter'].includes(event.key) && event.target.closest('button, a')) return;
            if (event.key === 'ArrowRight') { event.preventDefault(); move(1); }
            else if (event.key === 'ArrowLeft') { event.preventDefault(); move(-1); }
            else if ([' ', 'ArrowUp', 'ArrowDown'].includes(event.key)) { event.preventDefault(); flip(); }
            else if (event.key === 'Enter') { event.preventDefault(); flipped ? mark(true) : flip(); }
            else if (event.key.toLowerCase() === 'r' && flipped) mark(false);
        });
        render();
    } else if (app) showComplete();
    sync();
})();
