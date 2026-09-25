import { applicationDefault, initializeApp } from 'firebase-admin/app';
import { getAuth } from 'firebase-admin/auth';

const [uid, setting] = process.argv.slice(2);
if (!uid || !['true', 'false'].includes(setting)) {
    console.error('Usage: node operator/set-admin.js FIREBASE_UID true|false');
    process.exit(2);
}
const projectId = process.env.GOOGLE_CLOUD_PROJECT;
if (!projectId) throw new Error('GOOGLE_CLOUD_PROJECT is required');
const auth = getAuth(initializeApp({ credential: applicationDefault(), projectId }));
const user = await auth.getUser(uid);
await auth.setCustomUserClaims(uid, { ...user.customClaims, admin: setting === 'true' });
await auth.revokeRefreshTokens(uid);
console.log(`Admin claim ${setting === 'true' ? 'granted' : 'removed'} for ${uid}.`);
