import { randomUUID } from 'node:crypto';
import { pathToFileURL } from 'node:url';

const LEASE_SECONDS = 300;
const RETRY_SECONDS = 300;
const deletionFinal = account => account?.deleted && account.authDeleted !== false;

function jobEligibility(collector, author, collection, sticker) {
    if ([collector, author].some(account => account?.deleted && account.authDeleted === false)) return 'defer';
    if (deletionFinal(collector) || deletionFinal(author) || !collection || !sticker ||
        ['removed', 'deleted'].includes(sticker.status)) return 'cancel';
    return 'ready';
}

async function eligibility(adapter, job) {
    const [collector, author, collection, sticker] = await Promise.all([
        adapter.get('accounts', job.userId), adapter.get('accounts', job.authorId),
        adapter.get('collections', job.id), adapter.get('stickers', job.stickerId)
    ]);
    return jobEligibility(collector, author, collection, sticker);
}

async function dueJobs(adapter, now) {
    const groups = await Promise.all(['signed', 'submitted', 'delayed', 'queued', 'waiting'].map(state =>
        adapter.query('nftMints', [['state', '==', state], ['nextAttemptAt', '<=', now]], 200)));
    const jobs = groups.flat();
    return jobs.filter(job => job.rawTransaction).concat(jobs.filter(job => !job.rawTransaction))
        .sort((a, b) => (a.rawTransaction ? 0 : 1) - (b.rawTransaction ? 0 : 1) || a.createdAt - b.createdAt);
}

export async function runMintWorker({ adapter, chain, now = () => Math.floor(Date.now() / 1000),
    workerId = randomUUID(), maxJobs = 50 }) {
    if (!adapter || !chain) throw new Error('Mint worker requires Firestore and chain adapters');
    if (await chain.chainId() !== 11155111) throw new Error('Mint worker accepts Sepolia only');
    const signerKey = `sepolia:${chain.signerAddress().toLowerCase()}`;
    const targetContractAddress = chain.targetContractAddress().toLowerCase();
    const observedNonce = await chain.pendingNonce();
    const acquired = await adapter.transaction(async tx => {
        const current = await tx.get('mintSigner', signerKey);
        if (current?.leaseUntil > now() && current.owner !== workerId) return false;
        await tx.set('mintSigner', signerKey, { id: signerKey, owner: workerId,
            leaseUntil: now() + LEASE_SECONDS, nextNonce: Math.max(current?.nextNonce ?? 0, observedNonce) });
        return true;
    });
    if (!acquired) return { acquired: false, processed: 0 };
    let processed = 0;
    const fencedUpdate = (job, change) => adapter.transaction(async tx => {
        const [lease, current] = await Promise.all([tx.get('mintSigner', signerKey), tx.get('nftMints', job.id)]);
        if (lease?.owner !== workerId || lease.leaseUntil <= now() || !current ||
            current.rawTransaction !== job.rawTransaction || ['confirmed', 'cancelled'].includes(current.state)) return false;
        await tx.set('nftMints', job.id, { ...current, ...change });
        return true;
    });
    try {
        for (const listed of (await dueJobs(adapter, now())).slice(0, maxJobs)) {
            const renewed = await adapter.transaction(async tx => {
                const lease = await tx.get('mintSigner', signerKey);
                if (lease?.owner !== workerId || lease.leaseUntil <= now()) return false;
                await tx.set('mintSigner', signerKey, { ...lease, leaseUntil: now() + LEASE_SECONDS });
                return true;
            });
            if (!renewed) break;
            let job = await adapter.get('nftMints', listed.id);
            if (!job || !['waiting', 'queued', 'delayed', 'signed', 'submitted'].includes(job.state) || job.nextAttemptAt > now()) continue;
            if (job.chainId !== 11155111 || job.contractAddress?.toLowerCase() !== targetContractAddress) {
                await fencedUpdate(job, { ...(job.state === 'waiting' ? {} : { state: 'delayed' }),
                    nextAttemptAt: now() + RETRY_SECONDS, lastError: 'target_mismatch' });
                processed++;
                continue;
            }
            if (job.state === 'waiting') {
                const state = await eligibility(adapter, job);
                if (state === 'cancel') await fencedUpdate(job, { state: 'cancelled', cancelledAt: now() });
                if (state === 'defer') await fencedUpdate(job, { nextAttemptAt: now() + 30 });
                if (state !== 'ready') { processed++; continue; }
                const wallet = await adapter.get('wallets', job.userId);
                if (!wallet) {
                    await fencedUpdate(job, { nextAttemptAt: now() + RETRY_SECONDS });
                    processed++;
                    continue;
                }
                if (!await fencedUpdate(job, { state: 'queued', recipient: wallet.address, nextAttemptAt: now() })) continue;
                job = await adapter.get('nftMints', job.id);
            }
            if (job.rawTransaction) {
                try {
                    const receipt = await chain.receipt(job.transactionHash);
                    const canonical = receipt?.blockHash ? await chain.blockHash(receipt.blockNumber) === receipt.blockHash : true;
                    if (receipt && canonical) {
                        if (receipt.blockNumber <= await chain.finalizedBlockNumber()) {
                            if (receipt.status === 'success') {
                                if (!await chain.tokenExists(job.tokenId)) throw new Error('Finalized mint token missing');
                                await fencedUpdate(job, { state: 'confirmed', confirmedAt: now(), lastError: '' });
                            } else {
                                await fencedUpdate(job, { state: 'delayed', rawTransaction: '',
                                    nextAttemptAt: now() + RETRY_SECONDS, lastError: 'transaction_reverted' });
                            }
                        } else {
                            await fencedUpdate(job, { state: 'submitted', nextAttemptAt: now() + 30, lastError: '' });
                        }
                    } else {
                        await chain.broadcast(job.rawTransaction);
                        await fencedUpdate(job, { state: 'submitted', nextAttemptAt: now() + 30 });
                    }
                } catch {
                    await fencedUpdate(job, { state: 'delayed', nextAttemptAt: now() + RETRY_SECONDS,
                        lastError: 'reconciliation_failed' });
                }
                processed++;
                continue;
            }
            const state = await eligibility(adapter, job);
            if (state !== 'ready') {
                if (state === 'cancel') await fencedUpdate(job, { state: 'cancelled', cancelledAt: now() });
                else await fencedUpdate(job, { nextAttemptAt: now() + 30 });
                processed++;
                continue;
            }
            try {
                const signer = await adapter.get('mintSigner', signerKey);
                if (signer.owner !== workerId || signer.leaseUntil <= now()) break;
                const nonce = Math.max(signer.nextNonce ?? 0, await chain.pendingNonce());
                const signed = await chain.prepareMint({ recipient: job.recipient, tokenId: job.tokenId, preset: job.preset, nonce });
                const stored = await adapter.transaction(async tx => {
                    const [lease, current, collector, author, collection, sticker] = await Promise.all([
                        tx.get('mintSigner', signerKey), tx.get('nftMints', job.id),
                        tx.get('accounts', job.userId), tx.get('accounts', job.authorId),
                        tx.get('collections', job.id), tx.get('stickers', job.stickerId)
                    ]);
                    if (lease.owner !== workerId || lease.leaseUntil <= now() || current.rawTransaction ||
                        !['queued', 'delayed'].includes(current.state)) return false;
                    const state = jobEligibility(collector, author, collection, sticker);
                    if (state === 'defer') return false;
                    if (state === 'cancel') {
                        await tx.set('nftMints', job.id, { ...current, state: 'cancelled', cancelledAt: now() });
                        return false;
                    }
                    await tx.set('nftMints', job.id, { ...current, state: 'signed', rawTransaction: signed.raw,
                        transactionHash: signed.hash, nonce, attempts: current.attempts + 1, nextAttemptAt: now() });
                    await tx.set('mintSigner', signerKey, { ...lease, nextNonce: nonce + 1 });
                    return true;
                });
                if (stored) {
                    const signedJob = { ...job, rawTransaction: signed.raw };
                    try {
                        await chain.broadcast(signed.raw);
                        await fencedUpdate(signedJob, { state: 'submitted', nextAttemptAt: now() + 30 });
                    } catch {
                        await fencedUpdate(signedJob, { state: 'delayed', nextAttemptAt: now() + RETRY_SECONDS,
                            lastError: 'broadcast_ambiguous' });
                    }
                }
            } catch {
                await fencedUpdate(job, { state: 'delayed', nextAttemptAt: now() + RETRY_SECONDS,
                    lastError: 'signing_failed' });
            }
            processed++;
        }
    } finally {
        await adapter.transaction(async tx => {
            const lease = await tx.get('mintSigner', signerKey);
            if (lease?.owner === workerId) await tx.set('mintSigner', signerKey, { ...lease, leaseUntil: now() });
        });
    }
    return { acquired: true, processed };
}

if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) {
    try {
        const { createFirebaseAdapter } = await import('./firebase-adapter.js');
        const { createMintChain } = await import('./mint-chain.js');
        const { mintConfig } = await import('./nft-config.js');
        const config = mintConfig(process.env);
        if (!config.enabled) throw new Error('NFT minting is disabled');
        const result = await runMintWorker({ adapter: createFirebaseAdapter(), chain: createMintChain(config) });
        console.log(JSON.stringify(result));
    } catch {
        console.error('Mint worker failed');
        process.exitCode = 1;
    }
}
