import test from 'node:test';
import assert from 'node:assert/strict';
import { createServer } from 'node:http';
import { randomUUID } from 'node:crypto';
import { createFirebaseAdapter } from '../src/firebase-adapter.js';
import { createApi } from '../src/api.js';
import sharp from 'sharp';

const configured = !!(process.env.FIRESTORE_EMULATOR_HOST && process.env.FIREBASE_AUTH_EMULATOR_HOST && process.env.FIREBASE_STORAGE_EMULATOR_HOST);

test('Firebase emulators exercise Auth verification, Firestore transactions and private API', { skip: !configured }, async () => {
    const projectId = process.env.GOOGLE_CLOUD_PROJECT || 'demo-tagtag';
    const bucketName = process.env.TAGTAG_MAP_BUCKET || `${projectId}.appspot.com`;
    const apiKey = process.env.FIREBASE_API_KEY || 'demo-key';
    process.env.FIREBASE_API_KEY = apiKey;
    const signUp = async () => {
        const response = await fetch(`http://${process.env.FIREBASE_AUTH_EMULATOR_HOST}/identitytoolkit.googleapis.com/v1/accounts:signUp?key=${apiKey}`, {
            method: 'POST', headers: { 'content-type': 'application/json' }, body: JSON.stringify({ returnSecureToken: true })
        });
        assert.equal(response.status, 200);
        return response.json();
    };
    const alice = await signUp();
    const bob = await signUp();
    const firebase = createFirebaseAdapter({ projectId, bucketName });
    const adapter = { ...firebase,
        async signUpload(id) { return { uploadUrl: `https://upload.example/${id}`, uploadHeaders: { 'content-type': 'application/octet-stream', 'x-goog-content-length-range': '1,16777216' } }; },
        async mapMetadata() { return { size: 12, contentType: 'application/octet-stream', generation: '1' }; },
        async finalizeMap() {}, async signDownload(id) { return `https://download.example/${id}`; }, async deleteMap() {},
        async signDesignUpload(id) { return { uploadUrl: `https://upload.example/designs/${id}`, uploadHeaders: { 'content-type': 'image/png', 'x-goog-content-length-range': '1,5242880' } }; },
        async signDesignRead(id, type) { return `https://download.example/designs/${id}/${type}`; }
    };
    const now = Math.floor(Date.now() / 1000);
    const server = createServer(createApi({ adapter, now: () => now }));
    await new Promise(resolve => server.listen(0, '127.0.0.1', resolve));
    const base = `http://127.0.0.1:${server.address().port}`;
    async function call(method, path, body, token) {
        const response = await fetch(base + path, { method, headers: { ...(token ? { authorization: `Bearer ${token}` } : {}), 'content-type': 'application/json' }, body: body && JSON.stringify(body) });
        return { status: response.status, data: await response.json() };
    }
    const location = { latitude: 35.68, longitude: 139.76, accuracyMeters: 8, measuredUnixSeconds: now };
    try {
        assert.equal((await firebase.verifyToken(alice.idToken)).uid, alice.localId);
        const operationId = randomUUID();
        const prepared = await call('POST', '/v1/publications/prepare', { operationId, presetId: 'taggi-1', place: 'Tokyo', teaser: 'Hello', note: 'Private note', location,
            position: { x: 0, y: 0, z: 0 }, rotation: { x: 0, y: 0, z: 0, w: 1 }, widthMeters: 0.2, mapBytes: 12 }, alice.idToken);
        assert.equal(prepared.status, 200);
        assert.equal((await call('POST', `/v1/publications/${prepared.data.id}/finalize`, { operationId, location }, alice.idToken)).status, 200);
        const publicView = await call('POST', '/v1/nearby', { location });
        assert.equal(publicView.status, 200);
        assert.equal(publicView.data.items.some(item => item.id === prepared.data.id), true);
        assert.equal(JSON.stringify(publicView.data).includes('Private note'), false);
        const recovery = await call('POST', `/v1/stickers/${prepared.data.id}/recover`, { location }, bob.idToken);
        assert.equal(recovery.status, 200);
        const collected = await call('POST', `/v1/stickers/${prepared.data.id}/collect`, { location, discoveryId: recovery.data.discoveryId }, bob.idToken);
        assert.equal(collected.data.sticker.note, 'Private note');
        const image = await sharp({ create: { width: 24, height: 12, channels: 4, background: '#d0408099' } }).png().toBuffer();
        const designPrepare = await call('POST', '/v1/designs/prepare', { operationId: randomUUID(), name: 'Emulator art', kind: 'image',
            imageBytes: image.length, width: 24, height: 12 }, alice.idToken);
        assert.equal(designPrepare.status, 200);
        await firebase.bucket.file(`pending-designs/${designPrepare.data.id}`).save(image, { resumable: false, contentType: 'image/png' });
        const designFinalize = await call('POST', `/v1/designs/${designPrepare.data.id}/finalize`, undefined, alice.idToken);
        assert.equal(designFinalize.status, 200);
        const [thumbnail] = await firebase.bucket.file(`designs/${designPrepare.data.id}/thumbnail.png`).download();
        assert.equal((await sharp(thumbnail).metadata()).width, 24);
        assert.equal((await call('GET', '/v1/designs', undefined, bob.idToken)).data.items.length, 0);
        assert.equal((await call('GET', '/v1/designs', undefined, alice.idToken)).data.items[0].id, designPrepare.data.id);
        assert.equal((await call('DELETE', '/v1/account', undefined, alice.idToken)).status, 200);
        assert.equal((await call('GET', '/v1/collection', undefined, bob.idToken)).data.items[0].unavailable, true);
        assert.equal((await firebase.userExists(alice.localId)), false);
        assert.equal(await firebase.get('designs', designPrepare.data.id), null);
        assert.equal((await firebase.bucket.file(`designs/${designPrepare.data.id}/thumbnail.png`).exists())[0], false);
    } finally { await new Promise(resolve => server.close(resolve)); }
});
