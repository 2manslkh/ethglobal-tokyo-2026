import test from 'node:test';
import assert from 'node:assert/strict';
import { createServer } from 'node:http';
import sharp from 'sharp';
import { createApi } from '../src/api.js';
import { MemoryAdapter } from './memory.js';
import { cleanupAbandoned } from '../src/cleanup.js';

const png = (width = 24, height = 12, background = { r: 240, g: 40, b: 80, alpha: 0.5 }) =>
    sharp({ create: { width, height, channels: 4, background } }).png().toBuffer();
const place = { latitude: 35.68, longitude: 139.76, accuracyMeters: 8, measuredUnixSeconds: 1_000_000 };
const publication = designId => ({ operationId: 'publish-one', designId, place: 'Tokyo', teaser: 'Hello', note: 'Secret', location: place,
    position: { x: 0, y: 0, z: 0 }, rotation: { x: 0, y: 0, z: 0, w: 1 }, widthMeters: 0.2, mapBytes: 12 });

async function fixture() {
    const adapter = new MemoryAdapter();
    const server = createServer(createApi({ adapter, now: () => 1_000_000 }));
    await new Promise(resolve => server.listen(0, '127.0.0.1', resolve));
    const root = `http://127.0.0.1:${server.address().port}`;
    async function call(method, path, body, user = 'alice') {
        const response = await fetch(root + path, { method, headers: { ...(user ? { authorization: `Bearer ${user}` } : {}), 'content-type': 'application/json' },
            body: body === undefined ? undefined : JSON.stringify(body) });
        return { status: response.status, data: await response.json() };
    }
    async function prepare(buffer, overrides = {}) {
        return call('POST', '/v1/designs/prepare', { operationId: 'create-one', name: 'My art', kind: 'image', imageBytes: buffer.length,
            width: 24, height: 12, ...overrides });
    }
    async function create(buffer) {
        buffer ??= await png();
        const result = await prepare(buffer);
        assert.equal(result.status, 200);
        adapter.uploadDesign(result.data.id, buffer);
        assert.equal((await call('POST', `/v1/designs/${result.data.id}/finalize`)).status, 200);
        return result.data.id;
    }
    return { adapter, call, prepare, create, close: () => new Promise(resolve => server.close(resolve)) };
}

test('design prepare and finalize validate PNG and return server dimensions and signed URLs', async () => {
    const f = await fixture();
    try {
        const image = await png();
        const prepared = await f.prepare(image);
        assert.equal(prepared.status, 200);
        assert.equal(prepared.data.uploadHeaders['content-type'], 'image/png');
        f.adapter.uploadDesign(prepared.data.id, image);
        const saved = await f.call('POST', `/v1/designs/${prepared.data.id}/finalize`);
        assert.equal(saved.status, 200);
        assert.equal(saved.data.design.width, 24);
        assert.equal(saved.data.design.height, 12);
        assert.match(saved.data.design.artworkUrl, /^https:/);
        assert.match(saved.data.design.thumbnailUrl, /^https:/);
        const thumbnail = await sharp(f.adapter.thumbnail(prepared.data.id)).metadata();
        assert.equal(thumbnail.width, 24);
        assert.equal(thumbnail.height, 12);
    } finally { await f.close(); }
});

test('thumbnail preserves artwork aspect ratio and alpha at 256 pixels', async () => {
    const f = await fixture();
    try {
        const image = await png(1024, 512);
        const prepared = await f.prepare(image, { width: 1024, height: 512 });
        f.adapter.uploadDesign(prepared.data.id, image);
        assert.equal((await f.call('POST', `/v1/designs/${prepared.data.id}/finalize`)).status, 200);
        const thumbnail = f.adapter.thumbnail(prepared.data.id);
        const metadata = await sharp(thumbnail).metadata();
        assert.equal(metadata.width, 256);
        assert.equal(metadata.height, 128);
        assert.equal(metadata.hasAlpha, true);
    } finally { await f.close(); }
});

test('prepare retries are immutable and a completed finalize is idempotent', async () => {
    const f = await fixture();
    try {
        const image = await png();
        const first = await f.prepare(image);
        assert.equal(first.status, 200);
        assert.equal((await f.prepare(image)).data.id, first.data.id);
        assert.equal((await f.prepare(image, { name: 'Changed' })).status, 409);
        assert.equal((await f.call('POST', `/v1/designs/${first.data.id}/finalize`)).status, 409);
        f.adapter.uploadDesign(first.data.id, image);
        const saved = await f.call('POST', `/v1/designs/${first.data.id}/finalize`);
        assert.equal(saved.status, 200);
        assert.deepEqual((await f.call('POST', `/v1/designs/${first.data.id}/finalize`)).data.design, saved.data.design);
        assert.equal((await f.prepare(image)).data.uploadUrl, '');
    } finally { await f.close(); }
});

test('invalid image content, byte length, and dimensions cannot finalize', async () => {
    const f = await fixture();
    try {
        const image = await png();
        const prepared = await f.prepare(image);
        const id = prepared.data.id;
        f.adapter.uploadDesign(id, Buffer.from('not a png'));
        assert.equal((await f.call('POST', `/v1/designs/${id}/finalize`)).status, 409);
        f.adapter.uploadDesign(id, Buffer.alloc(image.length));
        assert.equal((await f.call('POST', `/v1/designs/${id}/finalize`)).status, 400);
        const changedDimensions = await png(12, 24);
        const second = await f.prepare(changedDimensions, { operationId: 'second', width: 24, height: 12 });
        f.adapter.uploadDesign(second.data.id, changedDimensions);
        assert.equal((await f.call('POST', `/v1/designs/${second.data.id}/finalize`)).status, 400);
        const oversized = await png(1025, 1);
        assert.equal((await f.prepare(oversized, { operationId: 'third', width: 1025, height: 1 })).status, 400);
        const spoofed = await f.prepare(oversized, { operationId: 'spoofed', width: 1024, height: 1 });
        f.adapter.uploadDesign(spoofed.data.id, oversized);
        assert.equal((await f.call('POST', `/v1/designs/${spoofed.data.id}/finalize`)).status, 400);
        assert.equal((await f.prepare(image, { operationId: 'fourth', imageBytes: 5 * 1024 * 1024 + 1 })).status, 400);
    } finally { await f.close(); }
});

test('only owner can list, finalize, delete, or publish a design', async () => {
    const f = await fixture();
    try {
        const image = await png();
        const prepared = await f.prepare(image);
        f.adapter.uploadDesign(prepared.data.id, image);
        assert.equal((await f.call('POST', `/v1/designs/${prepared.data.id}/finalize`, undefined, 'bob')).status, 403);
        assert.equal((await f.call('DELETE', `/v1/designs/${prepared.data.id}`, undefined, 'bob')).status, 403);
        await f.call('POST', `/v1/designs/${prepared.data.id}/finalize`);
        assert.deepEqual((await f.call('GET', '/v1/designs', undefined, 'bob')).data.items, []);
        assert.equal((await f.call('POST', '/v1/publications/prepare', publication(prepared.data.id), 'bob')).status, 403);
        assert.equal((await f.call('POST', '/v1/publications/prepare', { ...publication(prepared.data.id), presetId: 'taggi-1' })).status, 400);
    } finally { await f.close(); }
});

test('publication exposes signed design artwork only while its visibility permits', async () => {
    const f = await fixture();
    try {
        const id = await f.create();
        const prepared = await f.call('POST', '/v1/publications/prepare', publication(id));
        assert.equal(prepared.status, 200);
        f.adapter.upload(prepared.data.id, 12);
        assert.equal((await f.call('POST', `/v1/publications/${prepared.data.id}/finalize`, { operationId: 'publish-one', location: place })).status, 200);
        const publicItem = (await f.call('POST', '/v1/nearby', { location: place }, null)).data.items[0];
        assert.equal(publicItem.designId, id);
        assert.equal(publicItem.artworkWidth, 24);
        assert.equal(publicItem.artworkHeight, 12);
        assert.match(publicItem.artworkUrl, /^https:/);
        const recovered = await f.call('POST', `/v1/stickers/${prepared.data.id}/recover`, { location: place }, 'bob');
        assert.match(recovered.data.sticker.thumbnailUrl, /^https:/);
        await f.call('POST', `/v1/stickers/${prepared.data.id}/collect`, { discoveryId: recovered.data.discoveryId, location: place }, 'bob');
        await f.call('DELETE', `/v1/designs/${id}`);
        assert.equal((await f.call('GET', '/v1/designs')).data.items.length, 0);
        assert.ok(f.adapter.thumbnail(id));
        assert.match((await f.call('GET', '/v1/collection', undefined, 'bob')).data.items[0].artworkUrl, /^https:/);
        await f.call('POST', `/v1/stickers/${prepared.data.id}/withdraw`, {});
        assert.equal((await f.call('POST', '/v1/nearby', { location: place }, null)).data.items.length, 0);
        assert.equal((await f.adapter.get('designs', id)).references, 1);
        assert.match((await f.call('GET', '/v1/collection', undefined, 'bob')).data.items[0].artworkUrl, /^https:/);
        await f.call('POST', `/v1/admin/stickers/${prepared.data.id}/moderate`, { status: 'removed' }, 'admin');
        assert.equal((await f.call('GET', '/v1/collection', undefined, 'bob')).data.items[0].artworkUrl, undefined);
        assert.equal((await f.adapter.get('designs', id)).references, 1);
        assert.ok(f.adapter.thumbnail(id));
        await f.call('POST', `/v1/admin/stickers/${prepared.data.id}/moderate`, { status: 'published' }, 'admin');
        assert.match((await f.call('GET', '/v1/collection', undefined, 'bob')).data.items[0].artworkUrl, /^https:/);
    } finally { await f.close(); }
});

test('design quotas cap daily creations and active library entries', async () => {
    const f = await fixture();
    try {
        const image = await png();
        for (let i = 0; i < 20; i++) assert.equal((await f.prepare(image, { operationId: `make-${i}` })).status, 200);
        assert.equal((await f.prepare(image, { operationId: 'make-20' })).status, 429);
        const first = await f.prepare(image, { operationId: 'make-0' });
        assert.equal(first.status, 200);
        assert.equal((await f.call('DELETE', `/v1/designs/${first.data.id}`)).status, 200);
        assert.equal((await f.prepare(image, { operationId: 'make-20' })).status, 429);
    } finally { await f.close(); }
});

test('active design cap applies independently from daily quota', async () => {
    const f = await fixture();
    try {
        const image = await png();
        const first = await f.prepare(image, { operationId: 'first' });
        assert.equal(first.status, 200);
        const counter = [...f.adapter.bucket('designCounts').values()][0];
        await f.adapter.set('designCounts', counter.id, { ...counter, active: 100 });
        assert.equal((await f.prepare(image, { operationId: 'next' })).status, 429);
        assert.equal((await f.prepare(image, { operationId: 'first' })).status, 200);
    } finally { await f.close(); }
});

test('library listing still finds ready designs after many archived entries', async () => {
    const f = await fixture();
    try {
        for (let i = 0; i < 201; i++) await f.adapter.set('designs', `archive-${i}`, { id: `archive-${i}`, ownerId: 'alice', status: 'archived' });
        const id = await f.create();
        assert.deepEqual((await f.call('GET', '/v1/designs')).data.items.map(item => item.id), [id]);
    } finally { await f.close(); }
});

test('abandoned upload keeps its operation ID reserved after cleanup', async () => {
    const f = await fixture();
    try {
        const image = await png();
        const prepared = await f.prepare(image);
        assert.equal(prepared.status, 200);
        f.adapter.uploadDesign(prepared.data.id, image);
        await cleanupAbandoned(f.adapter, 1_004_000);
        const expired = await f.prepare(image);
        assert.equal(expired.status, 409);
        assert.equal(expired.data.error.code, 'design_expired');
        const collision = await f.prepare(image, { name: 'Changed' });
        assert.equal(collision.status, 409);
        assert.equal(collision.data.error.code, 'conflict');
        assert.equal((await f.call('POST', `/v1/designs/${prepared.data.id}/finalize`)).status, 409);
        assert.equal(await f.adapter.designMetadata(prepared.data.id), null);
    } finally { await f.close(); }
});

test('archived design prepare retry identifies expiration without changing its operation', async () => {
    const f = await fixture();
    try {
        const image = await png();
        const id = await f.create(image);
        assert.equal((await f.call('DELETE', `/v1/designs/${id}`)).status, 200);
        const expired = await f.prepare(image);
        assert.equal(expired.status, 409);
        assert.equal(expired.data.error.code, 'design_expired');
    } finally { await f.close(); }
});

test('concurrent finalize retries keep a single immutable artwork asset', async () => {
    const f = await fixture();
    try {
        const image = await png();
        const prepared = await f.prepare(image);
        f.adapter.uploadDesign(prepared.data.id, image);
        const responses = await Promise.all(Array.from({ length: 4 }, () => f.call('POST', `/v1/designs/${prepared.data.id}/finalize`)));
        assert.deepEqual(responses.map(item => item.status), [200, 200, 200, 200]);
        const original = Buffer.from(f.adapter.designAssets.get(prepared.data.id).artwork);
        f.adapter.uploadDesign(prepared.data.id, await png(12, 24));
        assert.equal((await f.call('POST', `/v1/designs/${prepared.data.id}/finalize`)).status, 200);
        assert.deepEqual(f.adapter.designAssets.get(prepared.data.id).artwork, original);
    } finally { await f.close(); }
});

test('changed upload generation during concurrent finalize cannot replace the first saved artwork', async () => {
    const f = await fixture();
    try {
        const firstImage = await png();
        const secondImage = await png(24, 12, { r: 40, g: 80, b: 240, alpha: 0.5 });
        assert.equal(firstImage.length, secondImage.length);
        const prepared = await f.prepare(firstImage);
        f.adapter.uploadDesign(prepared.data.id, firstImage);
        let reads = 0;
        f.adapter.readDesignUpload = async () => ++reads === 1 ? firstImage : secondImage;
        const save = f.adapter.saveDesignAssets.bind(f.adapter);
        let arrivals = 0;
        let release;
        const bothArrived = new Promise(resolve => { release = resolve; });
        f.adapter.saveDesignAssets = async (...args) => {
            if (++arrivals === 2) release();
            await bothArrived;
            await save(...args);
        };
        const responses = await Promise.all([f.call('POST', `/v1/designs/${prepared.data.id}/finalize`), f.call('POST', `/v1/designs/${prepared.data.id}/finalize`)]);
        assert.deepEqual(responses.map(item => item.status).sort(), [200, 409]);
        const stored = f.adapter.designAssets.get(prepared.data.id).artwork;
        assert.ok(stored.equals(firstImage) || stored.equals(secondImage));
    } finally { await f.close(); }
});

test('finalize racing account deletion leaves assets for the cleanup sweep', async () => {
    const f = await fixture();
    try {
        const image = await png();
        const prepared = await f.prepare(image);
        f.adapter.uploadDesign(prepared.data.id, image);
        const save = f.adapter.saveDesignAssets.bind(f.adapter);
        f.adapter.saveDesignAssets = async (...args) => {
            assert.equal((await f.call('DELETE', '/v1/account')).status, 200);
            await save(...args);
        };
        assert.equal((await f.call('POST', `/v1/designs/${prepared.data.id}/finalize`)).status, 403);
        assert.ok(f.adapter.thumbnail(prepared.data.id));
        await cleanupAbandoned(f.adapter, 1_000_000);
        assert.equal(f.adapter.thumbnail(prepared.data.id), undefined);
    } finally { await f.close(); }
});

test('finalize racing design deletion cannot resurrect a removed library entry', async () => {
    const f = await fixture();
    try {
        const image = await png();
        const prepared = await f.prepare(image);
        f.adapter.uploadDesign(prepared.data.id, image);
        const save = f.adapter.saveDesignAssets.bind(f.adapter);
        f.adapter.saveDesignAssets = async (...args) => {
            assert.equal((await f.call('DELETE', `/v1/designs/${prepared.data.id}`)).status, 200);
            await save(...args);
        };
        assert.equal((await f.call('POST', `/v1/designs/${prepared.data.id}/finalize`)).status, 409);
        assert.equal((await f.adapter.get('designs', prepared.data.id)).status, 'archived');
        await cleanupAbandoned(f.adapter, 1_000_000);
        assert.equal(f.adapter.thumbnail(prepared.data.id), undefined);
    } finally { await f.close(); }
});

test('finalize racing abandoned-upload cleanup cannot publish an abandoned design', async () => {
    const f = await fixture();
    try {
        const image = await png();
        const prepared = await f.prepare(image);
        f.adapter.uploadDesign(prepared.data.id, image);
        const save = f.adapter.saveDesignAssets.bind(f.adapter);
        f.adapter.saveDesignAssets = async (...args) => {
            await cleanupAbandoned(f.adapter, 1_004_000);
            await save(...args);
        };
        assert.equal((await f.call('POST', `/v1/designs/${prepared.data.id}/finalize`)).status, 409);
        assert.equal((await f.adapter.get('designs', prepared.data.id)).status, 'abandoned');
        await cleanupAbandoned(f.adapter, 1_004_000);
        assert.equal(f.adapter.thumbnail(prepared.data.id), undefined);
    } finally { await f.close(); }
});
