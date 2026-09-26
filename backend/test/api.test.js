import test from 'node:test';
import assert from 'node:assert/strict';
import { createServer } from 'node:http';
import { createApi } from '../src/api.js';
import { MemoryAdapter } from './memory.js';

const fix = (time = 1_000_000, latitude = 35.68) => ({ latitude, longitude: 139.76, accuracyMeters: 8, measuredUnixSeconds: time });
const draft = (operationId = 'op-one', time = 1_000_000) => ({ operationId, presetId: 'taggi-1', place: 'Tokyo', teaser: 'A little hello', note: 'The secret note', location: fix(time), position: { x: 0, y: 0, z: 0 }, rotation: { x: 0, y: 0, z: 0, w: 1 }, widthMeters: 0.2, mapBytes: 12 });

async function fixture(options = {}) {
    const adapter = new MemoryAdapter();
    const server = createServer(createApi({ adapter, now: () => 1_000_000, ids: (() => { let n = 0; return () => `id-${++n}`; })(), ...options }));
    await new Promise(resolve => server.listen(0, '127.0.0.1', resolve));
    const root = `http://127.0.0.1:${server.address().port}`;
    async function call(method, path, body, user = 'alice') {
        const response = await fetch(root + path, { method, headers: { ...(user ? { authorization: `Bearer ${user}` } : {}), 'content-type': 'application/json' }, body: body === undefined ? undefined : JSON.stringify(body) });
        return { status: response.status, data: await response.json() };
    }
    async function publish(owner = 'alice', operationId = 'op-one') {
        const prepared = await call('POST', '/v1/publications/prepare', draft(operationId), owner);
        assert.equal(prepared.status, 200);
        adapter.upload(prepared.data.id, 12);
        const finalized = await call('POST', `/v1/publications/${prepared.data.id}/finalize`, { operationId, location: fix() }, owner);
        assert.equal(finalized.status, 200);
        return prepared.data.id;
    }
    return { adapter, call, publish, close: () => new Promise(resolve => server.close(resolve)) };
}

test('authentication gates writes and invalid optional tokens fail closed', async () => {
    const f = await fixture();
    try {
        assert.equal((await f.call('POST', '/v1/publications/prepare', draft(), null)).status, 401);
        assert.equal((await f.call('POST', '/v1/nearby', { location: fix() }, 'invalid')).status, 401);
        assert.equal((await f.call('GET', '/v1/collection', undefined, 'invalid')).status, 401);
    } finally { await f.close(); }
});

test('public nearby hides notes, map URLs and exact placement recovery fields', async () => {
    const f = await fixture();
    try {
        await f.publish();
        const result = await f.call('POST', '/v1/nearby', { location: fix() }, null);
        assert.equal(result.status, 200);
        assert.equal(result.data.items.length, 1);
        assert.equal(JSON.stringify(result.data).includes('The secret note'), false);
        assert.equal(JSON.stringify(result.data).includes('mapUrl'), false);
        assert.equal(JSON.stringify(result.data).includes('position'), false);
    } finally { await f.close(); }
});

test('prepare retries are idempotent and incomplete upload cannot finalize', async () => {
    const f = await fixture();
    try {
        const one = await f.call('POST', '/v1/publications/prepare', draft());
        const two = await f.call('POST', '/v1/publications/prepare', draft());
        assert.equal(one.data.id, two.data.id);
        assert.equal((await f.call('POST', `/v1/publications/${one.data.id}/finalize`, { operationId: 'op-one', location: fix() })).status, 409);
        f.adapter.upload(one.data.id, 11);
        assert.equal((await f.call('POST', `/v1/publications/${one.data.id}/finalize`, { operationId: 'op-one', location: fix() })).status, 409);
        f.adapter.upload(one.data.id, 12);
        assert.equal((await f.call('POST', `/v1/publications/${one.data.id}/finalize`, { operationId: 'op-one', location: fix() })).status, 200);
        assert.equal((await f.call('POST', `/v1/publications/${one.data.id}/finalize`, { operationId: 'op-one', location: fix() })).status, 200);
    } finally { await f.close(); }
});

test('publication accepts a fresh 75 metre fix through finalization', async () => {
    const f = await fixture();
    try {
        const publication = await f.call('POST', '/v1/publications/prepare', {
            ...draft('op-75m'), location: { ...fix(), accuracyMeters: 75 }
        });
        assert.equal(publication.status, 200);
        f.adapter.upload(publication.data.id, 12);
        const finalized = await f.call('POST', `/v1/publications/${publication.data.id}/finalize`, {
            operationId: 'op-75m', location: { ...fix(), accuracyMeters: 75 }
        });
        assert.equal(finalized.status, 200);
    } finally { await f.close(); }
});

test('prepare retry accepts fresh GPS drift but preserves first placement', async () => {
    const f = await fixture();
    try {
        const first = await f.call('POST', '/v1/publications/prepare', draft());
        const shifted = { ...draft(), location: { ...fix(), latitude: 35.6803, longitude: 139.7602 } };
        const retry = await f.call('POST', '/v1/publications/prepare', shifted);
        assert.equal(retry.status, 200);
        assert.equal(retry.data.id, first.data.id);
        assert.equal((await f.adapter.get('stickers', first.data.id)).latitude, 35.68);
        assert.equal((await f.call('POST', '/v1/publications/prepare', { ...shifted, location: fix(1_000_000, 35.69) })).status, 403);
        assert.equal((await f.call('POST', '/v1/publications/prepare', { ...shifted, teaser: 'Changed' })).status, 409);
    } finally { await f.close(); }
});

test('stale, inaccurate and malformed input is rejected', async () => {
    const f = await fixture();
    try {
        assert.equal((await f.call('POST', '/v1/nearby', { location: fix(999_969) }, null)).status, 400);
        assert.equal((await f.call('POST', '/v1/nearby', { location: { ...fix(), accuracyMeters: 5001 } }, null)).status, 400);
        assert.equal((await f.call('POST', '/v1/nearby', { location: { ...fix(), accuracyMeters: -1 } }, null)).status, 400);
        assert.equal((await f.call('POST', '/v1/publications/prepare', { ...draft('op-75m'), location: { ...fix(), accuracyMeters: 75 } })).status, 200);
        assert.equal((await f.call('POST', '/v1/publications/prepare', { ...draft(), note: 'x'.repeat(2001) })).status, 400);
        assert.equal((await f.call('POST', '/v1/publications/prepare', { ...draft(), mapBytes: 16 * 1024 * 1024 + 1 })).status, 400);
    } finally { await f.close(); }
});

test('discovery requires proximity, valid session and gives one collection across races', async () => {
    const f = await fixture();
    try {
        const id = await f.publish();
        assert.equal((await f.call('POST', `/v1/stickers/${id}/recover`, { location: fix() }, 'bob')).status, 200);
        const recovery = await f.call('POST', `/v1/stickers/${id}/recover`, { location: fix() }, 'bob');
        assert.equal(recovery.data.mapUrl.startsWith('https://'), true);
        assert.equal((await f.call('POST', `/v1/stickers/${id}/collect`, { discoveryId: 'wrong', location: fix() }, 'bob')).status, 403);
        assert.equal((await f.call('POST', `/v1/stickers/${id}/collect`, { discoveryId: recovery.data.discoveryId, location: fix(1_000_000, 35.69) }, 'bob')).status, 403);
        const responses = await Promise.all(Array.from({ length: 5 }, () => f.call('POST', `/v1/stickers/${id}/collect`, { discoveryId: recovery.data.discoveryId, location: fix() }, 'bob')));
        assert.deepEqual(responses.map(x => x.status), [200, 200, 200, 200, 200]);
        assert.equal(responses[0].data.sticker.note, 'The secret note');
        assert.equal((await f.call('GET', '/v1/collection', undefined, 'bob')).data.items.length, 1);
        assert.equal((await f.call('POST', '/v1/nearby', { location: fix() }, null)).data.items.length, 1);
    } finally { await f.close(); }
});

test('blocks, withdrawal, moderation and deletion revoke appropriate access', async () => {
    const f = await fixture();
    try {
        const id = await f.publish();
        const recovery = await f.call('POST', `/v1/stickers/${id}/recover`, { location: fix() }, 'bob');
        await f.call('POST', `/v1/stickers/${id}/collect`, { discoveryId: recovery.data.discoveryId, location: fix() }, 'bob');
        await f.call('POST', `/v1/stickers/${id}/report`, { reason: 'inappropriate' }, 'bob');
        assert.equal((await f.call('GET', '/v1/admin/reports', undefined, 'bob')).status, 403);
        assert.equal((await f.call('POST', `/v1/stickers/${id}/withdraw`, {}, 'bob')).status, 403);
        assert.equal((await f.call('POST', `/v1/stickers/${id}/withdraw`, {}, 'alice')).status, 200);
        assert.equal((await f.call('POST', `/v1/stickers/${id}/recover`, { location: fix() }, 'bob')).status, 404);
        assert.equal((await f.call('GET', '/v1/collection', undefined, 'bob')).data.items[0].note, 'The secret note');
        await f.call('POST', '/v1/admin/stickers/' + id + '/moderate', { status: 'removed' }, 'admin');
        const moderated = (await f.call('GET', '/v1/collection', undefined, 'bob')).data.items[0];
        assert.equal(moderated.unavailable, true);
        assert.equal(moderated.note, '');
        await f.call('POST', '/v1/admin/stickers/' + id + '/moderate', { status: 'published' }, 'admin');
        await f.call('DELETE', '/v1/account', undefined, 'alice');
        assert.equal((await f.call('GET', '/v1/collection', undefined, 'bob')).data.items[0].unavailable, true);
        assert.equal((await f.call('GET', '/v1/collection', undefined, 'bob')).data.items[0].note, '');
        assert.equal((await f.call('POST', `/v1/stickers/${id}/recover`, { location: fix() }, 'bob')).status, 404);
    } finally { await f.close(); }
});

test('blocked author is hidden from nearby and recovery; quota caps daily publications', async () => {
    const f = await fixture();
    try {
        const id = await f.publish();
        await f.call('POST', '/v1/blocks', { authorId: 'alice' }, 'bob');
        assert.equal((await f.call('POST', '/v1/nearby', { location: fix() }, 'bob')).data.items.length, 0);
        assert.equal((await f.call('POST', `/v1/stickers/${id}/recover`, { location: fix() }, 'bob')).status, 403);
        for (let n = 2; n <= 5; n++) await f.publish('alice', `op-${n}`);
        assert.equal((await f.call('POST', '/v1/publications/prepare', draft('op-six'))).status, 429);
    } finally { await f.close(); }
});

test('content filter rejects disallowed text and request size is bounded', async () => {
    const f = await fixture();
    try {
        assert.equal((await f.call('POST', '/v1/publications/prepare', { ...draft(), teaser: 'buy crypto now' })).status, 400);
        assert.equal((await f.call('POST', '/v1/publications/prepare', { ...draft(), note: 'x'.repeat(33000) })).status, 413);
    } finally { await f.close(); }
});

test('signed map URL is issued only for live, nearby, unblocked discovery', async () => {
    const f = await fixture();
    try {
        const id = await f.publish();
        assert.equal((await f.call('POST', `/v1/stickers/${id}/recover`, { location: fix(1_000_000, 35.69) }, 'bob')).status, 403);
        await f.call('POST', '/v1/blocks', { authorId: 'alice' }, 'bob');
        assert.equal((await f.call('POST', `/v1/stickers/${id}/recover`, { location: fix() }, 'bob')).status, 403);
        const allowed = await f.call('POST', `/v1/stickers/${id}/recover`, { location: fix() }, 'admin');
        assert.equal(allowed.status, 200);
        assert.equal(allowed.data.mapUrl.startsWith('https://'), true);
        await f.call('POST', `/v1/admin/stickers/${id}/moderate`, { status: 'removed' }, 'admin');
        assert.equal((await f.call('POST', `/v1/stickers/${id}/recover`, { location: fix() }, 'admin')).status, 404);
    } finally { await f.close(); }
});

test('only admin claim can list reports and runtime cannot change custom claims', async () => {
    const f = await fixture();
    try {
        const id = await f.publish();
        await f.call('POST', `/v1/stickers/${id}/report`, { reason: 'unsafe' }, 'bob');
        assert.equal((await f.call('GET', '/v1/admin/reports', undefined, 'bob')).status, 403);
        assert.equal((await f.call('POST', '/v1/admin/users/bob/admin', { enabled: true }, 'bob')).status, 403);
        assert.equal((await f.call('GET', '/v1/admin/reports', undefined, 'admin')).data.items.length, 1);
        assert.equal((await f.call('POST', '/v1/admin/users/bob/admin', { enabled: true }, 'admin')).status, 404);
    } finally { await f.close(); }
});

test('operator can list removed stickers and restore a moderated publication', async () => {
    const f = await fixture();
    try {
        const id = await f.publish();
        assert.equal((await f.call('GET', '/v1/admin/removed', undefined, 'bob')).status, 403);
        await f.call('POST', `/v1/admin/stickers/${id}/moderate`, { status: 'removed' }, 'admin');
        assert.deepEqual((await f.call('GET', '/v1/admin/removed', undefined, 'admin')).data.items.map(item => item.id), [id]);
        await f.call('POST', `/v1/admin/stickers/${id}/moderate`, { status: 'published' }, 'admin');
        assert.equal((await f.call('GET', '/v1/admin/removed', undefined, 'admin')).data.items.length, 0);
        assert.equal((await f.call('POST', '/v1/nearby', { location: fix() }, null)).data.items.length, 1);
    } finally { await f.close(); }
});

test('failed self-service account deletion keeps authored and collected content', async () => {
    const f = await fixture();
    try {
        const id = await f.publish();
        f.adapter.deleteAuthFailure = Object.assign(new Error('Recent login required'), { code: 'auth/recent-login-required' });
        const response = await f.call('DELETE', '/v1/account', undefined, 'alice');
        assert.equal(response.status, 401);
        assert.equal((await f.adapter.get('stickers', id)).status, 'published');
        assert.equal((await f.call('GET', '/v1/authored', undefined, 'alice')).status, 200);
    } finally { await f.close(); }
});

test('author tombstone hides notes while cleanup is pending after Auth deletion', async () => {
    const f = await fixture({ deleteAccountData: async () => { throw new Error('cleanup unavailable'); } });
    try {
        const id = await f.publish();
        const recovery = await f.call('POST', `/v1/stickers/${id}/recover`, { location: fix() }, 'bob');
        await f.call('POST', `/v1/stickers/${id}/collect`, { discoveryId: recovery.data.discoveryId, location: fix() }, 'bob');
        const deleted = await f.call('DELETE', '/v1/account', undefined, 'alice');
        assert.equal(deleted.status, 200);
        assert.equal(deleted.data.cleanupPending, true);
        assert.equal((await f.call('POST', '/v1/nearby', { location: fix() }, null)).data.items.length, 0);
        assert.equal((await f.call('POST', `/v1/stickers/${id}/recover`, { location: fix() }, 'bob')).status, 404);
        const entry = (await f.call('GET', '/v1/collection', undefined, 'bob')).data.items[0];
        assert.equal(entry.unavailable, true);
        assert.equal(entry.note, '');
    } finally { await f.close(); }
});

test('per-instance limiter bounds anonymous browsing before store query', async () => {
    const f = await fixture();
    try {
        const statuses = await Promise.all(Array.from({ length: 31 }, () => f.call('POST', '/v1/nearby', { location: fix() }, null)));
        assert.equal(statuses.filter(item => item.status === 200).length, 30);
        assert.equal(statuses.filter(item => item.status === 429).length, 1);
    } finally { await f.close(); }
});

test('per-instance limiter bounds recovery and reports per user', async () => {
    const f = await fixture();
    try {
        const id = await f.publish();
        const recoveries = await Promise.all(Array.from({ length: 21 }, () => f.call('POST', `/v1/stickers/${id}/recover`, { location: fix() }, 'bob')));
        assert.equal(recoveries.filter(item => item.status === 429).length, 1);
        const reports = await Promise.all(Array.from({ length: 11 }, () => f.call('POST', `/v1/stickers/${id}/report`, { reason: 'unsafe' }, 'bob')));
        assert.equal(reports.filter(item => item.status === 429).length, 1);
    } finally { await f.close(); }
});


test('nearby browsing accepts approximate fixes without relaxing discovery or publishing', async () => {
    const f = await fixture();
    try {
        const id = await f.publish();
        for (const accuracyMeters of [75, 500, 2000, 2000.149, 5000]) {
            const approximate = { ...fix(), accuracyMeters };
            const nearby = await f.call('POST', '/v1/nearby', { location: approximate }, null);
            assert.equal(nearby.status, 200, `Browse with ${accuracyMeters} metre accuracy`);
            assert.equal(nearby.data.items.length, 1);
            assert.equal(nearby.data.items[0].note, undefined);
            assert.equal((await f.call('POST', `/v1/stickers/${id}/recover`, { location: approximate }, 'bob')).status, 400);
            assert.equal((await f.call('POST', `/v1/stickers/${id}/collect`, { location: approximate, discoveryId: 'unused' }, 'bob')).status, 400);
        }
        assert.equal((await f.call('POST', '/v1/nearby', { location: { ...fix(), accuracyMeters: 5001 } }, null)).status, 400);
        assert.equal((await f.call('POST', '/v1/nearby', { location: { ...fix(999_969), accuracyMeters: 500 } }, null)).status, 400);
        assert.equal((await f.call('POST', '/v1/publications/prepare', { ...draft('approximate'), location: { ...fix(), accuracyMeters: 500 } })).status, 400);
    } finally { await f.close(); }
});
