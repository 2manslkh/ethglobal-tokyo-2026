import { pathToFileURL } from 'node:url';
import { deleteAccountData } from './account.js';

export async function cleanupAbandoned(adapter, now = Math.floor(Date.now() / 1000)) {
    const cutoff = now - 3600;
    let publications = 0;
    let discoveries = 0;
    let accounts = 0;
    for (;;) {
        const pending = await adapter.query('stickers', [['status', '==', 'pending'], ['createdAt', '<', cutoff]], 200);
        if (!pending.length) break;
        for (const sticker of pending) {
            const removed = await adapter.transaction(async tx => {
                const current = await tx.get('stickers', sticker.id);
                if (!current || current.status !== 'pending' || current.createdAt >= cutoff) return false;
                await tx.delete('stickers', sticker.id);
                return true;
            });
            if (removed) { await adapter.deleteMap(sticker.id); publications++; }
        }
    }
    for (;;) {
        const expired = await adapter.query('discoveries', [['expiresAt', '<', now]], 200);
        if (!expired.length) break;
        for (const session of expired) { await adapter.delete('discoveries', session.id); discoveries++; }
    }
    for (;;) {
        const expired = await adapter.query('walletChallenges', [['expiresAt', '<', now]], 200);
        if (!expired.length) break;
        for (const challenge of expired) await adapter.delete('walletChallenges', challenge.id);
    }
    const tombstones = await adapter.query('accounts', [['deleted', '==', true], ['cleaned', '==', false]], 200);
    for (const account of tombstones) {
        if (account.cleaned) continue;
        if (account.authDeleted || !(await adapter.userExists(account.id))) {
            await deleteAccountData(adapter, account.id, now);
            accounts++;
        }
    }
    return { publications, discoveries, accounts };
}

if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) {
    const { createFirebaseAdapter } = await import('./firebase-adapter.js');
    const result = await cleanupAbandoned(createFirebaseAdapter());
    console.log(JSON.stringify(result));
}
