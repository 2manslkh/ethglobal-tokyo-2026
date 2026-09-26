import test from 'node:test';
import assert from 'node:assert/strict';
import { createServer } from 'node:http';
import { createApi } from '../src/api.js';
import { MemoryAdapter } from './memory.js';
import { distanceMeters } from '../src/geo.js';

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

test('all thirteen presets retain their exact IDs through publication and collection', async () => {
    const f = await fixture();
    try {
        const expected = new Map();
        for (let index = 1; index <= 13; index++) {
            const presetId = `taggi-${index}`;
            const operationId = `preset-${index}`;
            const author = index <= 6 ? 'alice' : 'admin';
            const prepared = await f.call('POST', '/v1/publications/prepare', {
                ...draft(operationId), presetId
            }, author);
            assert.equal(prepared.status, 200, presetId);
            f.adapter.upload(prepared.data.id, 12);
            const finalized = await f.call('POST', `/v1/publications/${prepared.data.id}/finalize`,
                { operationId, location: fix() }, author);
            assert.equal(finalized.status, 200, presetId);
            assert.equal(finalized.data.sticker.presetId, presetId);
            const recovery = await f.call('POST', `/v1/stickers/${prepared.data.id}/recover`, { location: fix() }, 'bob');
            assert.equal(recovery.status, 200, presetId);
            const collected = await f.call('POST', `/v1/stickers/${prepared.data.id}/collect`,
                { location: fix(), discoveryId: recovery.data.discoveryId }, 'bob');
            assert.equal(collected.status, 200, presetId);
            assert.equal(collected.data.sticker.presetId, presetId);
            expected.set(prepared.data.id, presetId);
        }
        const collection = await f.call('GET', '/v1/collection', undefined, 'bob');
        assert.equal(collection.status, 200);
        assert.equal(collection.data.items.length, 13);
        for (const item of collection.data.items) assert.equal(item.presetId, expected.get(item.id));
    } finally { await f.close(); }
});

test('publication rejects malformed and unknown preset IDs', async () => {
    const f = await fixture();
    try {
        for (const presetId of ['taggi-0', 'taggi-14', 'taggi-01', 'taggi--1', 'taggi-1x', 'taggi-13 ', 'Taggi-1', 13]) {
            const result = await f.call('POST', '/v1/publications/prepare', { ...draft(), presetId });
            assert.equal(result.status, 400, String(presetId));
        }
        assert.equal((await f.adapter.query('stickers')).length, 0);
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

test('Unity default zero-valued pin is ignored without explicit confirmation', async () => {
    const f = await fixture();
    try {
        const result = await f.call('POST', '/v1/publications/prepare', {
            ...draft('unity-precise'), locationConfirmed: false, confirmedLocation: { latitude: 0, longitude: 0 }
        });
        assert.equal(result.status, 200);
        const saved = await f.adapter.get('stickers', result.data.id);
        assert.equal(saved.latitude, 35.68);
        assert.equal(saved.locationSource, 'device');
    } finally { await f.close(); }
});

test('map-confirmed publication retains real uncertainty and publishes at the confirmed pin', async () => {
    const f = await fixture();
    try {
        const measured = { ...fix(), accuracyMeters: 2000.149 };
        const confirmedLocation = { latitude: 35.681, longitude: 139.761 };
        const prepared = await f.call('POST', '/v1/publications/prepare', {
            ...draft('confirmed'), location: measured, confirmedLocation, locationConfirmed: true
        });
        assert.equal(prepared.status, 200);
        const saved = await f.adapter.get('stickers', prepared.data.id);
        assert.equal(saved.latitude, confirmedLocation.latitude);
        assert.equal(saved.longitude, confirmedLocation.longitude);
        assert.equal(saved.locationSource, 'map-confirmed');
        assert.equal(saved.locationAccuracyMeters, measured.accuracyMeters);
        f.adapter.upload(prepared.data.id, 12);
        assert.equal((await f.call('POST', `/v1/publications/${prepared.data.id}/finalize`, {
            operationId: 'confirmed', location: measured, confirmedLocation, locationConfirmed: true
        })).status, 200);
        assert.equal((await f.call('POST', `/v1/stickers/${prepared.data.id}/recover`, {
            location: measured
        }, 'bob')).status, 200);
    } finally { await f.close(); }
});

test('map confirmation cannot bypass freshness, uncertainty bounds or measured proximity', async () => {
    const f = await fixture();
    try {
        const confirmedLocation = { latitude: 35.681, longitude: 139.761 };
        for (const location of [fix(999_969), { ...fix(), accuracyMeters: 5001 },
            { ...fix(), accuracyMeters: -1 }, { ...fix(), accuracyMeters: 2000, latitude: 36 }]) {
            assert.equal((await f.call('POST', '/v1/publications/prepare', {
                ...draft(), location, confirmedLocation, locationConfirmed: true
            })).status, 400);
        }
        assert.equal((await f.call('POST', '/v1/publications/prepare', {
            ...draft(), location: { ...fix(), accuracyMeters: 2000 }
        })).status, 400);
    } finally { await f.close(); }
});

test('confirmed pin is immutable across publication retries and finalization', async () => {
    const f = await fixture();
    try {
        const location = { ...fix(), accuracyMeters: 2000 };
        const confirmedLocation = { latitude: 35.681, longitude: 139.761 };
        const input = { ...draft('pin-retry'), location, confirmedLocation, locationConfirmed: true };
        const first = await f.call('POST', '/v1/publications/prepare', input);
        assert.equal(first.status, 200);
        assert.equal((await f.call('POST', '/v1/publications/prepare', {
            ...input, location: { ...location, latitude: 35.682, accuracyMeters: 1800 }
        })).status, 200);
        const changed = { ...confirmedLocation, latitude: 35.683 };
        assert.equal((await f.call('POST', '/v1/publications/prepare', {
            ...input, confirmedLocation: changed
        })).status, 409);
        f.adapter.upload(first.data.id, 12);
        assert.equal((await f.call('POST', `/v1/publications/${first.data.id}/finalize`, {
            operationId: 'pin-retry', location, confirmedLocation: changed, locationConfirmed: true
        })).status, 409);
        assert.equal((await f.call('POST', `/v1/publications/${first.data.id}/finalize`, {
            operationId: 'pin-retry', location
        })).status, 400);
    } finally { await f.close(); }
});

test('publication operation lookup recovers only the authenticated owners original pin', async () => {
    const f = await fixture();
    try {
        const input = draft('legacy-lookup');
        await f.call('POST', '/v1/publications/prepare', input);
        await f.call('POST', '/v1/publications/prepare', { ...input, location: fix(1_000_000, 35.6803) });
        const path = '/v1/publications/operations/legacy-lookup';
        const recovered = await f.call('GET', path);
        assert.equal(recovered.status, 200);
        assert.deepEqual(recovered.data, { found: true, locationConfirmed: false,
            publicationLocation: { latitude: 35.68, longitude: 139.76 } });
        assert.deepEqual((await f.call('GET', path, undefined, 'bob')).data, { found: false });
        assert.equal((await f.call('GET', path, undefined, null)).status, 401);
    } finally { await f.close(); }
});

test('a client-locked precise publication retains its pin across drift before the first prepare succeeds', async () => {
    const f = await fixture();
    try {
        const confirmedLocation = { latitude: 35.68, longitude: 139.76 };
        const initial = { ...draft('precise-lock'), location: fix(1_000_000, 35.6803),
            hasPublicationLocation: true, locationConfirmed: false, confirmedLocation };
        const first = await f.call('POST', '/v1/publications/prepare', initial);
        assert.equal(first.status, 200);
        assert.equal((await f.adapter.get('stickers', first.data.id)).latitude, confirmedLocation.latitude);
        assert.deepEqual(first.data.publicationLocation, confirmedLocation);
        assert.equal((await f.call('POST', '/v1/publications/prepare', { ...initial,
            location: { ...fix(), accuracyMeters: 2000 }, locationConfirmed: true
        })).status, 200);
    } finally { await f.close(); }
});

test('a precise prepared post can finish with explicit confirmation of its original pin', async () => {
    const f = await fixture();
    try {
        const initial = draft('precision-change');
        const first = await f.call('POST', '/v1/publications/prepare', initial);
        assert.equal(first.status, 200);
        const confirmation = { locationConfirmed: true,
            confirmedLocation: { latitude: initial.location.latitude, longitude: initial.location.longitude },
            location: { ...fix(), accuracyMeters: 2000 } };
        const retry = await f.call('POST', '/v1/publications/prepare', { ...initial, ...confirmation });
        assert.equal(retry.status, 200);
        assert.equal(retry.data.id, first.data.id);
        f.adapter.upload(first.data.id, 12);
        const finished = await f.call('POST', `/v1/publications/${first.data.id}/finalize`, {
            operationId: initial.operationId, ...confirmation
        });
        assert.equal(finished.status, 200);
        assert.equal(finished.data.sticker.latitude, initial.location.latitude);
        assert.equal(JSON.stringify(finished.data).includes('measuredLocation'), false);
        assert.equal((await f.call('POST', '/v1/publications/prepare', { ...initial, ...confirmation,
            confirmedLocation: { ...confirmation.confirmedLocation, latitude: 35.681 }
        })).status, 409);
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
        assert.equal(responses.filter(x => x.data.isNew).length, 1);
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

test('blocked author is hidden from nearby and recovery', async () => {
    const f = await fixture();
    try {
        const id = await f.publish();
        await f.call('POST', '/v1/blocks', { authorId: 'alice' }, 'bob');
        assert.equal((await f.call('POST', '/v1/nearby', { location: fix() }, 'bob')).data.items.length, 0);
        assert.equal((await f.call('POST', `/v1/stickers/${id}/recover`, { location: fix() }, 'bob')).status, 403);

    } finally { await f.close(); }
});

test('daily publication quota allows 100 new preparations and retries but rejects 101', async () => {
    const f = await fixture();
    try {
        for (let n = 1; n <= 100; n++) {
            assert.equal((await f.call('POST', '/v1/publications/prepare', draft(`op-${n}`))).status, 200, `Publication ${n}`);
        }
        assert.equal((await f.call('POST', '/v1/publications/prepare', draft('op-100'))).status, 200);
        const rejected = await f.call('POST', '/v1/publications/prepare', draft('op-101'));
        assert.equal(rejected.status, 429);
        assert.equal(rejected.data.error.code, 'quota_exceeded');
        assert.equal((await f.call('POST', '/v1/publications/prepare', draft('op-1'), 'bob')).status, 200);
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

test('authored pagination filters before the page limit and orders equal timestamps by id', async () => {
    const f = await fixture();
    try {
        for (let n = 0; n < 1005; n++) {
            const id = `sticker-${String(n).padStart(4, '0')}`;
            await f.adapter.set('stickers', id, { id, authorId: 'alice', status: n % 2 ? 'withdrawn' : 'published',
                createdAt: n < 4 ? 2000 : 1000 + n, presetId: 'taggi-1', revision: 1 });
        }
        await f.adapter.set('stickers', 'other-owner', { id: 'other-owner', authorId: 'bob', status: 'published',
            createdAt: 999999, presetId: 'taggi-1', revision: 1 });
        const seen = [];
        let cursor;
        do {
            const path = `/v1/authored?status=published&limit=50${cursor ? `&cursor=${encodeURIComponent(cursor)}` : ''}`;
            const page = await f.call('GET', path);
            assert.equal(page.status, 200);
            assert.ok(page.data.items.length <= 50);
            assert.ok(page.data.items.every(item => item.status === 'published'));
            seen.push(...page.data.items.map(item => item.id));
            cursor = page.data.nextCursor;
        } while (cursor);
        assert.equal(seen.length, 503);
        assert.equal(new Set(seen).size, 503);
        assert.deepEqual(seen.slice(-2), ['sticker-0006', 'sticker-0004']);
        assert.ok(seen.indexOf('sticker-0002') < seen.indexOf('sticker-0000'));
        assert.equal(seen.includes('other-owner'), false);
    } finally { await f.close(); }
});

test('authored cursor stays bound to its owner and status while the legacy list retains its shape', async () => {
    const f = await fixture();
    try {
        const first = await f.publish('alice', 'first');
        const second = await f.publish('alice', 'second');
        await f.call('POST', `/v1/stickers/${first}/withdraw`, {});
        const legacy = await f.call('GET', '/v1/authored');
        assert.equal(legacy.status, 200);
        assert.deepEqual(Object.keys(legacy.data), ['items']);
        assert.deepEqual(new Set(legacy.data.items.map(item => item.status)), new Set(['published', 'withdrawn']));
        const page = await f.call('GET', '/v1/authored?status=published&limit=1');
        assert.equal(page.status, 200);
        assert.deepEqual(page.data.items.map(item => item.id), [second]);
        assert.equal(page.data.nextCursor, null);
        const withdrawn = await f.call('GET', '/v1/authored?status=withdrawn&limit=1');
        assert.deepEqual(withdrawn.data.items.map(item => item.id), [first]);
        assert.equal((await f.call('GET', '/v1/authored?status=published&limit=0')).status, 400);
        assert.equal((await f.call('GET', '/v1/authored?status=removed')).status, 400);
        assert.equal((await f.call('GET', '/v1/authored?status=published&cursor=bad!')).status, 400);
        const extra = await f.publish('alice', 'third');
        assert.ok(extra);
        const cursor = (await f.call('GET', '/v1/authored?status=published&limit=1')).data.nextCursor;
        assert.ok(cursor);
        assert.equal((await f.call('GET', `/v1/authored?status=published&limit=1&cursor=${cursor}`, undefined, 'bob')).status, 400);
        assert.equal((await f.call('GET', `/v1/authored?status=withdrawn&limit=1&cursor=${cursor}`)).status, 400);
    } finally { await f.close(); }
});

test('authored cursor advances through stickers with the same creation time', async () => {
    const f = await fixture();
    try {
        for (const id of ['a', 'b', 'c']) await f.adapter.set('stickers', id, {
            id, authorId: 'alice', status: 'published', createdAt: 1000, presetId: 'taggi-1', revision: 1
        });
        const first = await f.call('GET', '/v1/authored?status=published&limit=1');
        const second = await f.call('GET', `/v1/authored?status=published&limit=1&cursor=${encodeURIComponent(first.data.nextCursor)}`);
        const third = await f.call('GET', `/v1/authored?status=published&limit=1&cursor=${encodeURIComponent(second.data.nextCursor)}`);
        assert.deepEqual([first, second, third].map(page => page.data.items[0].id), ['c', 'b', 'a']);
        assert.equal(third.data.nextCursor, null);
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


test('browsing recovery and collection accept measured uncertainty without relaxing publishing', async () => {
    const f = await fixture();
    try {
        for (const accuracyMeters of [75, 500, 2000, 2000.149, 5000]) {
            const id = await f.publish('alice', `approx-${Math.round(accuracyMeters * 1000)}`);
            const approximate = { ...fix(), accuracyMeters };
            const nearby = await f.call('POST', '/v1/nearby', { location: approximate }, null);
            assert.equal(nearby.status, 200, `Browse with ${accuracyMeters} metre accuracy`);
            assert.ok(nearby.data.items.length >= 1);
            assert.equal(nearby.data.items[0].note, undefined);
            const recovery = await f.call('POST', `/v1/stickers/${id}/recover`, { location: approximate }, 'bob');
            assert.equal(recovery.status, 200);
            assert.equal(recovery.data.sticker.note, undefined);
            assert.ok(recovery.data.mapUrl);
            assert.equal((await f.call('POST', `/v1/stickers/${id}/collect`, { location: approximate, discoveryId: 'unused' }, 'bob')).status, 403);
            assert.equal((await f.call('POST', `/v1/stickers/${id}/collect`, { location: approximate, discoveryId: recovery.data.discoveryId }, 'bob')).status, 200);
            assert.equal((await f.call('POST', `/v1/stickers/${id}/collect`, { location: { ...fix(), accuracyMeters: 5001 }, discoveryId: 'unused' }, 'bob')).status, 400);
        }
        assert.equal((await f.call('POST', '/v1/nearby', { location: { ...fix(), accuracyMeters: 5001 } }, null)).status, 400);
        assert.equal((await f.call('POST', '/v1/nearby', { location: { ...fix(999_969), accuracyMeters: 500 } }, null)).status, 400);
        assert.equal((await f.call('POST', '/v1/publications/prepare', { ...draft('approximate'), location: { ...fix(), accuracyMeters: 500 } })).status, 400);
    } finally { await f.close(); }
});


test('recovery and collection use measured uncertainty while rejecting false precision', async () => {
    const f = await fixture();
    try {
        const id = await f.publish();
        const drifted = { ...fix(1_000_000, 35.684), accuracyMeters: 500 };
        const distance = distanceMeters(fix(), drifted);
        assert.ok(distance > 100);
        for (const [accuracyMeters, status] of [[distance - 100, 200], [distance - 100 - 0.01, 403]]) {
            assert.equal((await f.call('POST', `/v1/stickers/${id}/recover`, {
                location: { ...drifted, accuracyMeters }
            }, 'bob')).status, status);
        }
        const recovered = await f.call('POST', `/v1/stickers/${id}/recover`, { location: drifted }, 'bob');
        assert.equal(recovered.status, 200);
        const discoveryId = recovered.data.discoveryId;
        assert.equal((await f.call('POST', `/v1/stickers/${id}/collect`, {
            discoveryId, location: { ...drifted, accuracyMeters: 8 }
        }, 'bob')).status, 403);
        assert.equal((await f.call('POST', `/v1/stickers/${id}/collect`, { discoveryId, location: drifted }, 'bob')).status, 200);
    } finally { await f.close(); }
});

test('recovery rejects stale excessive and malformed fixes', async () => {
    const f = await fixture();
    try {
        const id = await f.publish();
        for (const location of [fix(999_969), fix(1_000_006), null,
            { ...fix(), accuracyMeters: 5001 }, { ...fix(), accuracyMeters: -1 },
            { ...fix(), accuracyMeters: null }, { ...fix(), accuracyMeters: '500' },
            { ...fix(), latitude: 91 }]) {
            assert.equal((await f.call('POST', `/v1/stickers/${id}/recover`, { location }, 'bob')).status, 400);
        }
        assert.equal((await f.call('POST', `/v1/stickers/${id}/recover`, {
            location: { ...fix(999_970), accuracyMeters: 5000 }
        }, 'bob')).status, 200);
    } finally { await f.close(); }
});
