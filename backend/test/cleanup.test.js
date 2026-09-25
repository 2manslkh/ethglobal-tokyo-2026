import test from 'node:test';
import assert from 'node:assert/strict';
import { cleanupAbandoned } from '../src/cleanup.js';
import { MemoryAdapter } from './memory.js';

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
    assert.deepEqual(result, { publications: 1, discoveries: 1, accounts: 0 });
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
