import test from 'node:test';
import assert from 'node:assert/strict';
import { createRequire } from 'node:module';

const require = createRequire(import.meta.url);
const { ReviewOutbox } = require('../../QuizManagement/wwwroot/js/flashcard-reviews.js');

function memoryStorage() {
    const values = new Map();
    return {
        get length() { return values.size; },
        key: index => [...values.keys()][index] ?? null,
        getItem: key => values.get(key) ?? null,
        setItem: (key, value) => values.set(key, value),
        removeItem: key => values.delete(key)
    };
}

const review = (id, questionId = 10) => ({ id, questionId, remembered: false, createdAt: 1 });

test('a lost response is retried with the same identity after reopening', async () => {
    const storage = memoryStorage();
    const first = new ReviewOutbox(storage, 'student-a', 4);
    first.add(review('first'));
    const received = new Set();
    const send = async entry => {
        received.add(entry.id);
        throw new Error('Response lost');
    };
    await assert.rejects(first.flush(send));

    const reopened = new ReviewOutbox(storage, 'student-a', 4);
    assert.equal(reopened.list()[0].id, 'first');
    await reopened.flush(async entry => received.add(entry.id));
    assert.equal(received.size, 1);
    assert.deepEqual(reopened.list(), []);
});

test('acknowledging one review preserves a review added by another tab', async () => {
    const storage = memoryStorage();
    const first = new ReviewOutbox(storage, 'student-a', 4);
    const second = new ReviewOutbox(storage, 'student-a', 4);
    first.add(review('first'));
    const sent = [];
    await first.flush(async entry => {
        sent.push(entry.id);
        if (entry.id === 'first') second.add(review('second', 11));
    });
    assert.deepEqual(sent, ['first', 'second']);
    assert.deepEqual(second.list(), []);
});

test('queued reviews are isolated by account and deck', async () => {
    const storage = memoryStorage();
    const first = new ReviewOutbox(storage, 'student-a', 4);
    first.add(review('first'));
    assert.deepEqual(new ReviewOutbox(storage, 'student-b', 4).list(), []);
    assert.deepEqual(new ReviewOutbox(storage, 'student-a', 5).list(), []);
    await new ReviewOutbox(storage, 'student-b', 4).flush(() => assert.fail('Wrong account'));
    assert.equal(first.list().length, 1);
});
