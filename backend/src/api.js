import { createHash, randomBytes, randomUUID } from 'node:crypto';
import { getAddress, isHex, recoverMessageAddress } from 'viem';
import { distanceMeters, geohash, neighboringCells } from './geo.js';
import { deleteAccountData as removeAccountData } from './account.js';
import { inspectDesignImage, MAX_DESIGN_BYTES, MAX_DESIGN_EDGE } from './design-image.js';

const MAX_MAP = 16 * 1024 * 1024;
const PRESETS = new Set(['taggi-1', 'taggi-2', 'taggi-3', 'taggi-4']);
const MAX_BODY = 32768;
const DISCOVERY_SECONDS = 300;
const NEARBY_METERS = 2000;
const BROWSE_MAX_ACCURACY_METERS = 5000;
const DESIGN_KINDS = new Set(['image', 'ai', 'polaroid']);

class ApiError extends Error {
    constructor(status, code, message) { super(message); this.status = status; this.code = code; }
}
const bad = (message) => { throw new ApiError(400, 'invalid_request', message); };
const denied = () => { throw new ApiError(403, 'forbidden', 'Access denied'); };
const missing = () => { throw new ApiError(404, 'not_found', 'Sticker unavailable'); };
const conflict = (message) => { throw new ApiError(409, 'conflict', message); };
const designExpired = () => { throw new ApiError(409, 'design_expired', 'Design has been removed'); };
const digest = value => createHash('sha256').update(value).digest('hex');
const blockedPhrases = ['buy crypto now', 'free money', 'kill yourself', 't.me/', 'http://', 'https://',
    ...String(process.env.TAGTAG_BLOCKED_PHRASES || '').split(',').map(value => value.trim()).filter(Boolean)];
const text = (value, max, field, required = true) => {
    if (typeof value !== 'string' || value.length > max || (required && !value.trim())) bad(`${field} is invalid`);
    return value.trim();
};
const content = (value, max, field) => {
    const clean = text(value, max, field);
    const normalized = clean.normalize('NFKC').toLowerCase().replace(/\s+/g, ' ');
    if (blockedPhrases.some(phrase => normalized.includes(phrase))) bad(`${field} contains disallowed content`);
    return clean;
};
const number = (value, min, max, field) => {
    if (typeof value !== 'number' || !Number.isFinite(value) || value < min || value > max) bad(`${field} is invalid`);
    return value;
};
const coordinates = value => {
    if (!value || typeof value !== 'object') bad('location is required');
    return { latitude: number(value.latitude, -90, 90, 'latitude'), longitude: number(value.longitude, -180, 180, 'longitude') };
};
const vector = (value, fields, field) => {
    if (!value || typeof value !== 'object') bad(`${field} is required`);
    return Object.fromEntries(fields.map(key => [key, number(value[key], -10000, 10000, `${field}.${key}`)]));
};
const summary = sticker => ({
    id: sticker.id, presetId: sticker.presetId, designId: sticker.designId,
    artworkWidth: sticker.artworkWidth, artworkHeight: sticker.artworkHeight, authorId: sticker.authorId,
    authorName: sticker.authorName, place: sticker.place, teaser: sticker.teaser,
    latitude: sticker.latitude, longitude: sticker.longitude,
    revision: sticker.revision, createdAt: sticker.createdAt
});

async function bodyJson(request) {
    let size = 0;
    const chunks = [];
    for await (const chunk of request) {
        size += chunk.length;
        if (size > MAX_BODY) throw new ApiError(413, 'too_large', 'Request body too large');
        chunks.push(chunk);
    }
    if (!size) return {};
    try {
        const value = JSON.parse(Buffer.concat(chunks).toString('utf8'));
        if (!value || Array.isArray(value) || typeof value !== 'object') bad('JSON object required');
        return value;
    } catch (error) {
        if (error instanceof ApiError) throw error;
        bad('Invalid JSON');
    }
}

export function createApi({ adapter, now = () => Math.floor(Date.now() / 1000), ids = randomUUID, deleteAccountData = removeAccountData,
    nft = { enabled: false } }) {
    if (!adapter) throw new Error('Adapter required');
    if (nft.enabled && (nft.chainId !== 11155111 || !nft.contractAddress || !nft.domain)) throw new Error('Invalid NFT configuration');
    const tokenId = nft.tokenId || (() => BigInt(`0x${randomBytes(32).toString('hex')}`).toString());
    const walletStatus = address => ({ enabled: !!nft.enabled, address: address || '', chainId: 11155111 });
    const nftStatus = job => job && ({ status: job.state === 'confirmed' ? 'confirmed' : job.state === 'cancelled' ? 'cancelled' : job.state === 'delayed' ? 'delayed' : 'pending',
        chainId: job.chainId, contractAddress: job.contractAddress, tokenId: job.tokenId, transactionHash: job.transactionHash || '' });
    const limits = new Map();
    const rateLimit = (scope, key, max, seconds) => {
        const timestamp = now();
        const id = `${scope}\0${key}`;
        const state = limits.get(id);
        if (!state || state.until <= timestamp) limits.set(id, { count: 1, until: timestamp + seconds });
        else if (++state.count > max) throw new ApiError(429, 'rate_limited', 'Too many requests');
        if (limits.size > 10000) for (const [entry, value] of limits) if (value.until <= timestamp) limits.delete(entry);
    };
    const location = (value, maxAccuracyMeters = 50) => {
        const point = coordinates(value);
        number(value.accuracyMeters, 0, maxAccuracyMeters, 'accuracyMeters');
        if (!Number.isInteger(value.measuredUnixSeconds) || value.measuredUnixSeconds < now() - 30 || value.measuredUnixSeconds > now() + 5) bad('Location fix is stale');
        return point;
    };
    const auth = async (request, optional = false) => {
        const header = request.headers.authorization;
        if (!header) {
            if (optional) return null;
            throw new ApiError(401, 'unauthenticated', 'Sign in required');
        }
        if (!/^Bearer [^\s]+$/.test(header)) throw new ApiError(401, 'unauthenticated', 'Invalid authorization');
        try {
            const user = await adapter.verifyToken(header.slice(7));
            if (!user?.uid || (await adapter.get('accounts', user.uid))?.deleted) denied();
            return user;
        } catch (error) {
            if (error instanceof ApiError) throw error;
            throw new ApiError(401, 'unauthenticated', 'Invalid authorization');
        }
    };
    const active = async (id) => {
        const sticker = await adapter.get('stickers', id);
        if (!sticker || sticker.status !== 'published') missing();
        if ((await adapter.get('accounts', sticker.authorId))?.deleted) missing();
        return sticker;
    };
    const isBlocked = async (uid, authorId) => !!(await adapter.get('blocks', digest(`${uid}\0${authorId}`)));
    const designResponse = async design => ({ id: design.id, ownerId: design.ownerId, name: design.name, kind: design.kind,
        width: design.width, height: design.height, revision: design.revision, createdAt: design.createdAt,
        artworkUrl: await adapter.signDesignRead(design.id, 'artwork'), thumbnailUrl: await adapter.signDesignRead(design.id, 'thumbnail') });
    const visibleSummary = async sticker => {
        const result = summary(sticker);
        if (sticker.designId) {
            result.artworkUrl = await adapter.signDesignRead(sticker.designId, 'artwork');
            result.thumbnailUrl = await adapter.signDesignRead(sticker.designId, 'thumbnail');
        }
        return result;
    };
    const collected = async (uid, id, collectedAt) => {
        const sticker = await adapter.get('stickers', id);
        const collectionId = digest(`${uid}\0${id}`);
        const job = nft.enabled ? await adapter.get('nftMints', collectionId) : null;
        const blocked = sticker && await isBlocked(uid, sticker.authorId);
        const authorDeleted = sticker && (await adapter.get('accounts', sticker.authorId))?.deleted;
        const unavailable = !sticker || sticker.status === 'removed' || sticker.status === 'deleted' || blocked || authorDeleted;
        return sticker ? { ...(unavailable ? summary(sticker) : await visibleSummary(sticker)), note: unavailable ? '' : sticker.note, collectedAt, unavailable: !!unavailable,
            ...(job ? { nft: nftStatus(job) } : {}) } : {
            id, presetId: '', authorId: '', authorName: '', place: '', teaser: '', latitude: 0, longitude: 0, revision: 0, createdAt: 0,
            note: '', collectedAt, unavailable: true, ...(job ? { nft: nftStatus(job) } : {})
        };
    };
    const send = (response, status, data) => {
        response.writeHead(status, { 'content-type': 'application/json; charset=utf-8', 'cache-control': 'no-store', 'x-content-type-options': 'nosniff' });
        response.end(JSON.stringify(data));
    };

    return async (request, response) => {
        try {
            const path = new URL(request.url, 'http://localhost').pathname;
            const method = request.method;
            if (method === 'GET' && path === '/health') return send(response, 200, { ok: true });
            if (method === 'GET' && path === '/admin') {
                const { adminPage } = await import('./admin-page.js');
                response.writeHead(200, { 'content-type': 'text/html; charset=utf-8', 'cache-control': 'no-store', 'content-security-policy': "default-src 'none'; script-src 'self' https://www.gstatic.com https://apis.google.com; style-src 'self'; connect-src 'self' https://*.googleapis.com https://*.firebaseapp.com; frame-src https://*.firebaseapp.com https://accounts.google.com; base-uri 'none'; form-action 'none'" });
                return response.end(adminPage);
            }
            if (method === 'GET' && path === '/admin.js') {
                const { adminScript } = await import('./admin-page.js');
                response.writeHead(200, { 'content-type': 'text/javascript; charset=utf-8', 'cache-control': 'no-store' });
                return response.end(adminScript);
            }
            if (method === 'GET' && path === '/admin.css') {
                const { adminCss } = await import('./admin-page.js');
                response.writeHead(200, { 'content-type': 'text/css; charset=utf-8', 'cache-control': 'no-store' });
                return response.end(adminCss);
            }
            if (method === 'GET' && path === '/admin-config') {
                const apiKey = process.env.FIREBASE_API_KEY;
                const authDomain = process.env.FIREBASE_AUTH_DOMAIN || (process.env.GOOGLE_CLOUD_PROJECT ? `${process.env.GOOGLE_CLOUD_PROJECT}.firebaseapp.com` : '');
                if (!apiKey || !authDomain) throw new ApiError(503, 'not_configured', 'Admin sign-in unavailable');
                return send(response, 200, { apiKey, authDomain });
            }
            const segments = path.split('/').filter(Boolean);
            if (segments[0] !== 'v1') throw new ApiError(404, 'not_found', 'Route not found');
            const forwarded = request.headers['x-forwarded-for'];
            const ip = typeof forwarded === 'string' ? forwarded.split(',').at(-1).trim() : request.socket.remoteAddress || 'unknown';
            rateLimit('ip', ip, 120, 60);

            if (method === 'POST' && path === '/v1/nearby') {
                rateLimit('nearby', ip, 30, 60);
                const user = await auth(request, true);
                // Browsing public clues is independent of the stricter presence checks below.
                const point = location((await bodyJson(request)).location, BROWSE_MAX_ACCURACY_METERS);
                const blocks = user ? await adapter.query('blocks', [['userId', '==', user.uid]], 1000) : [];
                const excluded = new Set(blocks.map(item => item.authorId));
                const candidates = await adapter.query('stickers', [['geoCell', 'in', neighboringCells(point.latitude, point.longitude)], ['status', '==', 'published']], 500);
                const near = candidates.filter(item => !excluded.has(item.authorId) && distanceMeters(point, item) <= NEARBY_METERS);
                const authors = [...new Set(near.map(item => item.authorId))];
                const accounts = await Promise.all(authors.map(id => adapter.get('accounts', id)));
                const deletedAuthors = new Set(authors.filter((_, index) => accounts[index]?.deleted));
                const visible = near.filter(item => !deletedAuthors.has(item.authorId))
                    .sort((a, b) => distanceMeters(point, a) - distanceMeters(point, b)).slice(0, 100);
                const items = await Promise.all(visible.map(visibleSummary));
                return send(response, 200, { items });
            }

            const user = await auth(request);
            if (method === 'GET' && path === '/v1/wallet') {
                const wallet = nft.enabled ? await adapter.get('wallets', user.uid) : null;
                return send(response, 200, walletStatus(wallet?.address));
            }
            if (method === 'POST' && path === '/v1/wallet/challenge') {
                if (!nft.enabled) throw new ApiError(503, 'not_configured', 'Wallet unavailable');
                rateLimit('walletChallenge', user.uid, 10, 300);
                const input = (await bodyJson(request)).address;
                let address;
                try { address = getAddress(input); } catch { bad('address is invalid'); }
                const challengeId = ids();
                const expiresAt = now() + 300;
                const message = `${nft.domain} wants to bind a tagtag souvenir wallet.\nPurpose: bind wallet\nAddress: ${address}\nChain ID: 11155111\nChallenge: ${challengeId}\nExpires At: ${expiresAt}`;
                await adapter.set('walletChallenges', challengeId, { id: challengeId, uid: user.uid, address, message, expiresAt, used: false });
                return send(response, 200, { challengeId, message, expiresAt });
            }
            if (method === 'POST' && path === '/v1/wallet/bind') {
                if (!nft.enabled) throw new ApiError(503, 'not_configured', 'Wallet unavailable');
                rateLimit('walletBind', user.uid, 10, 300);
                const input = await bodyJson(request);
                const challengeId = text(input.challengeId, 128, 'challengeId');
                if (!isHex(input.signature, { strict: true }) || input.signature.length !== 132) bad('signature is invalid');
                const challenge = await adapter.get('walletChallenges', challengeId);
                if (!challenge || challenge.uid !== user.uid || challenge.used || challenge.expiresAt <= now()) denied();
                let recovered;
                try { recovered = await recoverMessageAddress({ message: challenge.message, signature: input.signature }); }
                catch { denied(); }
                if (recovered.toLowerCase() !== challenge.address.toLowerCase()) denied();
                await adapter.transaction(async tx => {
                    const current = await tx.get('walletChallenges', challengeId);
                    if (!current || current.uid !== user.uid || current.used || current.expiresAt <= now()) conflict('Challenge already consumed');
                    if ((await tx.get('accounts', user.uid))?.deleted) denied();
                    if (await tx.get('wallets', user.uid)) conflict('Wallet already bound');
                    const key = current.address.toLowerCase();
                    if (await tx.get('walletAddresses', key)) conflict('Address already bound');
                    await tx.set('walletChallenges', challengeId, { ...current, used: true });
                    await tx.set('wallets', user.uid, { id: user.uid, address: current.address, boundAt: now() });
                    await tx.set('walletAddresses', key, { id: key, uid: user.uid });
                });
                try {
                    for (;;) {
                        const waiting = await adapter.query('nftMints', [['userId', '==', user.uid], ['state', '==', 'waiting']], 200);
                        for (const job of waiting) await adapter.transaction(async tx => {
                            const [current, wallet] = await Promise.all([tx.get('nftMints', job.id), tx.get('wallets', user.uid)]);
                            if (current?.state === 'waiting') await tx.set('nftMints', job.id, { ...current, state: 'queued',
                                recipient: wallet.address, nextAttemptAt: now() });
                        });
                        if (waiting.length < 200) break;
                    }
                } catch {
                    // Binding is durable; the scheduled worker resumes any waiting jobs.
                }
                return send(response, 200, walletStatus(challenge.address));
            }
            if (method === 'GET' && path === '/v1/designs') {
                rateLimit('designs', user.uid, 60, 60);
                const entries = await adapter.query('designs', [['ownerId', '==', user.uid], ['status', '==', 'ready']], 100);
                const ready = entries.sort((a, b) => b.createdAt - a.createdAt);
                return send(response, 200, { items: await Promise.all(ready.map(designResponse)) });
            }
            if (method === 'POST' && path === '/v1/designs/prepare') {
                const data = await bodyJson(request);
                const operationId = text(data.operationId, 100, 'operationId');
                if (!/^[A-Za-z0-9_-]+$/.test(operationId)) bad('operationId is invalid');
                const name = content(data.name, 80, 'name');
                if (!DESIGN_KINDS.has(data.kind)) bad('kind is invalid');
                const imageBytes = number(data.imageBytes, 1, MAX_DESIGN_BYTES, 'imageBytes');
                const width = number(data.width, 1, MAX_DESIGN_EDGE, 'width');
                const height = number(data.height, 1, MAX_DESIGN_EDGE, 'height');
                if (![imageBytes, width, height].every(Number.isInteger)) bad('Design size is invalid');
                const id = digest(`design\0${user.uid}\0${operationId}`);
                const requestHash = digest(JSON.stringify({ name, kind: data.kind, imageBytes, width, height }));
                const day = Math.floor(now() / 86400);
                await adapter.transaction(async tx => {
                    if ((await tx.get('accounts', user.uid))?.deleted) denied();
                    const existing = await tx.get('designs', id);
                    if (existing) {
                        if (existing.ownerId !== user.uid || existing.requestHash !== requestHash) conflict('Operation ID already used');
                        if (existing.status === 'archived' || existing.status === 'abandoned') designExpired();
                        return;
                    }
                    const quotaId = digest(`design-quota\0${user.uid}\0${day}`);
                    const quota = await tx.get('designQuotas', quotaId);
                    if ((quota?.count ?? 0) >= 20) throw new ApiError(429, 'quota_exceeded', 'Daily design limit reached');
                    const countId = digest(`design-count\0${user.uid}`);
                    const count = await tx.get('designCounts', countId);
                    if ((count?.active ?? 0) >= 100) throw new ApiError(429, 'quota_exceeded', 'Design library limit reached');
                    await tx.set('designQuotas', quotaId, { id: quotaId, userId: user.uid, day, count: (quota?.count ?? 0) + 1 });
                    await tx.set('designCounts', countId, { id: countId, userId: user.uid, active: (count?.active ?? 0) + 1 });
                    await tx.set('designs', id, { id, ownerId: user.uid, operationId, requestHash, name, kind: data.kind,
                        width, height, imageBytes, status: 'pending', references: 0, revision: 1, createdAt: now() });
                });
                const design = await adapter.get('designs', id);
                if (design.status !== 'pending' && design.status !== 'ready') designExpired();
                if (design.status === 'ready') return send(response, 200, { id, uploadUrl: '', uploadHeaders: {} });
                return send(response, 200, { id, ...(await adapter.signDesignUpload(id)) });
            }
            if (method === 'POST' && segments[1] === 'designs' && segments[3] === 'finalize' && segments.length === 4) {
                await bodyJson(request);
                const id = segments[2];
                const design = await adapter.get('designs', id);
                if (!design) missing();
                if (design.ownerId !== user.uid) denied();
                if (design.status === 'ready') return send(response, 200, { design: await designResponse(design) });
                if (design.status !== 'pending') conflict('Design has been removed');
                const metadata = await adapter.designMetadata(id);
                if (!metadata || metadata.size !== design.imageBytes || metadata.contentType !== 'image/png') conflict('Design upload is incomplete');
                let image;
                try { image = await inspectDesignImage(await adapter.readDesignUpload(id, metadata)); }
                catch { bad('Uploaded image is not a valid PNG'); }
                if (image.width !== design.width || image.height !== design.height) bad('Image dimensions differ from prepare request');
                try { await adapter.saveDesignAssets(id, image.artwork, image.thumbnail); }
                catch (error) {
                    if (error.code === 'asset_conflict') conflict('Design artwork has already been finalized from a different upload');
                    throw error;
                }
                await adapter.transaction(async tx => {
                    if ((await tx.get('accounts', user.uid))?.deleted) denied();
                    const current = await tx.get('designs', id);
                    if (!current || current.ownerId !== user.uid || !['pending', 'ready'].includes(current.status)) conflict('Design has been removed');
                    if (current.status === 'pending') await tx.set('designs', id, { ...current, status: 'ready', width: image.width, height: image.height });
                });
                await adapter.cleanupPendingDesign(id);
                return send(response, 200, { design: await designResponse(await adapter.get('designs', id)) });
            }
            if (method === 'DELETE' && segments[1] === 'designs' && segments.length === 3) {
                const id = segments[2];
                const archived = await adapter.transaction(async tx => {
                    const design = await tx.get('designs', id);
                    if (!design) missing();
                    if (design.ownerId !== user.uid) denied();
                    if (design.status === 'archived' || design.status === 'abandoned') return design;
                    const countId = digest(`design-count\0${user.uid}`);
                    const count = await tx.get('designCounts', countId);
                    await tx.set('designCounts', countId, { id: countId, userId: user.uid, active: Math.max(0, (count?.active ?? 1) - 1) });
                    const updated = { ...design, status: 'archived', revision: design.revision + 1 };
                    await tx.set('designs', id, updated);
                    return updated;
                });
                if (!archived.references) await adapter.deleteDesignAssets(id);
                return send(response, 200, { ok: true });
            }
            if (method === 'POST' && path === '/v1/publications/prepare') {
                const data = await bodyJson(request);
                const operationId = text(data.operationId, 100, 'operationId');
                if (!/^[A-Za-z0-9_-]+$/.test(operationId)) bad('operationId is invalid');
                const hasPreset = typeof data.presetId === 'string' && data.presetId.length > 0;
                const hasDesign = typeof data.designId === 'string' && data.designId.length > 0;
                if (hasPreset === hasDesign) bad('Exactly one presetId or designId is required');
                if (hasPreset && !PRESETS.has(data.presetId)) bad('presetId is invalid');
                if (hasDesign && !/^[a-f0-9]{64}$/.test(data.designId)) bad('designId is invalid');
                const point = location(data.location, 100);
                const input = {
                    ...(hasPreset ? { presetId: data.presetId } : { designId: data.designId }),
                    place: content(data.place, 80, 'place'), teaser: content(data.teaser, 180, 'teaser'),
                    note: content(data.note, 2000, 'note'), latitude: point.latitude, longitude: point.longitude,
                    position: vector(data.position, ['x', 'y', 'z'], 'position'), rotation: vector(data.rotation, ['x', 'y', 'z', 'w'], 'rotation'),
                    widthMeters: number(data.widthMeters, 0.05, 2, 'widthMeters'), mapBytes: number(data.mapBytes, 1, MAX_MAP, 'mapBytes')
                };
                if (!Number.isInteger(input.mapBytes)) bad('mapBytes is invalid');
                const id = digest(`${user.uid}\0${operationId}`);
                const { latitude: _latitude, longitude: _longitude, ...stableInput } = input;
                const requestHash = digest(JSON.stringify(stableInput));
                const day = Math.floor(now() / 86400);
                await adapter.transaction(async tx => {
                    if ((await tx.get('accounts', user.uid))?.deleted) denied();
                    const existing = await tx.get('stickers', id);
                    if (existing) {
                        if (existing.authorId !== user.uid || existing.requestHash !== requestHash) conflict('Operation ID already used');
                        if (distanceMeters(point, existing) > 100) denied();
                        return;
                    }
                    const design = hasDesign ? await tx.get('designs', data.designId) : null;
                    if (hasDesign && (!design || design.status !== 'ready' || design.ownerId !== user.uid)) denied();
                    const quotaId = digest(`${user.uid}\0${day}`);
                    const quota = await tx.get('quotas', quotaId);
                    if ((quota?.count ?? 0) >= 5) throw new ApiError(429, 'quota_exceeded', 'Daily publication limit reached');
                    await tx.set('quotas', quotaId, { id: quotaId, userId: user.uid, day, count: (quota?.count ?? 0) + 1 });
                    if (design) await tx.set('designs', design.id, { ...design, references: (design.references || 0) + 1 });
                    await tx.set('stickers', id, { id, authorId: user.uid, authorName: String(user.name || 'Explorer').slice(0, 80),
                        operationId, requestHash, ...input,
                        ...(design ? { artworkWidth: design.width, artworkHeight: design.height } : {}),
                        geoCell: geohash(point.latitude, point.longitude), status: 'pending',
                        createdAt: now(), revision: 1 });
                });
                const sticker = await adapter.get('stickers', id);
                if (sticker.status !== 'pending') return send(response, 200, { id, uploadUrl: '', uploadHeaders: {} });
                return send(response, 200, { id, ...(await adapter.signUpload(id, input.mapBytes)) });
            }

            if (method === 'POST' && segments[1] === 'publications' && segments[3] === 'finalize' && segments.length === 4) {
                const id = segments[2];
                const data = await bodyJson(request);
                const operationId = text(data.operationId, 100, 'operationId');
                const point = location(data.location, 100);
                const sticker = await adapter.get('stickers', id);
                if (!sticker) missing();
                if (sticker.authorId !== user.uid) denied();
                if (sticker.operationId !== operationId) conflict('Operation ID mismatch');
                if (sticker.status === 'published') return send(response, 200, { sticker: await visibleSummary(sticker) });
                if (sticker.status !== 'pending') conflict('Publication is unavailable');
                if (distanceMeters(point, sticker) > 100) denied();
                const metadata = await adapter.mapMetadata(id);
                if (!metadata || Number(metadata.size) !== sticker.mapBytes || metadata.contentType !== 'application/octet-stream') conflict('World map upload is incomplete');
                await adapter.finalizeMap(id, metadata);
                await adapter.transaction(async tx => {
                    if ((await tx.get('accounts', user.uid))?.deleted) denied();
                    const current = await tx.get('stickers', id);
                    if (!current || current.authorId !== user.uid) denied();
                    if (current.status === 'pending') await tx.set('stickers', id, { ...current, status: 'published', publishedAt: now() });
                    else if (current.status !== 'published') conflict('Publication is unavailable');
                });
                return send(response, 200, { sticker: await visibleSummary(await adapter.get('stickers', id)) });
            }

            if (method === 'POST' && segments[1] === 'stickers' && segments[3] === 'recover' && segments.length === 4) {
                rateLimit('recover', user.uid, 20, 60);
                const point = location((await bodyJson(request)).location);
                const sticker = await active(segments[2]);
                if (await isBlocked(user.uid, sticker.authorId)) denied();
                if (distanceMeters(point, sticker) > 100) denied();
                const discoveryId = ids();
                const expiresAt = now() + DISCOVERY_SECONDS;
                await adapter.set('discoveries', discoveryId, { id: discoveryId, userId: user.uid, stickerId: sticker.id, expiresAt });
                const mapUrl = await adapter.signDownload(sticker.id, expiresAt);
                return send(response, 200, { sticker: await visibleSummary(sticker), discoveryId, expiresAt, mapUrl,
                    position: sticker.position, rotation: sticker.rotation, widthMeters: sticker.widthMeters });
            }

            if (method === 'POST' && segments[1] === 'stickers' && segments[3] === 'collect' && segments.length === 4) {
                const data = await bodyJson(request);
                const point = location(data.location);
                const discoveryId = text(data.discoveryId, 128, 'discoveryId');
                const id = segments[2];
                const collectionId = digest(`${user.uid}\0${id}`);
                const collectedAt = await adapter.transaction(async tx => {
                    if ((await tx.get('accounts', user.uid))?.deleted) denied();
                    const sticker = await tx.get('stickers', id);
                    if (!sticker) missing();
                    if (await tx.get('blocks', digest(`${user.uid}\0${sticker.authorId}`))) denied();
                    const existing = await tx.get('collections', collectionId);
                    if (existing) return existing.collectedAt;
                    if (sticker.status !== 'published') missing();
                    const discovery = await tx.get('discoveries', discoveryId);
                    if (!discovery || discovery.userId !== user.uid || discovery.stickerId !== id || discovery.expiresAt < now()) denied();
                    if (distanceMeters(point, sticker) > 100) denied();
                    const timestamp = now();
                    const wallet = nft.enabled ? await tx.get('wallets', user.uid) : null;
                    await tx.set('collections', collectionId, { id: collectionId, userId: user.uid, stickerId: id, collectedAt: timestamp });
                    if (nft.enabled) {
                        const freshTokenId = tokenId();
                        if (typeof freshTokenId !== 'string' || !/^(0|[1-9][0-9]*)$/.test(freshTokenId) ||
                            BigInt(freshTokenId) >= 2n ** 256n) throw new Error('Invalid token ID generator');
                        await tx.set('nftMints', collectionId, { id: collectionId, userId: user.uid, stickerId: id,
                            // Custom artwork remains private; its souvenir uses the generic first Taggi preset.
                            authorId: sticker.authorId, tokenId: freshTokenId, preset: sticker.designId ? 0 : Number(sticker.presetId.slice(-1)) - 1,
                            chainId: 11155111, contractAddress: nft.contractAddress,
                            recipient: wallet?.address || '', state: wallet ? 'queued' : 'waiting',
                            createdAt: timestamp, nextAttemptAt: timestamp, attempts: 0, transactionHash: '' });
                    }
                    return timestamp;
                });
                return send(response, 200, { sticker: await collected(user.uid, id, collectedAt) });
            }

            if (method === 'GET' && path === '/v1/collection') {
                rateLimit('collection', user.uid, 60, 60);
                const entries = await adapter.query('collections', [['userId', '==', user.uid]], 1000);
                const items = await Promise.all(entries.sort((a, b) => a.collectedAt - b.collectedAt).map(item => collected(user.uid, item.stickerId, item.collectedAt)));
                return send(response, 200, { items });
            }
            if (method === 'GET' && path === '/v1/authored') {
                rateLimit('authored', user.uid, 60, 60);
                const entries = await adapter.query('stickers', [['authorId', '==', user.uid]], 1000);
                const visible = entries.filter(item => item.status === 'published' || item.status === 'withdrawn').sort((a, b) => b.createdAt - a.createdAt);
                return send(response, 200, { items: await Promise.all(visible.map(visibleSummary)) });
            }
            if (method === 'POST' && segments[1] === 'stickers' && segments[3] === 'withdraw' && segments.length === 4) {
                await bodyJson(request);
                await adapter.transaction(async tx => {
                    if ((await tx.get('accounts', user.uid))?.deleted) denied();
                    const sticker = await tx.get('stickers', segments[2]);
                    if (!sticker) missing();
                    if (sticker.authorId !== user.uid) denied();
                    if (sticker.status === 'published') await tx.set('stickers', sticker.id, { ...sticker, status: 'withdrawn', revision: sticker.revision + 1 });
                    else if (sticker.status !== 'withdrawn') conflict('Sticker cannot be withdrawn');
                });
                return send(response, 200, { ok: true });
            }
            if (method === 'POST' && segments[1] === 'stickers' && segments[3] === 'report' && segments.length === 4) {
                rateLimit('report', user.uid, 10, 3600);
                const reason = text((await bodyJson(request)).reason, 500, 'reason');
                const sticker = await adapter.get('stickers', segments[2]);
                if (!sticker || !['published', 'withdrawn', 'removed'].includes(sticker.status)) missing();
                if (sticker.authorId === user.uid) denied();
                const id = digest(`${user.uid}\0${sticker.id}`);
                await adapter.transaction(async tx => {
                    if ((await tx.get('accounts', user.uid))?.deleted) denied();
                    if (!(await tx.get('reports', id))) await tx.set('reports', id, { id, stickerId: sticker.id, reporterId: user.uid, reason, status: 'open', createdAt: now() });
                });
                return send(response, 200, { ok: true });
            }
            if (method === 'POST' && path === '/v1/blocks') {
                const authorId = text((await bodyJson(request)).authorId, 128, 'authorId');
                if (authorId === user.uid) bad('Cannot block self');
                const id = digest(`${user.uid}\0${authorId}`);
                await adapter.transaction(async tx => {
                    if ((await tx.get('accounts', user.uid))?.deleted) denied();
                    await tx.set('blocks', id, { id, userId: user.uid, authorId, createdAt: now() });
                });
                return send(response, 200, { ok: true });
            }
            if (method === 'DELETE' && path === '/v1/account') {
                await adapter.transaction(async tx => { await tx.set('accounts', user.uid, { id: user.uid, deleted: true, authDeleted: false, cleaned: false, deletedAt: now() }); });
                try {
                    await adapter.deleteAuth(request.headers.authorization.slice(7));
                } catch (error) {
                    await adapter.set('accounts', user.uid, { id: user.uid, deleted: false });
                    if (error.code === 'auth/recent-login-required') throw new ApiError(401, 'recent_login_required', 'Sign in again before deleting account');
                    throw error;
                }
                try {
                    await adapter.set('accounts', user.uid, { id: user.uid, deleted: true, authDeleted: true, cleaned: false, deletedAt: now() });
                    await deleteAccountData(adapter, user.uid, now());
                    return send(response, 200, { ok: true });
                } catch {
                    return send(response, 200, { ok: true, cleanupPending: true });
                }
            }
            if (segments[1] === 'admin') {
                if (!user.admin) denied();
                if (method === 'GET' && path === '/v1/admin/reports') {
                    const entries = await adapter.query('reports', [['status', '==', 'open']], 200);
                    return send(response, 200, { items: entries.sort((a, b) => a.createdAt - b.createdAt) });
                }
                if (method === 'GET' && path === '/v1/admin/removed') {
                    const entries = await adapter.query('stickers', [['status', '==', 'removed']], 200);
                    return send(response, 200, { items: entries.sort((a, b) => b.createdAt - a.createdAt).map(summary) });
                }
                if (method === 'POST' && segments[2] === 'stickers' && segments[4] === 'moderate' && segments.length === 5) {
                    const status = (await bodyJson(request)).status;
                    if (!['removed', 'published'].includes(status)) bad('status is invalid');
                    await adapter.transaction(async tx => {
                        const sticker = await tx.get('stickers', segments[3]);
                        if (!sticker || sticker.status === 'deleted' || sticker.status === 'pending') missing();
                        if (status === 'removed' && sticker.status !== 'removed') await tx.set('stickers', sticker.id, { ...sticker, status: 'removed', preModerationStatus: sticker.status, revision: sticker.revision + 1 });
                        else if (status === 'published' && sticker.status === 'removed') await tx.set('stickers', sticker.id, { ...sticker, status: sticker.preModerationStatus || 'published', revision: sticker.revision + 1 });
                        const reports = await adapter.query('reports', [['stickerId', '==', sticker.id]], 200);
                        for (const report of reports) await tx.set('reports', report.id, { ...report, status: status === 'removed' ? 'actioned' : 'reviewed' });
                    });
                    return send(response, 200, { ok: true });
                }
            }
            throw new ApiError(404, 'not_found', 'Route not found');
        } catch (error) {
            const status = error instanceof ApiError ? error.status : 500;
            if (status === 500) console.error('tagtag API error:', error?.code || error?.name || 'unknown');
            send(response, status, { error: { code: error instanceof ApiError ? error.code : 'internal', message: error instanceof ApiError ? error.message : 'Internal error' } });
        }
    };
}
