export class MemoryAdapter {
    constructor() {
        this.collections = new Map();
        this.uploads = new Map();
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
            if (op === '<=') return item[field] <= value;
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
}
