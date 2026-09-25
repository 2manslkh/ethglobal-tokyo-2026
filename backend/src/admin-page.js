export const adminPage = `<!doctype html>
<html lang="en"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>tagtag moderation</title><link rel="stylesheet" href="/admin.css"></head>
<body><main><header><span class="mark">tagtag</span><h1>Moderation</h1><p>Review reports and control who can publish content.</p></header>
<section class="panel"><h2>Operator sign in</h2><div class="actions"><button id="google">Sign in with Google</button><button id="apple">Sign in with Apple</button><button id="signout" hidden>Sign out</button></div><p id="status" role="status">Loading sign in…</p></section>
<section id="workspace" hidden><div class="panel"><div class="row"><h2>Open reports</h2><button id="refresh">Refresh</button></div><div id="reports"></div></div><div class="panel"><h2>Removed stickers</h2><div id="removed"></div></div></section></main><script type="module" src="/admin.js"></script></body></html>`;

export const adminCss = `:root{font-family:system-ui,sans-serif;color:#171717;background:#fffdf6}*{box-sizing:border-box}main{max-width:900px;margin:0 auto;padding:24px}header{margin:28px 0}.mark{font-weight:900;font-size:22px;letter-spacing:-.08em}h1{font-size:clamp(2.5rem,6vw,4rem);margin:10px 0}h2{font-size:1.25rem}p{line-height:1.55;color:#444}.panel{border:2px solid #171717;border-radius:16px;padding:24px;margin:20px 0;background:white;box-shadow:4px 4px 0 #f3d237}.row,.actions{display:flex;align-items:center;justify-content:space-between;gap:12px;flex-wrap:wrap}button,select,input{font:inherit;min-height:44px;padding:8px 12px;border:2px solid #171717;border-radius:9px;background:white}button{cursor:pointer;background:#f6db4d;font-weight:700}button:focus-visible,input:focus-visible,select:focus-visible{outline:3px solid #295ca8;outline-offset:3px}label{display:block;margin:14px 0;font-weight:600}label input,label select{display:block;width:100%;margin-top:6px}.report{border-top:1px solid #ccc;padding:16px 0}.report p{overflow-wrap:anywhere}.muted{color:#666;font-size:.9rem}`;

export const adminScript = `import { initializeApp } from 'https://www.gstatic.com/firebasejs/11.10.0/firebase-app.js';
import { getAuth, GoogleAuthProvider, OAuthProvider, signInWithPopup, onAuthStateChanged, signOut, getIdTokenResult } from 'https://www.gstatic.com/firebasejs/11.10.0/firebase-auth.js';

const element = id => document.getElementById(id);
const status = message => { element('status').textContent = message; };
let auth;
let currentUser;

async function api(path, method = 'GET', body) {
    const token = await currentUser.getIdToken();
    const response = await fetch(path, { method, headers: { authorization: 'Bearer ' + token, 'content-type': 'application/json' }, body: body === undefined ? undefined : JSON.stringify(body) });
    const result = await response.json();
    if (!response.ok) throw new Error(result.error?.message || 'Request failed');
    return result;
}

async function refresh() {
    const result = await api('/v1/admin/reports');
    const list = element('reports');
    list.replaceChildren();
    if (!result.items.length) { const empty = document.createElement('p'); empty.textContent = 'No open reports.'; list.append(empty); }
    for (const report of result.items) {
        const row = document.createElement('article'); row.className = 'report';
        const title = document.createElement('strong'); title.textContent = 'Sticker ' + report.stickerId;
        const reason = document.createElement('p'); reason.textContent = report.reason;
        const by = document.createElement('p'); by.className = 'muted'; by.textContent = 'Reported by ' + report.reporterId;
        const remove = document.createElement('button'); remove.textContent = 'Remove sticker';
        remove.addEventListener('click', async () => { try { await api('/v1/admin/stickers/' + encodeURIComponent(report.stickerId) + '/moderate', 'POST', { status: 'removed' }); await refresh(); status('Sticker removed.'); } catch (error) { status(error.message); } });
        const restore = document.createElement('button'); restore.textContent = 'Restore sticker';
        restore.addEventListener('click', async () => { try { await api('/v1/admin/stickers/' + encodeURIComponent(report.stickerId) + '/moderate', 'POST', { status: 'published' }); await refresh(); status('Sticker reviewed.'); } catch (error) { status(error.message); } });
        const actions = document.createElement('div'); actions.className = 'actions'; actions.append(remove, restore);
        row.append(title, reason, by, actions); list.append(row);
    }
    const removed = await api('/v1/admin/removed');
    const removedList = element('removed'); removedList.replaceChildren();
    if (!removed.items.length) { const empty = document.createElement('p'); empty.textContent = 'No removed stickers.'; removedList.append(empty); }
    for (const sticker of removed.items) {
        const row = document.createElement('article'); row.className = 'report';
        const label = document.createElement('strong'); label.textContent = sticker.place + ' · ' + sticker.id;
        const button = document.createElement('button'); button.textContent = 'Restore sticker';
        button.addEventListener('click', async () => { try { await api('/v1/admin/stickers/' + encodeURIComponent(sticker.id) + '/moderate', 'POST', { status: 'published' }); await refresh(); status('Sticker restored.'); } catch (error) { status(error.message); } });
        row.append(label, button); removedList.append(row);
    }
}

try {
    const configResponse = await fetch('/admin-config');
    if (!configResponse.ok) throw new Error('Admin sign in is not configured.');
    const config = await configResponse.json();
    auth = getAuth(initializeApp(config));
    element('google').addEventListener('click', () => signInWithPopup(auth, new GoogleAuthProvider()).catch(error => status(error.message)));
    element('apple').addEventListener('click', () => signInWithPopup(auth, new OAuthProvider('apple.com')).catch(error => status(error.message)));
    element('signout').addEventListener('click', () => signOut(auth));
    element('refresh').addEventListener('click', () => refresh().catch(error => status(error.message)));
    onAuthStateChanged(auth, async user => {
        currentUser = user;
        element('signout').hidden = !user;
        element('google').hidden = !!user;
        element('apple').hidden = !!user;
        element('workspace').hidden = true;
        if (!user) { status('Sign in with an administrator account.'); return; }
        try {
            const claims = await getIdTokenResult(user, true);
            if (claims.claims.admin !== true) { status('This account does not have administrator access.'); return; }
            element('workspace').hidden = false;
            status('Signed in as an administrator.');
            await refresh();
        } catch (error) { status(error.message); }
    });
} catch (error) { status(error.message); }
`;
