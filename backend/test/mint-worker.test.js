import test from 'node:test';
import assert from 'node:assert/strict';
import { MemoryAdapter } from './memory.js';
import { runMintWorker } from '../src/mint-worker.js';

const recipient = '0x1111111111111111111111111111111111111111';
const signerA = '0xaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa';
const signerB = '0xbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb';
const signerKey = `sepolia:${signerA}`;
const contractAddress = '0x1234567890123456789012345678901234567890';
const tokenId = `0x${'a'.repeat(64)}`;
const now = 1_000_000;

function fixture() {
    const adapter = new MemoryAdapter();
    let clock = now;
    const chain = {
        broadcasts: [], confirmed: new Map(), finalized: 100n, nonce: 7, address: signerA,
        signerAddress() { return this.address; }, targetContractAddress() { return contractAddress; },
        async chainId() { return 11155111; }, async pendingNonce() { return this.nonce; },
        async prepareMint({ nonce }) { return { raw: `0xraw${nonce}`, hash: `0xhash${nonce}` }; },
        async broadcast(raw) { this.broadcasts.push(raw); },
        async receipt(hash) { return this.confirmed.get(hash) || null; },
        async finalizedBlockNumber() { return this.finalized; },
        async tokenExists(id) { return id === tokenId; }
    };
    const job = { id: 'one', userId: 'bob', authorId: 'alice', stickerId: 'sticker', state: 'queued', recipient,
        tokenId, preset: 2, chainId: 11155111, contractAddress, createdAt: now, nextAttemptAt: now, attempts: 0, transactionHash: '' };
    const seed = async () => {
        await adapter.set('collections', 'one', { id: 'one', userId: 'bob', stickerId: 'sticker' });
        await adapter.set('stickers', 'sticker', { id: 'sticker', authorId: 'alice', status: 'published' });
        await adapter.set('nftMints', 'one', job);
    };
    const run = (options = {}) => runMintWorker({ adapter, chain, now: () => clock, workerId: 'test', ...options });
    return { adapter, chain, seed, run, advance: seconds => { clock += seconds; } };
}

test('signed raw transaction is durable before first broadcast and uses one nonce', async () => {
    const f = fixture();
    await f.seed();
    f.chain.broadcast = async raw => {
        const signed = await f.adapter.get('nftMints', 'one');
        assert.equal(signed.rawTransaction, raw);
        assert.equal((await f.adapter.get('mintSigner', signerKey)).nextNonce, 8);
        f.chain.broadcasts.push(raw);
    };
    await f.run();
    assert.deepEqual(f.chain.broadcasts, ['0xraw7']);
    assert.equal((await f.adapter.get('nftMints', 'one')).transactionHash, '0xhash7');
    f.advance(31);
    await f.run();
    assert.deepEqual(f.chain.broadcasts, ['0xraw7', '0xraw7']);
    assert.equal((await f.adapter.get('mintSigner', signerKey)).nextNonce, 8);
});

test('finalized successful receipt and contract state confirm mint', async () => {
    const f = fixture();
    await f.seed();
    await f.run();
    f.chain.confirmed.set('0xhash7', { status: 'success', blockNumber: 99n });
    f.advance(31);
    await f.run();
    assert.equal((await f.adapter.get('nftMints', 'one')).state, 'confirmed');
});

test('unsigned removed content cancels, while signed content still reconciles', async () => {
    const f = fixture();
    await f.seed();
    await f.adapter.set('stickers', 'sticker', { id: 'sticker', authorId: 'alice', status: 'removed' });
    await f.run();
    assert.equal((await f.adapter.get('nftMints', 'one')).state, 'cancelled');
    assert.equal(f.chain.broadcasts.length, 0);
    await f.adapter.set('nftMints', 'one', { ...(await f.adapter.get('nftMints', 'one')), state: 'signed', rawTransaction: '0xraw7', transactionHash: '0xhash7', nonce: 7 });
    f.chain.confirmed.set('0xhash7', { status: 'success', blockNumber: 99n });
    await f.run();
    assert.equal((await f.adapter.get('nftMints', 'one')).state, 'confirmed');
});

test('temporary account deletion tombstone does not cancel mint before Auth confirms deletion', async () => {
    const f = fixture();
    await f.seed();
    await f.adapter.set('accounts', 'bob', { id: 'bob', deleted: true, authDeleted: false });
    await f.run();
    assert.equal((await f.adapter.get('nftMints', 'one')).state, 'queued');
    assert.equal(f.chain.broadcasts.length, 0);
});

test('deletion beginning during preparation prevents signed bytes from being accepted', async () => {
    const f = fixture();
    await f.seed();
    f.chain.prepareMint = async () => {
        await f.adapter.set('accounts', 'bob', { id: 'bob', deleted: true, authDeleted: false });
        return { raw: '0xraw7', hash: '0xhash7' };
    };
    await f.run();
    const job = await f.adapter.get('nftMints', 'one');
    assert.equal(job.state, 'queued');
    assert.equal(job.rawTransaction, undefined);
    assert.equal(f.chain.broadcasts.length, 0);
    assert.equal((await f.adapter.get('mintSigner', signerKey)).nextNonce, 7);
});

test('worker recovers a waiting job after bind saved wallet but crashed before queue assignment', async () => {
    const f = fixture();
    await f.seed();
    await f.adapter.set('nftMints', 'one', { ...(await f.adapter.get('nftMints', 'one')), state: 'waiting', recipient: '' });
    await f.adapter.set('wallets', 'bob', { id: 'bob', address: recipient });
    await f.run();
    const job = await f.adapter.get('nftMints', 'one');
    assert.equal(job.recipient, recipient);
    assert.equal(job.transactionHash, '0xhash7');
});

test('waiting job without a wallet stays pending and backs off so other jobs can run', async () => {
    const f = fixture();
    await f.seed();
    await f.adapter.set('nftMints', 'one', { ...(await f.adapter.get('nftMints', 'one')), state: 'waiting', recipient: '' });
    await f.run();
    const job = await f.adapter.get('nftMints', 'one');
    assert.equal(job.state, 'waiting');
    assert.equal(job.nextAttemptAt > now, true);
    assert.equal(f.chain.broadcasts.length, 0);
});

test('waiting job cancels when author is deleted', async () => {
    const f = fixture();
    await f.seed();
    await f.adapter.set('nftMints', 'one', { ...(await f.adapter.get('nftMints', 'one')), state: 'waiting', recipient: '' });
    await f.adapter.set('accounts', 'alice', { id: 'alice', deleted: true, authDeleted: true });
    await f.run();
    assert.equal((await f.adapter.get('nftMints', 'one')).state, 'cancelled');
});

test('worker delays job for a different contract without signing', async () => {
    const f = fixture();
    await f.seed();
    await f.adapter.set('nftMints', 'one', { ...(await f.adapter.get('nftMints', 'one')),
        contractAddress: '0x9999999999999999999999999999999999999999' });
    await f.run();
    assert.equal((await f.adapter.get('nftMints', 'one')).state, 'delayed');
    assert.equal(f.chain.broadcasts.length, 0);
});

test('unfunded or failing signing delays without losing job or recipient', async () => {
    const f = fixture();
    await f.seed();
    f.chain.prepareMint = async () => { throw Object.assign(new Error('low balance'), { code: 'insufficient_funds' }); };
    await f.run();
    const job = await f.adapter.get('nftMints', 'one');
    assert.equal(job.state, 'delayed');
    assert.equal(job.recipient, recipient);
    assert.equal(job.rawTransaction, undefined);
    assert.equal((await f.adapter.get('mintSigner', signerKey)).nextNonce, 7);
});

test('different worker cannot take a live signer lease', async () => {
    const f = fixture();
    await f.seed();
    await f.adapter.set('mintSigner', signerKey, { id: signerKey, owner: 'other', leaseUntil: now + 100, nextNonce: 8 });
    await f.run();
    assert.equal(f.chain.broadcasts.length, 0);
    assert.equal((await f.adapter.get('nftMints', 'one')).state, 'queued');
});

test('expired worker cannot overwrite a replacement worker after receipt lookup', async () => {
    const f = fixture();
    await f.seed();
    await f.adapter.set('nftMints', 'one', { ...(await f.adapter.get('nftMints', 'one')),
        state: 'signed', rawTransaction: '0xraw7', transactionHash: '0xhash7', nonce: 7 });
    f.chain.receipt = async () => {
        await f.adapter.set('mintSigner', signerKey, { id: signerKey, owner: 'replacement', leaseUntil: now + 300, nextNonce: 8 });
        await f.adapter.set('nftMints', 'one', { ...(await f.adapter.get('nftMints', 'one')), state: 'delayed' });
        return { status: 'success', blockNumber: 99n };
    };
    await f.run();
    assert.equal((await f.adapter.get('nftMints', 'one')).state, 'delayed');
    assert.equal((await f.adapter.get('mintSigner', signerKey)).owner, 'replacement');
});

test('worker releases its signer lease after processing', async () => {
    const f = fixture();
    await f.seed();
    await f.run();
    assert.equal((await f.adapter.get('mintSigner', signerKey)).leaseUntil <= now, true);
});

test('rotated signer starts from its own pending nonce instead of old signer state', async () => {
    const f = fixture();
    await f.seed();
    await f.run();
    f.chain.address = signerB;
    f.chain.nonce = 1;
    await f.adapter.set('collections', 'two', { id: 'two', userId: 'bob', stickerId: 'sticker' });
    await f.adapter.set('nftMints', 'two', { ...(await f.adapter.get('nftMints', 'one')),
        id: 'two', state: 'queued', rawTransaction: '', transactionHash: '', nextAttemptAt: now });
    await f.run();
    assert.equal((await f.adapter.get('nftMints', 'two')).transactionHash, '0xhash1');
    assert.equal((await f.adapter.get('mintSigner', signerKey)).nextNonce, 8);
    assert.equal((await f.adapter.get('mintSigner', `sepolia:${signerB}`)).nextNonce, 2);
});

test('replacement signer ignores persisted nonce nineteen from previous address', async () => {
    const f = fixture();
    await f.seed();
    await f.adapter.set('mintSigner', signerKey, { id: signerKey, owner: 'old', leaseUntil: now - 1, nextNonce: 19 });
    f.chain.address = signerB;
    f.chain.nonce = 0;
    await f.run();
    assert.equal((await f.adapter.get('nftMints', 'one')).transactionHash, '0xhash0');
    assert.equal((await f.adapter.get('mintSigner', signerKey)).nextNonce, 19);
    assert.equal((await f.adapter.get('mintSigner', `sepolia:${signerB}`)).nextNonce, 1);
});

test('stale receipt from a noncanonical block keeps signed raw transaction', async () => {
    const f = fixture();
    await f.seed();
    await f.run();
    f.advance(31);
    f.chain.confirmed.set('0xhash7', { status: 'success', blockNumber: 99n, blockHash: '0xstale' });
    f.chain.blockHash = async () => '0xcanonical';
    await f.run();
    assert.notEqual((await f.adapter.get('nftMints', 'one')).state, 'confirmed');
    assert.equal((await f.adapter.get('nftMints', 'one')).rawTransaction, '0xraw7');
});
