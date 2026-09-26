import { pathToFileURL } from 'node:url';
import { deleteAccountData } from './account.js';

export async function cleanupAbandoned(adapter, now = Math.floor(Date.now() / 1000)) {
    const cutoff = now - 3600;
    let publications = 0;
    let designs = 0;
    let discoveries = 0;
    let accounts = 0;
    let orphanAssets = 0;
    for (;;) {
        const pending = await adapter.query('stickers', [['status', '==', 'pending'], ['createdAt', '<', cutoff]], 200);
        if (!pending.length) break;
        for (const sticker of pending) {
            const removed = await adapter.transaction(async tx => {
                const current = await tx.get('stickers', sticker.id);
                if (!current || current.status !== 'pending' || current.createdAt >= cutoff) return null;
                const design = current.designId ? await tx.get('designs', current.designId) : null;
                const references = design ? Math.max(0, (design.references || 0) - 1) : 0;
                await tx.delete('stickers', sticker.id);
                if (design) await tx.set('designs', design.id, { ...design, references });
                return { designId: design?.id, orphaned: design?.status === 'archived' && references === 0 };
            });
            if (removed) {
                await adapter.deleteMap(sticker.id);
                if (removed.orphaned) await adapter.deleteDesignAssets(removed.designId);
                publications++;
            }
        }
    }
    for (;;) {
        const pending = await adapter.query('designs', [['status', '==', 'pending'], ['createdAt', '<', cutoff]], 200);
        if (!pending.length) break;
        for (const design of pending) {
            const counters = await adapter.query('designCounts', [['userId', '==', design.ownerId]], 1);
            const removed = await adapter.transaction(async tx => {
                const current = await tx.get('designs', design.id);
                if (!current || current.status !== 'pending' || current.createdAt >= cutoff) return false;
                const counter = counters[0] && await tx.get('designCounts', counters[0].id);
                await tx.set('designs', design.id, { ...current, status: 'abandoned', revision: (current.revision || 0) + 1 });
                if (counter) await tx.set('designCounts', counter.id, { ...counter, active: Math.max(0, counter.active - 1) });
                return true;
            });
            if (removed) { await adapter.deleteDesignAssets(design.id); designs++; }
        }
    }
    for (;;) {
        const expired = await adapter.query('discoveries', [['expiresAt', '<', now]], 200);
        if (!expired.length) break;
        for (const session of expired) { await adapter.delete('discoveries', session.id); discoveries++; }
    }
    const tombstones = await adapter.query('accounts', [['deleted', '==', true], ['cleaned', '==', false]], 200);
    for (const account of tombstones) {
        if (account.cleaned) continue;
        if (account.authDeleted || !(await adapter.userExists(account.id))) {
            await deleteAccountData(adapter, account.id, now);
            accounts++;
        }
    }
    for (const id of await adapter.listDesignAssetIds()) {
        const design = await adapter.get('designs', id);
        if (!design || design.status === 'abandoned' || (design.status === 'archived' && !design.references)) {
            await adapter.deleteDesignAssets(id);
            orphanAssets++;
        }
    }
    return { publications, designs, discoveries, accounts, orphanAssets };
}

if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) {
    const { createFirebaseAdapter } = await import('./firebase-adapter.js');
    const result = await cleanupAbandoned(createFirebaseAdapter());
    console.log(JSON.stringify(result));
}
