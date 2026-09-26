import { initializeApp, applicationDefault } from 'firebase-admin/app';
import { getAuth } from 'firebase-admin/auth';
import { getFirestore } from 'firebase-admin/firestore';
import { getStorage } from 'firebase-admin/storage';
import { createHash } from 'node:crypto';

export function createFirebaseAdapter({ projectId = process.env.GOOGLE_CLOUD_PROJECT || process.env.GCLOUD_PROJECT,
    bucketName = process.env.TAGTAG_MAP_BUCKET } = {}) {
    if (!projectId || !bucketName || !process.env.FIREBASE_API_KEY) throw new Error('GOOGLE_CLOUD_PROJECT, TAGTAG_MAP_BUCKET and FIREBASE_API_KEY are required');
    const emulator = process.env.FIRESTORE_EMULATOR_HOST || process.env.FIREBASE_AUTH_EMULATOR_HOST;
    const app = initializeApp({ ...(emulator ? {} : { credential: applicationDefault() }), projectId, storageBucket: bucketName });
    const db = getFirestore(app);
    const auth = getAuth(app);
    const bucket = getStorage(app).bucket(bucketName);
    const ref = (name, id) => db.collection(name).doc(id);
    const pending = id => bucket.file(`pending/${id}`);
    const final = id => bucket.file(`maps/${id}`);
    const pendingDesign = id => bucket.file(`pending-designs/${id}`);
    const designArtwork = id => bucket.file(`designs/${id}/artwork.png`);
    const designThumbnail = id => bucket.file(`designs/${id}/thumbnail.png`);
    const data = snapshot => snapshot.exists ? snapshot.data() : null;

    return {
        async get(name, id) { return data(await ref(name, id).get()); },
        async set(name, id, value) { await ref(name, id).set(value); },
        async delete(name, id) { await ref(name, id).delete(); },
        async query(name, filters = [], limit = 1000) {
            let query = db.collection(name);
            for (const [field, operation, value] of filters) query = query.where(field, operation, value);
            const snapshot = await query.limit(limit).get();
            return snapshot.docs.map(item => item.data());
        },
        async transaction(callback) {
            return db.runTransaction(async firestoreTransaction => {
                const tx = {
                    async get(name, id) { return data(await firestoreTransaction.get(ref(name, id))); },
                    set(name, id, value) { firestoreTransaction.set(ref(name, id), value); },
                    delete(name, id) { firestoreTransaction.delete(ref(name, id)); }
                };
                return callback(tx);
            });
        },
        async verifyToken(token) { return auth.verifyIdToken(token, true); },
        async userExists(uid) {
            try { await auth.getUser(uid); return true; }
            catch (error) { if (error.code === 'auth/user-not-found') return false; throw error; }
        },
        async deleteAuth(idToken) {
            const apiKey = process.env.FIREBASE_API_KEY;
            if (!apiKey) throw new Error('FIREBASE_API_KEY is required for account deletion');
            const base = process.env.FIREBASE_AUTH_EMULATOR_HOST
                ? `http://${process.env.FIREBASE_AUTH_EMULATOR_HOST}/identitytoolkit.googleapis.com/v1`
                : 'https://identitytoolkit.googleapis.com/v1';
            const response = await fetch(`${base}/accounts:delete?key=${encodeURIComponent(apiKey)}`, {
                method: 'POST', headers: { 'content-type': 'application/json' }, body: JSON.stringify({ idToken })
            });
            if (!response.ok) {
                const result = await response.json().catch(() => ({}));
                const error = new Error('Firebase account deletion failed');
                if (result.error?.message === 'CREDENTIAL_TOO_OLD_LOGIN_AGAIN') error.code = 'auth/recent-login-required';
                throw error;
            }
        },
        async signUpload(id) {
            const headers = { 'content-type': 'application/octet-stream', 'x-goog-content-length-range': '1,16777216' };
            const [uploadUrl] = await pending(id).getSignedUrl({ version: 'v4', action: 'write', expires: Date.now() + 5 * 60 * 1000,
                contentType: headers['content-type'], extensionHeaders: { 'x-goog-content-length-range': headers['x-goog-content-length-range'] } });
            return { uploadUrl, uploadHeaders: headers };
        },
        async mapMetadata(id) {
            try {
                const [metadata] = await pending(id).getMetadata();
                return { size: Number(metadata.size), contentType: metadata.contentType, generation: metadata.generation };
            } catch (error) {
                if (error.code === 404) return null;
                throw error;
            }
        },
        async finalizeMap(id, metadata) {
            const source = bucket.file(`pending/${id}`, { generation: metadata.generation });
            try {
                await source.copy(final(id), { preconditionOpts: { ifGenerationMatch: 0 } });
            } catch (error) {
                if (error.code !== 412) throw error;
            }
            const [destination] = await final(id).getMetadata();
            if (Number(destination.size) !== Number(metadata.size) || destination.contentType !== 'application/octet-stream') throw new Error('Final map mismatch');
        },
        async signDownload(id, expiresAt) {
            const [url] = await final(id).getSignedUrl({ version: 'v4', action: 'read', expires: expiresAt * 1000,
                responseDisposition: 'attachment; filename="tagtag-world-map.bin"', responseType: 'application/octet-stream' });
            return url;
        },
        async deleteMap(id) {
            await Promise.all([pending(id).delete({ ignoreNotFound: true }), final(id).delete({ ignoreNotFound: true })]);
        },
        async cleanupPending(id) { await pending(id).delete({ ignoreNotFound: true }); },
        async signDesignUpload(id) {
            const headers = { 'content-type': 'image/png', 'x-goog-content-length-range': '1,5242880' };
            const [uploadUrl] = await pendingDesign(id).getSignedUrl({ version: 'v4', action: 'write', expires: Date.now() + 5 * 60 * 1000,
                contentType: headers['content-type'], extensionHeaders: { 'x-goog-content-length-range': headers['x-goog-content-length-range'] } });
            return { uploadUrl, uploadHeaders: headers };
        },
        async designMetadata(id) {
            try {
                const [metadata] = await pendingDesign(id).getMetadata();
                return { size: Number(metadata.size), contentType: metadata.contentType, generation: metadata.generation };
            } catch (error) {
                if (error.code === 404) return null;
                throw error;
            }
        },
        async readDesignUpload(id, metadata) {
            const [bytes] = await bucket.file(`pending-designs/${id}`, { generation: metadata.generation }).download();
            return bytes;
        },
        async saveDesignAssets(id, artwork, thumbnail) {
            const saveImmutable = async (file, bytes) => {
                try {
                    await file.save(bytes, { resumable: false, contentType: 'image/png', preconditionOpts: { ifGenerationMatch: 0 } });
                } catch (error) {
                    if (error.code !== 412) throw error;
                    const [existing] = await file.download();
                    const hash = value => createHash('sha256').update(value).digest('hex');
                    if (hash(existing) !== hash(bytes)) throw Object.assign(new Error('Design asset already differs'), { code: 'asset_conflict' });
                }
            };
            await saveImmutable(designArtwork(id), artwork);
            await saveImmutable(designThumbnail(id), thumbnail);
        },
        async signDesignRead(id, type) {
            const file = type === 'artwork' ? designArtwork(id) : type === 'thumbnail' ? designThumbnail(id) : null;
            if (!file) throw new Error('Invalid design asset type');
            const [url] = await file.getSignedUrl({ version: 'v4', action: 'read', expires: Date.now() + 5 * 60 * 1000,
                responseType: 'image/png', responseDisposition: 'inline' });
            return url;
        },
        async deleteDesignAssets(id) {
            await Promise.all([pendingDesign(id), designArtwork(id), designThumbnail(id)].map(file => file.delete({ ignoreNotFound: true })));
        },
        async cleanupPendingDesign(id) { await pendingDesign(id).delete({ ignoreNotFound: true }); },
        async listDesignAssetIds() {
            const [files] = await bucket.getFiles({ prefix: 'designs/' });
            return [...new Set(files.map(file => /^designs\/([a-f0-9]{64})\/(?:artwork|thumbnail)\.png$/.exec(file.name)?.[1]).filter(Boolean))];
        },
        db, bucket
    };
}
