import test from 'node:test';
import assert from 'node:assert/strict';
import { cleanupAbandoned } from '../src/cleanup.js';
import { MemoryAdapter } from './memory.js';
import { deleteAccountData } from '../src/account.js';

test('cleanup removes old pending maps and expired sessions but retains published maps', async () => {
    const adapter = new MemoryAdapter();
    await adapter.set('stickers', 'old', { id: 'old', status: 'pending', createdAt: 1 });
    await adapter.set('stickers', 'live', { id: 'live', status: 'published', createdAt: 1 });
    await adapter.set('stickers', 'recent', { id: 'recent', status: 'pending', createdAt: 9990 });
    await adapter.set('discoveries', 'expired', { id: 'expired', expiresAt: 2 });
    adapter.upload('old', 12);
    adapter.upload('live', 12);
    adapter.upload('recent', 12);
    const result = await cleanupAbandoned(adapter, 10000);
    assert.deepEqual(result, { publications: 1, designs: 0, discoveries: 1, accounts: 0, orphanAssets: 0 });
    assert.equal(await adapter.get('stickers', 'old'), null);
    assert.equal(await adapter.mapMetadata('old'), null);
    assert.ok(await adapter.get('stickers', 'live'));
    assert.ok(await adapter.mapMetadata('live'));
    assert.ok(await adapter.get('stickers', 'recent'));
});

test('cleanup resumes data removal for an account whose Auth identity is already deleted', async () => {
    const adapter = new MemoryAdapter();
    await adapter.set('accounts', 'alice', { id: 'alice', deleted: true, authDeleted: true, cleaned: false });
    await adapter.set('stickers', 'sticker', { id: 'sticker', authorId: 'alice', status: 'published', note: 'private' });
    await adapter.set('collections', 'copy', { id: 'copy', userId: 'alice', stickerId: 'sticker' });
    adapter.upload('sticker', 12);
    const result = await cleanupAbandoned(adapter, 10000);
    assert.equal(result.accounts, 1);
    assert.equal(await adapter.get('stickers', 'sticker'), null);
    assert.equal(await adapter.get('collections', 'copy'), null);
    assert.equal(await adapter.mapMetadata('sticker'), null);
    assert.equal((await adapter.get('accounts', 'alice')).cleaned, true);
});

test('account cleanup removes wallet challenge and cancels unsigned mint, retaining signed reconciliation', async () => {
    const adapter = new MemoryAdapter();
    await adapter.set('wallets', 'alice', { id: 'alice', address: '0x1111111111111111111111111111111111111111' });
    await adapter.set('walletAddresses', '0x1111111111111111111111111111111111111111', { uid: 'alice' });
    await adapter.set('walletChallenges', 'challenge', { id: 'challenge', uid: 'alice' });
    await adapter.set('nftMints', 'unsigned', { id: 'unsigned', userId: 'alice', state: 'waiting', recipient: '' });
    await adapter.set('nftMints', 'signed', { id: 'signed', userId: 'alice', state: 'submitted', rawTransaction: '0xabc' });
    await deleteAccountData(adapter, 'alice', 10000);
    assert.equal(await adapter.get('wallets', 'alice'), null);
    assert.equal(await adapter.get('walletChallenges', 'challenge'), null);
    assert.equal((await adapter.get('walletAddresses', '0x1111111111111111111111111111111111111111')).uid, undefined);
    assert.equal((await adapter.get('nftMints', 'unsigned')).state, 'cancelled');
    assert.equal((await adapter.get('nftMints', 'signed')).rawTransaction, '0xabc');
});

test('cleanup removes abandoned design uploads and releases active slots', async () => {
    const adapter = new MemoryAdapter();
    await adapter.set('designs', 'old-design', { id: 'old-design', ownerId: 'alice', status: 'pending', createdAt: 1, references: 0 });
    await adapter.set('designCounts', 'counter', { id: 'counter', userId: 'alice', active: 1 });
    adapter.uploadDesign('old-design', Buffer.from('pending'));
    const result = await cleanupAbandoned(adapter, 10000);
    assert.equal(result.designs, 1);
    assert.equal((await adapter.get('designs', 'old-design')).status, 'abandoned');
    assert.equal(await adapter.designMetadata('old-design'), null);
    assert.equal((await adapter.get('designCounts', 'counter')).active, 0);
});

test('account cleanup removes owned designs and their immutable assets', async () => {
    const adapter = new MemoryAdapter();
    await adapter.set('accounts', 'alice', { id: 'alice', deleted: true, authDeleted: true, cleaned: false });
    await adapter.set('designs', 'owned', { id: 'owned', ownerId: 'alice', status: 'ready', references: 1 });
    await adapter.set('designQuotas', 'daily', { id: 'daily', userId: 'alice', count: 1 });
    await adapter.set('designCounts', 'counter', { id: 'counter', userId: 'alice', active: 1 });
    await adapter.saveDesignAssets('owned', Buffer.from('art'), Buffer.from('thumb'));
    const result = await cleanupAbandoned(adapter, 10000);
    assert.equal(result.accounts, 1);
    assert.equal(await adapter.get('designs', 'owned'), null);
    assert.equal(adapter.thumbnail('owned'), undefined);
    assert.equal(await adapter.get('designQuotas', 'daily'), null);
    assert.equal(await adapter.get('designCounts', 'counter'), null);
});

test('expired publication releases archived design artwork when it was the last reference', async () => {
    const adapter = new MemoryAdapter();
    await adapter.set('stickers', 'pending-sticker', { id: 'pending-sticker', authorId: 'alice', designId: 'design', status: 'pending', createdAt: 1 });
    await adapter.set('designs', 'design', { id: 'design', ownerId: 'alice', status: 'archived', references: 1 });
    await adapter.saveDesignAssets('design', Buffer.from('art'), Buffer.from('thumb'));
    await cleanupAbandoned(adapter, 10000);
    assert.equal((await adapter.get('designs', 'design')).references, 0);
    assert.equal(adapter.thumbnail('design'), undefined);
});

test('cleanup sweeps immutable artwork left by finalize after account deletion', async () => {
    const adapter = new MemoryAdapter();
    await adapter.saveDesignAssets('orphan', Buffer.from('art'), Buffer.from('thumb'));
    await adapter.set('designs', 'retained', { id: 'retained', status: 'archived', references: 1 });
    await adapter.saveDesignAssets('retained', Buffer.from('art'), Buffer.from('thumb'));
    const result = await cleanupAbandoned(adapter, 10000);
    assert.equal(result.orphanAssets, 1);
    assert.equal(adapter.thumbnail('orphan'), undefined);
    assert.ok(adapter.thumbnail('retained'));
});
