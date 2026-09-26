export async function deleteAccountData(adapter, uid, now = Math.floor(Date.now() / 1000)) {
    const wallet = await adapter.get('wallets', uid);
    if (wallet) {
        await adapter.set('walletAddresses', wallet.address.toLowerCase(), { id: wallet.address.toLowerCase(), spent: true });
        await adapter.delete('wallets', uid);
    }
    for (;;) {
        const challenges = await adapter.query('walletChallenges', [['uid', '==', uid]], 200);
        if (!challenges.length) break;
        for (const challenge of challenges) await adapter.delete('walletChallenges', challenge.id);
    }
    for (const field of ['userId', 'authorId']) for (const state of ['waiting', 'queued', 'delayed']) {
        for (;;) {
            const jobs = await adapter.query('nftMints', [[field, '==', uid], ['state', '==', state]], 200);
            let cancelled = 0;
            for (const job of jobs) {
                if (job.rawTransaction) continue;
                await adapter.transaction(async tx => {
                    const current = await tx.get('nftMints', job.id);
                    if (current?.state === state && !current.rawTransaction) {
                        await tx.set('nftMints', job.id, { ...current, state: 'cancelled', cancelledAt: now });
                        cancelled++;
                    }
                });
            }
            if (jobs.length < 200 || !cancelled) break;
        }
    }
    for (;;) {
        const authored = await adapter.query('stickers', [['authorId', '==', uid]], 200);
        if (!authored.length) break;
        for (const sticker of authored) {
            await adapter.set('stickers', sticker.id, { ...sticker, status: 'deleted', note: '', revision: (sticker.revision || 0) + 1 });
            await adapter.deleteMap(sticker.id);
            await adapter.delete('stickers', sticker.id);
        }
    }
    for (;;) {
        const designs = await adapter.query('designs', [['ownerId', '==', uid]], 200);
        if (!designs.length) break;
        for (const design of designs) {
            await adapter.deleteDesignAssets(design.id);
            await adapter.delete('designs', design.id);
        }
    }
    for (const [name, field] of [['collections', 'userId'], ['blocks', 'userId'], ['reports', 'reporterId'], ['discoveries', 'userId'],
        ['designQuotas', 'userId'], ['designCounts', 'userId']]) {
        for (;;) {
            const entries = await adapter.query(name, [[field, '==', uid]], 200);
            if (!entries.length) break;
            for (const item of entries) await adapter.delete(name, item.id);
        }
    }
    await adapter.set('accounts', uid, { id: uid, deleted: true, authDeleted: true, cleaned: true, deletedAt: now });
}
