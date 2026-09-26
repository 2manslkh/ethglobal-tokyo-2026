import test from 'node:test';
import assert from 'node:assert/strict';
import { createServer } from 'node:http';
import { privateKeyToAccount } from 'viem/accounts';
import { createApi } from '../src/api.js';
import { MemoryAdapter } from './memory.js';
import { runMintWorker } from '../src/mint-worker.js';

const account = privateKeyToAccount(`0x${'1'.repeat(64)}`);
const otherAccount = privateKeyToAccount(`0x${'2'.repeat(64)}`);
const contractAddress = '0x1234567890123456789012345678901234567890';
const tokenId = '123456789';
const now = 1_000_000;
const location = { latitude: 35.68, longitude: 139.76, accuracyMeters: 8, measuredUnixSeconds: now };

async function fixture({ injectedTokenId = tokenId } = {}) {
    const adapter = new MemoryAdapter();
    let serial = 0;
    let clock = now;
    const server = createServer(createApi({ adapter, now: () => clock, ids: () => `challenge-${++serial}`,
        nft: { enabled: true, chainId: 11155111, contractAddress, domain: 'tagtag.example',
            ...(injectedTokenId === null ? {} : { tokenId: () => injectedTokenId }) } }));
    await new Promise(resolve => server.listen(0, '127.0.0.1', resolve));
    const base = `http://127.0.0.1:${server.address().port}`;
    const call = async (method, path, body, user = 'bob') => {
        const response = await fetch(base + path, { method, headers: { authorization: `Bearer ${user}`, 'content-type': 'application/json' }, body: body === undefined ? undefined : JSON.stringify(body) });
        return { status: response.status, data: await response.json() };
    };
    const publish = async () => {
        const prepared = await call('POST', '/v1/publications/prepare', { operationId: 'one', presetId: 'taggi-2', place: 'Tokyo', teaser: 'Hello', note: 'Private note', location,
            position: { x: 0, y: 0, z: 0 }, rotation: { x: 0, y: 0, z: 0, w: 1 }, widthMeters: 0.2, mapBytes: 12 }, 'alice');
        adapter.upload(prepared.data.id, 12);
        assert.equal((await call('POST', `/v1/publications/${prepared.data.id}/finalize`, { operationId: 'one', location }, 'alice')).status, 200);
        return prepared.data.id;
    };
    const collect = async id => {
        const recovery = await call('POST', `/v1/stickers/${id}/recover`, { location });
        return call('POST', `/v1/stickers/${id}/collect`, { location, discoveryId: recovery.data.discoveryId });
    };
    const bind = async (signer = account, user = 'bob') => {
        const challenge = await call('POST', '/v1/wallet/challenge', { address: signer.address }, user);
        assert.equal(challenge.status, 200);
        const signature = await signer.signMessage({ message: challenge.data.message });
        return call('POST', '/v1/wallet/bind', { challengeId: challenge.data.challengeId, signature }, user);
    };
    return { adapter, call, publish, collect, bind, advance: seconds => { clock += seconds; },
        close: () => new Promise(resolve => server.close(resolve)) };
}

test('wallet challenge proves EIP-191 ownership once and reserves one address per UID', async () => {
    const f = await fixture();
    try {
        assert.deepEqual((await f.call('GET', '/v1/wallet')).data, { enabled: true, address: '', chainId: 11155111 });
        const challenge = await f.call('POST', '/v1/wallet/challenge', { address: account.address });
        assert.match(challenge.data.message, /tagtag.example/);
        assert.match(challenge.data.message, /11155111/);
        assert.equal((await f.call('POST', '/v1/wallet/bind', { challengeId: challenge.data.challengeId,
            signature: await otherAccount.signMessage({ message: challenge.data.message }) })).status, 403);
        const signature = await account.signMessage({ message: challenge.data.message });
        assert.equal((await f.call('POST', '/v1/wallet/bind', { challengeId: challenge.data.challengeId, signature })).data.address, account.address);
        assert.equal((await f.call('POST', '/v1/wallet/bind', { challengeId: challenge.data.challengeId, signature })).status, 403);
        assert.equal((await f.bind(account, 'alice')).status, 409);
        assert.equal((await f.bind(otherAccount)).status, 409);
    } finally { await f.close(); }
});

test('wallet challenge expires after five minutes', async () => {
    const f = await fixture();
    try {
        const challenge = await f.call('POST', '/v1/wallet/challenge', { address: account.address });
        f.advance(300);
        const signature = await account.signMessage({ message: challenge.data.message });
        assert.equal((await f.call('POST', '/v1/wallet/bind', { challengeId: challenge.data.challengeId, signature })).status, 403);
        assert.equal((await f.call('GET', '/v1/wallet')).data.address, '');
    } finally { await f.close(); }
});

test('first collection atomically queues one private-free mint and exposes pending NFT status', async () => {
    const f = await fixture();
    try {
        assert.equal((await f.bind()).status, 200);
        const id = await f.publish();
        const first = await f.collect(id);
        const second = await f.collect(id);
        assert.equal(first.status, 200);
        assert.deepEqual(first.data.sticker.nft, { status: 'pending', chainId: 11155111, contractAddress,
            tokenId, transactionHash: '' });
        assert.deepEqual(second.data.sticker.nft, first.data.sticker.nft);
        const jobs = await f.adapter.query('nftMints');
        assert.equal(jobs.length, 1);
        assert.equal(jobs[0].recipient, account.address);
        assert.equal(jobs[0].preset, 1);
        assert.equal(JSON.stringify(jobs).includes('Private note'), false);
        assert.equal((await f.call('GET', '/v1/collection')).data.items[0].nft.status, 'pending');
    } finally { await f.close(); }
});

test('default token ID is a decimal uint256 string', async () => {
    const f = await fixture({ injectedTokenId: null });
    try {
        const id = await f.publish();
        const result = await f.collect(id);
        assert.equal(result.status, 200);
        const value = result.data.sticker.nft.tokenId;
        assert.match(value, /^(0|[1-9][0-9]*)$/);
        assert.equal(BigInt(value) < 2n ** 256n, true);
    } finally { await f.close(); }
});

test('custom sticker collection retains private artwork and queues only a generic souvenir', async () => {
    const f = await fixture();
    try {
        const id = await f.publish();
        const sticker = await f.adapter.get('stickers', id);
        delete sticker.presetId;
        sticker.designId = 'd'.repeat(64);
        await f.adapter.set('stickers', id, sticker);
        const first = await f.collect(id);
        assert.equal(first.status, 200);
        assert.equal(first.data.sticker.designId, sticker.designId);
        assert.ok(first.data.sticker.artworkUrl);
        assert.equal(first.data.sticker.note, 'Private note');
        assert.equal(first.data.sticker.nft.status, 'pending');
        assert.equal((await f.collect(id)).status, 200);
        const [job, extra] = await f.adapter.query('nftMints');
        assert.equal(extra, undefined);
        assert.equal(job.preset, 0);
        assert.equal(job.designId, undefined);
        assert.equal(job.artworkUrl, undefined);
        assert.equal(job.note, undefined);
    } finally { await f.close(); }
});

test('collection before wallet binding waits, then binding assigns its immutable recipient', async () => {
    const f = await fixture();
    try {
        const id = await f.publish();
        const result = await f.collect(id);
        assert.equal(result.data.sticker.nft.status, 'pending');
        const [waiting] = await f.adapter.query('nftMints');
        assert.equal(waiting.state, 'waiting');
        assert.equal(waiting.recipient, '');
        assert.equal((await f.bind()).status, 200);
        const assigned = await f.adapter.get('nftMints', waiting.id);
        assert.equal(assigned.state, 'queued');
        assert.equal(assigned.recipient, account.address);
    } finally { await f.close(); }
});

test('durable wallet bind succeeds when post-bind queue update fails', async () => {
    const f = await fixture();
    try {
        const stickerId = await f.publish();
        assert.equal((await f.collect(stickerId)).data.sticker.nft.status, 'pending');
        const query = f.adapter.query.bind(f.adapter);
        let fail = true;
        f.adapter.query = (name, ...args) => {
            if (name === 'nftMints' && fail) { fail = false; return Promise.reject(new Error('query offline')); }
            return query(name, ...args);
        };
        assert.equal((await f.bind()).status, 200);
        assert.equal((await f.call('GET', '/v1/wallet')).data.address, account.address);
        const [waiting] = await f.adapter.query('nftMints');
        assert.equal(waiting.state, 'waiting');
        const chain = { signerAddress: () => '0xaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa',
            targetContractAddress: () => contractAddress, chainId: async () => 11155111,
            pendingNonce: async () => 0, prepareMint: async () => ({ raw: '0xserialized', hash: '0xhash' }),
            broadcast: async () => {} };
        await runMintWorker({ adapter: f.adapter, chain, now: () => now });
        const recovered = await f.adapter.get('nftMints', waiting.id);
        assert.equal(recovered.recipient, account.address);
        assert.equal(recovered.state, 'submitted');
    } finally { await f.close(); }
});
