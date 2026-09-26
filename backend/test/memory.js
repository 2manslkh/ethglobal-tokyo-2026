export class MemoryAdapter {
    constructor() {
        this.collections = new Map();
        this.uploads = new Map();
        this.designUploads = new Map();
        this.designAssets = new Map();
        this.deletedUsers = new Set();
        this.tail = Promise.resolve();
    }
    bucket(name) {
        if (!this.collections.has(name)) this.collections.set(name, new Map());
        return this.collections.get(name);
    }
    async get(name, id) { return structuredClone(this.bucket(name).get(id) ?? null); }
    async set(name, id, value) { this.bucket(name).set(id, structuredClone(value)); }
    async delete(name, id) { this.bucket(name).delete(id); }
    async query(name, filters = [], limit = 1000) {
        return [...this.bucket(name).values()].filter(item => filters.every(([field, op, value]) => {
            if (op === '==') return item[field] === value;
            if (op === 'in') return value.includes(item[field]);
            if (op === '<') return item[field] < value;
            throw new Error(`Unknown filter ${op}`);
        })).slice(0, limit).map(item => structuredClone(item));
    }
    async transaction(callback) {
        const previous = this.tail;
        let release;
        this.tail = new Promise(resolve => { release = resolve; });
        await previous;
        try { return await callback(this); } finally { release(); }
    }
    async verifyToken(token) {
        if (!['alice', 'bob', 'admin'].includes(token) || this.deletedUsers.has(token)) throw new Error('Invalid token');
        return { uid: token, name: token, admin: token === 'admin' };
    }
    async deleteAuth(token) { if (this.deleteAuthFailure) throw this.deleteAuthFailure; this.deletedUsers.add(token); }
    async userExists(uid) { return !this.deletedUsers.has(uid); }
    async signUpload(id) { return { uploadUrl: `https://upload.example/${id}`, uploadHeaders: { 'content-type': 'application/octet-stream', 'x-goog-content-length-range': '1,16777216' } }; }
    async signDownload(id) { return `https://download.example/${id}`; }
    upload(id, size) { this.uploads.set(id, size); }
    async mapMetadata(id) { return this.uploads.has(id) ? { size: this.uploads.get(id), contentType: 'application/octet-stream' } : null; }
    async finalizeMap(id) { if (!this.uploads.has(id)) throw new Error('Missing map'); }
    async deleteMap(id) { this.uploads.delete(id); }
    async signDesignUpload(id) { return { uploadUrl: `https://upload.example/designs/${id}`, uploadHeaders: { 'content-type': 'image/png', 'x-goog-content-length-range': '1,5242880' } }; }
    uploadDesign(id, bytes) { this.designUploads.set(id, Buffer.from(bytes)); }
    async designMetadata(id) { const bytes = this.designUploads.get(id); return bytes ? { size: bytes.length, contentType: 'image/png', generation: '1' } : null; }
    async readDesignUpload(id) { return this.designUploads.get(id); }
    async saveDesignAssets(id, artwork, thumbnail) {
        if (!this.designAssets.has(id)) this.designAssets.set(id, { artwork: Buffer.from(artwork), thumbnail: Buffer.from(thumbnail) });
        else if (!this.designAssets.get(id).artwork.equals(artwork)) throw Object.assign(new Error('Design asset already differs'), { code: 'asset_conflict' });
    }
    async signDesignRead(id, type) { return `https://download.example/designs/${id}/${type}`; }
    async deleteDesignAssets(id) { this.designUploads.delete(id); this.designAssets.delete(id); }
    async cleanupPendingDesign(id) { this.designUploads.delete(id); }
    async listDesignAssetIds() { return [...this.designAssets.keys()]; }
    thumbnail(id) { return this.designAssets.get(id)?.thumbnail; }
}
