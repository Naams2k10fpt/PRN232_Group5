(function (root) {
    'use strict';

    class ReviewOutbox {
        constructor(storage, accountId, deckId) {
            this.storage = storage;
            this.prefix = 'quizms.flashcard.review.v1.' + JSON.stringify([accountId, deckId]) + '.';
            this.sending = false;
        }

        list() {
            const reviews = [];
            for (let index = 0; index < this.storage.length; index++) {
                const key = this.storage.key(index);
                if (!key?.startsWith(this.prefix)) continue;
                const review = JSON.parse(this.storage.getItem(key));
                if (!review || typeof review.id !== 'string' ||
                    key !== this.prefix + review.id ||
                    !Number.isInteger(review.questionId) || review.questionId <= 0 ||
                    typeof review.remembered !== 'boolean' || !Number.isFinite(review.createdAt)) {
                    throw new Error('Invalid stored flashcard review.');
                }
                reviews.push(review);
            }
            return reviews.sort((a, b) => a.createdAt - b.createdAt || a.id.localeCompare(b.id));
        }

        add(review) {
            this.storage.setItem(this.prefix + review.id, JSON.stringify(review));
        }

        async flush(send) {
            if (this.sending) return;
            this.sending = true;
            try {
                while (true) {
                    const review = this.list()[0];
                    if (!review) break;
                    await send(review);
                    // Each review has its own key so a response cannot overwrite
                    // a newer review queued by another tab.
                    this.storage.removeItem(this.prefix + review.id);
                }
            } finally {
                this.sending = false;
            }
        }
    }

    if (typeof module !== 'undefined' && module.exports) module.exports = { ReviewOutbox };
    else root.QuizFlashcardReviews = { ReviewOutbox };
})(typeof window !== 'undefined' ? window : globalThis);
