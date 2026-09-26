import test from 'node:test';
import assert from 'node:assert/strict';
import { createServer } from 'node:http';
import { randomBytes, randomUUID } from 'node:crypto';
import { createFirebaseAdapter } from '../src/firebase-adapter.js';
import { createApi } from '../src/api.js';
import { privateKeyToAccount } from 'viem/accounts';
import { runMintWorker } from '../src/mint-worker.js';

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
        async finalizeMap() {}, async signDownload(id) { return `https://download.example/${id}`; }, async deleteMap() {}
    };
    const now = Math.floor(Date.now() / 1000);
    const contractAddress = '0x1234567890123456789012345678901234567890';
    const tokenId = '123456789';
    const server = createServer(createApi({ adapter, now: () => now,
        nft: { enabled: true, chainId: 11155111, contractAddress, domain: 'tagtag.example', tokenId: () => tokenId } }));
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
        assert.equal(collected.data.sticker.nft.status, 'pending');
        const account = privateKeyToAccount(`0x${randomBytes(32).toString('hex')}`);
        const challenge = await call('POST', '/v1/wallet/challenge', { address: account.address }, bob.idToken);
        assert.equal(challenge.status, 200);
        const signature = await account.signMessage({ message: challenge.data.message });
        const bound = await call('POST', '/v1/wallet/bind', { challengeId: challenge.data.challengeId, signature }, bob.idToken);
        assert.equal(bound.data.address, account.address);
        const jobs = await adapter.query('nftMints', [['userId', '==', bob.localId], ['state', '==', 'queued']], 10);
        assert.equal(jobs.length, 1);
        assert.equal(jobs[0].recipient, account.address);
        const chain = { signerAddress() { return '0xaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa'; },
            targetContractAddress() { return contractAddress; },
            async chainId() { return 11155111; }, async pendingNonce() { return 3; },
            async prepareMint() { return { raw: '0xserialized', hash: '0xhash' }; }, async broadcast() {} };
        await runMintWorker({ adapter, chain, now: () => now, workerId: 'emulator' });
        assert.equal((await adapter.get('nftMints', jobs[0].id)).rawTransaction, '0xserialized');
        assert.equal((await call('DELETE', '/v1/account', undefined, alice.idToken)).status, 200);
        assert.equal((await call('GET', '/v1/collection', undefined, bob.idToken)).data.items[0].unavailable, true);
        assert.equal((await firebase.userExists(alice.localId)), false);
    } finally { await new Promise(resolve => server.close(resolve)); }
});
