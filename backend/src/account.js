export async function deleteAccountData(adapter, uid, now = Math.floor(Date.now() / 1000)) {
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
