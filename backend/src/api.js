import { createHash, randomUUID } from 'node:crypto';
import { distanceMeters, geohash, neighboringCells } from './geo.js';

const MAX_MAP = 16 * 1024 * 1024;
const PRESETS = new Set(['taggi-1', 'taggi-2', 'taggi-3', 'taggi-4']);
const MAX_BODY = 32768;
const DISCOVERY_SECONDS = 300;
const NEARBY_METERS = 5000;

class ApiError extends Error {
    constructor(status, code, message) { super(message); this.status = status; this.code = code; }
}
const bad = (message) => { throw new ApiError(400, 'invalid_request', message); };
const denied = () => { throw new ApiError(403, 'forbidden', 'Access denied'); };
const missing = () => { throw new ApiError(404, 'not_found', 'Sticker unavailable'); };
const conflict = (message) => { throw new ApiError(409, 'conflict', message); };
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
    id: sticker.id, presetId: sticker.presetId, authorId: sticker.authorId,
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

export function createApi({ adapter, now = () => Math.floor(Date.now() / 1000), ids = randomUUID }) {
    if (!adapter) throw new Error('Adapter required');
    const location = value => {
        const point = coordinates(value);
        number(value.accuracyMeters, 0, 50, 'accuracyMeters');
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
        return sticker;
    };
    const isBlocked = async (uid, authorId) => !!(await adapter.get('blocks', digest(`${uid}\0${authorId}`)));
    const collected = async (uid, id, collectedAt) => {
        const sticker = await adapter.get('stickers', id);
        const blocked = sticker && await isBlocked(uid, sticker.authorId);
        const unavailable = !sticker || sticker.status === 'removed' || sticker.status === 'deleted' || blocked;
        return sticker ? { ...summary(sticker), note: unavailable ? '' : sticker.note, collectedAt, unavailable: !!unavailable } : {
            id, presetId: '', authorId: '', authorName: '', place: '', teaser: '', latitude: 0, longitude: 0, revision: 0, createdAt: 0,
            note: '', collectedAt, unavailable: true
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
                const apiKey = process.env.FIREBASE_WEB_API_KEY;
                const authDomain = process.env.FIREBASE_AUTH_DOMAIN;
                if (!apiKey || !authDomain) throw new ApiError(503, 'not_configured', 'Admin sign-in unavailable');
                return send(response, 200, { apiKey, authDomain });
            }
            const segments = path.split('/').filter(Boolean);
            if (segments[0] !== 'v1') throw new ApiError(404, 'not_found', 'Route not found');

            if (method === 'POST' && path === '/v1/nearby') {
                const user = await auth(request, true);
                const point = location((await bodyJson(request)).location);
                const blocks = user ? await adapter.query('blocks', [['userId', '==', user.uid]], 1000) : [];
                const excluded = new Set(blocks.map(item => item.authorId));
                const candidates = await adapter.query('stickers', [['geoCell', 'in', neighboringCells(point.latitude, point.longitude)], ['status', '==', 'published']], 500);
                const items = candidates.filter(item => !excluded.has(item.authorId) && distanceMeters(point, item) <= NEARBY_METERS)
                    .sort((a, b) => distanceMeters(point, a) - distanceMeters(point, b)).slice(0, 100).map(summary);
                return send(response, 200, { items });
            }

            const user = await auth(request);
            if (method === 'POST' && path === '/v1/publications/prepare') {
                const data = await bodyJson(request);
                const operationId = text(data.operationId, 100, 'operationId');
                if (!/^[A-Za-z0-9_-]+$/.test(operationId)) bad('operationId is invalid');
                if (!PRESETS.has(data.presetId)) bad('presetId is invalid');
                const point = location(data.location);
                const input = {
                    presetId: data.presetId, place: content(data.place, 80, 'place'), teaser: content(data.teaser, 180, 'teaser'),
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
                    const quotaId = digest(`${user.uid}\0${day}`);
                    const quota = await tx.get('quotas', quotaId);
                    if ((quota?.count ?? 0) >= 5) throw new ApiError(429, 'quota_exceeded', 'Daily publication limit reached');
                    await tx.set('quotas', quotaId, { id: quotaId, userId: user.uid, day, count: (quota?.count ?? 0) + 1 });
                    await tx.set('stickers', id, { id, authorId: user.uid, authorName: String(user.name || 'Explorer').slice(0, 80),
                        operationId, requestHash, ...input, geoCell: geohash(point.latitude, point.longitude), status: 'pending',
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
                const point = location(data.location);
                const sticker = await adapter.get('stickers', id);
                if (!sticker) missing();
                if (sticker.authorId !== user.uid) denied();
                if (sticker.operationId !== operationId) conflict('Operation ID mismatch');
                if (sticker.status === 'published') return send(response, 200, { sticker: summary(sticker) });
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
                return send(response, 200, { sticker: summary(await adapter.get('stickers', id)) });
            }

            if (method === 'POST' && segments[1] === 'stickers' && segments[3] === 'recover' && segments.length === 4) {
                const point = location((await bodyJson(request)).location);
                const sticker = await active(segments[2]);
                if (await isBlocked(user.uid, sticker.authorId)) denied();
                if (distanceMeters(point, sticker) > 100) denied();
                const discoveryId = ids();
                const expiresAt = now() + DISCOVERY_SECONDS;
                await adapter.set('discoveries', discoveryId, { id: discoveryId, userId: user.uid, stickerId: sticker.id, expiresAt });
                const mapUrl = await adapter.signDownload(sticker.id, expiresAt);
                return send(response, 200, { sticker: summary(sticker), discoveryId, expiresAt, mapUrl,
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
                    await tx.set('collections', collectionId, { id: collectionId, userId: user.uid, stickerId: id, collectedAt: timestamp });
                    return timestamp;
                });
                return send(response, 200, { sticker: await collected(user.uid, id, collectedAt) });
            }

            if (method === 'GET' && path === '/v1/collection') {
                const entries = await adapter.query('collections', [['userId', '==', user.uid]], 1000);
                const items = await Promise.all(entries.sort((a, b) => a.collectedAt - b.collectedAt).map(item => collected(user.uid, item.stickerId, item.collectedAt)));
                return send(response, 200, { items });
            }
            if (method === 'GET' && path === '/v1/authored') {
                const entries = await adapter.query('stickers', [['authorId', '==', user.uid]], 1000);
                return send(response, 200, { items: entries.filter(item => item.status === 'published' || item.status === 'withdrawn').sort((a, b) => b.createdAt - a.createdAt).map(summary) });
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
                await adapter.transaction(async tx => { await tx.set('accounts', user.uid, { id: user.uid, deleted: true, deletedAt: now() }); });
                try {
                    await adapter.deleteAuth(request.headers.authorization.slice(7));
                } catch (error) {
                    await adapter.set('accounts', user.uid, { id: user.uid, deleted: false });
                    if (error.code === 'auth/recent-login-required') throw new ApiError(401, 'recent_login_required', 'Sign in again before deleting account');
                    throw error;
                }
                await adapter.set('accounts', user.uid, { id: user.uid, deleted: true, authDeleted: true, deletedAt: now() });
                const authored = await adapter.query('stickers', [['authorId', '==', user.uid]], 10000);
                for (const sticker of authored) {
                    await adapter.set('stickers', sticker.id, { ...sticker, status: 'deleted', note: '', revision: sticker.revision + 1 });
                    await adapter.deleteMap(sticker.id);
                }
                for (const name of ['collections', 'blocks', 'reports', 'discoveries']) {
                    const entries = await adapter.query(name, [[name === 'reports' ? 'reporterId' : 'userId', '==', user.uid]], 10000);
                    for (const item of entries) await adapter.delete(name, item.id);
                }
                await adapter.set('accounts', user.uid, { id: user.uid, deleted: true, authDeleted: true, cleaned: true, deletedAt: now() });
                return send(response, 200, { ok: true });
            }
            if (segments[1] === 'admin') {
                if (!user.admin) denied();
                if (method === 'GET' && path === '/v1/admin/reports') {
                    const entries = await adapter.query('reports', [['status', '==', 'open']], 200);
                    return send(response, 200, { items: entries.sort((a, b) => a.createdAt - b.createdAt) });
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
