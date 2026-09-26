# tagtag API

Node 22+ service for the shared sticker map. Cloud Run can build the included Node 24 Dockerfile. The service starts only when `GOOGLE_CLOUD_PROJECT`, `TAGTAG_MAP_BUCKET`, and `FIREBASE_API_KEY` are set. Firebase Authentication, Firestore, and private Cloud Storage use application default credentials. `FIREBASE_API_KEY` is the public Firebase web API key used by the operator sign-in page and the requesting user's own `accounts:delete` call. `FIREBASE_AUTH_DOMAIN` is optional and defaults to `<PROJECT_ID>.firebaseapp.com`. Do not put service account keys or user tokens in the repository.

## Local verification

```sh
cd backend
npm ci
npm test
cd ..
npx --yes firebase-tools@14.21.0 emulators:exec --only auth,firestore,storage --project demo-tagtag 'cd backend && npm run test:emulator'
```

`npm test` uses an injected adapter and real HTTP listener. The emulator command uses Firebase Auth tokens and Firestore transactions with the production adapter. The emulator substitutes only the signed Storage transport, which the Storage emulator does not implement as Google Cloud Storage V4 URLs. The API tests cover authorization, note isolation, input bounds, retries, races, quotas, blocks, moderation, deletion, map URL gates, and custom design storage and visibility.

The Storage emulator verifies design object writes, thumbnail reads, and account cleanup. It does not enforce Cloud Storage's `ifGenerationMatch: 0` conditional write in an overwrite probe, so it cannot verify that production-only storage guarantee. The HTTP tests exercise concurrent finalize requests with changed upload bytes and assert that the saved artwork stays immutable. Verify the conditional write against a private Cloud Storage test bucket before live deployment.

NFT tests also exercise EIP-191 wallet binding, the atomic collection outbox, the durable mint lease/nonce, finalized receipt reconciliation, and real viem transaction serialization. The emulator test covers Firestore wallet assignment and signing persistence when the Firebase emulators are available.

## Deploy

Deploy `firebase.json`, `firestore.rules`, `storage.rules`, and `firestore.indexes.json` from the repository root to the dedicated Firebase project. The direct client rules deny all reads and writes; the API uses IAM. The map bucket must have uniform bucket-level access and no public principal grant.

```sh
firebase deploy --only firestore:rules,firestore:indexes,storage --project <PROJECT_ID>
gcloud run deploy tagtag-api --source backend --project <PROJECT_ID> --region asia-northeast1 \
  --service-account <RUNTIME_SERVICE_ACCOUNT> --min-instances 0 --max-instances 1 \
  --allow-unauthenticated \
  --set-env-vars GOOGLE_CLOUD_PROJECT=<PROJECT_ID>,TAGTAG_MAP_BUCKET=<PRIVATE_MAP_BUCKET>,FIREBASE_API_KEY=<PUBLIC_FIREBASE_API_KEY>
```

Explore accepts fresh location fixes up to 5,000 metres accuracy for `/v1/nearby`, with its 2 km search radius unchanged. AR recovery also accepts accuracy up to 5,000 metres and permits distance to the sticker up to measured accuracy plus 100 metres. Collection retains 50-metre accuracy and 100-metre proximity; automatic publication retains 100-metre accuracy. All fixes retain the 30-second freshness limit. Deploy the backend before distributing the updated client.

The Cloud Run service accepts anonymous `/v1/nearby` and `/health`; every other `/v1` route verifies a Firebase ID token, including revocation. Grant the runtime service account `roles/datastore.user` on the project, `roles/storage.objectAdmin` on the private map bucket, `firebaseauth.users.get` through a custom project role, and `iam.serviceAccounts.signBlob` on itself for V4 signed URLs. A broader self-scoped `roles/iam.serviceAccountTokenCreator` also provides signing. It does not need Firebase Auth admin or user deletion permission. Enable the Identity Toolkit and IAM Service Account Credentials APIs.

Run cleanup as a Cloud Run job using the same image, service account, and environment variables. Set command to `node` and arguments to `src/cleanup.js`; schedule it at least hourly with Cloud Scheduler and a service account permitted to run that job. The worker removes pending publications older than one hour, abandons unfinished design uploads after one hour, sweeps unreferenced design assets, removes expired discovery sessions, and processes account tombstones whose Firebase Auth user no longer exists. The asset sweep also recovers from a finalize process that stops during account or design deletion. If the user has just deleted Auth but cleanup is unavailable, `DELETE /v1/account` returns `{ok:true,cleanupPending:true}` and the tombstone hides their stickers and notes immediately. An Auth token requiring recent login returns `401 recent_login_required` before any content is removed.

### Design deployment verification — 2026-09-26

In `tagtag-tokyo-2026` (`asia-northeast1`), API revision `tagtag-api-00003-75l` is deployed, both `designs(status, createdAt)` and `designs(ownerId, status)` indexes are `READY`, and the `tagtag-cleanup` job uses the exact API image. NFT minting remains disabled. The fresh backend suite passed 68 tests with one emulator test skipped.

Unauthenticated `GET /v1/designs` returned `401`, confirming the authentication boundary. The dedicated synthetic-user smoke could not reach authenticated listing or upload: IAM `signJwt` returned `403` before Firebase sign-in, so no test account or content was created. Authenticated listing, design prepare/upload/finalize, and unpublished publication prepare remain unverified in production. Re-run that smoke with an authorized test-signing identity or another dedicated test credential; do not treat the unauthenticated `401` as proof of the design route or change IAM solely to bypass this check.

## Sepolia souvenir minting

### Isolated NFT staging

The local staging deployment planner is [infra.py](../scripts/nft-staging/infra.py), with a [configuration example](../config/nft-staging/staging.example.json). It is pinned to Google Cloud/Firebase project `tagtag-nft-staging-2026` (number `542095619867`) in `asia-northeast1`. It uses a separate private map bucket, Firestore database, Firebase Auth users, API service, cleanup job, and mint job. The API service is named `tagtag-api` in that project; its wallet domain is `tagtag-api-542095619867.asia-northeast1.run.app`. The script never targets the production project. The staging HTTP service has no signer or RPC secret mapping.

**Current blocker (2026-09-26):** the staging project exists, but Cloud Billing attachment failed with `CLOUD BILLING QUOTA EXCEEDED`; Firebase CLI login is unavailable. No staging API, map bucket, Auth setup, Firestore deployment, cleanup job, mint worker, scheduler, or signer secret should be inferred from the local plan. The coordinator will deploy only after billing and login are resolved. The public Thirdweb client ID `639af3b16477c0bb4b73b8e163eb0397` still needs provider setup by the user; it is not a signer credential.

Copy the example JSON to a private local file and replace `firebase_api_key` with the **staging** Firebase web API key. Keep `nft_enabled` false for initial setup. Run from the repository root:

```sh
python3 -m unittest discover -s scripts/nft-staging -p 'test_infra.py' -v
python3 scripts/nft-staging/infra.py validate --config /path/to/staging.json
python3 scripts/nft-staging/infra.py plan --config /path/to/staging.json
python3 scripts/nft-staging/infra.py preflight --config /path/to/staging.json
```

`validate` and `plan` are local only. They reject the committed production Firebase API key without printing it. The rendered plan refers to `${TAGTAG_STAGING_FIREBASE_API_KEY}` instead of showing the configured key; `apply` supplies that variable to the plan process. `preflight` makes read-only project, billing, Firebase, API key ownership, and relevant worker checks. It stops if billing is disabled. The API Keys and Cloud Run APIs must already be enabled for these read-only checks; after billing is attached, the coordinator can enable them explicitly with `gcloud services enable apikeys.googleapis.com run.googleapis.com --project=tagtag-nft-staging-2026`. The tool does not enable APIs during preflight. The deploy path requires an explicit confirmation and is reserved for the coordinator after the blocker is resolved:

```sh
python3 scripts/nft-staging/infra.py apply --config /path/to/staging.json \
  --confirm-project tagtag-nft-staging-2026
```

The plan creates resources if missing, reuses staging resources on repeat runs, deploys the repository's Firestore rules and indexes, builds the backend image once, and deploys that image to the API and both jobs. Cleanup runs hourly. Mint scheduling is configured for every minute with one task and parallelism one, but remains paused while `nft_enabled` is false. The API and job both receive `NFT_ENABLED=false` initially. Set up Firebase Auth providers, the staging app/API key restrictions, and the Firebase project association before deploy. Preflight looks up the configured Firebase key and requires its parent to be `projects/542095619867/locations/global`; lookup failure or another parent stops deployment without printing the key. The private map bucket uses uniform bucket access and public access prevention; signed URLs remain under the API identity.

The API and cleanup identities get Firestore access, a narrow Firebase Auth user lookup role, and map object access. API signed URLs use its self-scoped service account token creator grant. The current mint worker reads Firestore and does **not** call `adapter.userExists`; it gets Firestore access plus secret access on the two worker secrets only when enabled. The signer secret policy must have no other direct accessor and no project-wide accessor binding. The deployment script never creates a signer key, reads secret payloads, or includes a signer key on the API. Review inherited IAM grants separately before live minting.

To enable minting after the contract, RPC, signer, and provider are ready, set `nft_enabled=true`, add the Sepolia contract address, the exact staging API wallet domain above, and pinned numeric versions of the pre-existing `tagtag-staging-sepolia-rpc-url` and `tagtag-staging-sepolia-signer` Secret Manager secrets. `plan` rejects incomplete or other-project settings. The enabled `preflight` checks those versions and the signer policy, then verifies the configured domain against the deployed API URL. The coordinator must check pending jobs, signer funding, contract identity, wallet issuer/audience, and physical-device acceptance before enabling the app flag. The staging Thirdweb JWT issuer and audience are based on `tagtag-nft-staging-2026`.

If new NFT enqueueing must stop after minting was enabled, **leave the mint worker and its minute scheduler running** so signed jobs continue reconciling. Use the API-only command below; it changes `NFT_ENABLED` on the API service and preserves the worker configuration:

```sh
python3 scripts/nft-staging/infra.py plan-pause-enqueue --config /path/to/staging.json
python3 scripts/nft-staging/infra.py pause-enqueue --config /path/to/staging.json \
  --confirm-project tagtag-nft-staging-2026
```

A full disabled deploy clears worker secret mappings and pauses its scheduler. It is for initial setup or an already-disabled worker only; `preflight` refuses it when the existing mint job is enabled. Do not use it as a rollback for pending, signed, or submitted jobs. The tool does not inspect Firestore queue contents, so an operator must verify queue state before making any manual worker change.

Minting is off by default. Configure the API with `NFT_ENABLED=true`, `NFT_CHAIN_ID=11155111`, `NFT_CONTRACT_ADDRESS=<deployed TagtagSouvenir>`, and `NFT_WALLET_DOMAIN=<API domain>`. The API needs no signer key or RPC URL. `GET /v1/wallet` returns the bound address or an empty address; challenge and bind require Firebase authentication. The five-minute EIP-191 challenge is single use. A UID can bind once, and an address cannot be reused by another UID. Account cleanup removes the wallet record and challenges while retaining an address tombstone that prevents reuse.

Each first collection writes one `nftMints` outbox document in the same Firestore transaction. The job holds a random 256-bit token ID represented as a decimal string, generic Taggi preset index, and immutable recipient once a wallet is bound. Collections made before binding wait and are assigned on bind; the worker also resumes waiting jobs if the API stops after saving a binding. The collection API exposes `nft` with `pending`, `confirmed`, `delayed`, or `cancelled` status. Old collections have no `nft`. Notes, map data, locations, Firebase IDs, and other private content never enter mint calldata or token metadata; the contract receives only recipient, token ID, and preset. The `nftMints` collection and signed raw transactions are server-only under the existing deny-all client rules.

Publication accepts the exact built-in IDs `taggi-1` through `taggi-12` and preserves each ID in publication and collection responses. The existing souvenir contract has four variants: `taggi-1` through `taggi-4` map to NFT presets 0 through 3 respectively. `taggi-5` through `taggi-12` and custom designs mint the generic NFT preset 0. No new contract metadata variant is required for these added app presets.

Run `npm run mint:worker` as a separate scheduled Cloud Run job using the same image and Firestore service account. Give **only that job** `NFT_RPC_URL=<HTTPS Sepolia RPC>` and `NFT_SIGNER_PRIVATE_KEY` through Secret Manager; do not put the key on the HTTP service or in source control. Set the four public NFT variables on the worker too. Schedule the job every minute. The worker keys its Firestore lease and next-nonce record by chain and signer address, persists each signed transaction before broadcast, and retries ambiguous broadcasts using the same bytes. It checks the deployed contract's identity before signing, and confirms only after a canonical finalized receipt and a finalized `ownerOf` read. An unsigned job is cancelled when its collector or author is deleted or its sticker is removed; a signed job keeps reconciling. An account deletion still awaiting Firebase Auth confirmation pauses unsigned work. Insufficient signer balance and RPC failures surface as delayed jobs without blocking collection. Monitor delayed job count, oldest pending age, and signer Sepolia ETH balance. Keep the feature disabled until contract address, metadata, signer funding, wallet provider, and device smoke test are ready.

The operator page is `/admin`. Configure Google and/or Apple as Firebase Auth providers and authorize the deployed origin. The page reads reports and submits moderation actions through admin-claim protected API routes. Grant or remove claims only from an operator workstation with privileged credentials:

```sh
cd backend
GOOGLE_CLOUD_PROJECT=<PROJECT_ID> node operator/set-admin.js <FIREBASE_UID> true
```

Refresh the operator's ID token after changing claims. There is no runtime HTTP claim-granting route.

## Upload and data

`prepare` returns a five-minute signed PUT URL for `pending/<id>` and exactly two required headers: `Content-Type: application/octet-stream` and `x-goog-content-length-range: 1,16777216`. Send the raw binary map with both headers. Cloud Storage enforces the header's 16 MiB bound. `finalize` checks the exact declared byte length and content type, then copies the specific uploaded generation into private `maps/<id>` before publishing. Recovery issues a five-minute signed GET URL only after token, publication, block, and 100 m checks. The AR tap remains a client gameplay gate, not proof of physical presence.

Firestore collections:

| Collection | Contents |
| --- | --- |
| `stickers` | Pending/published/withdrawn/removed sticker, private note and placement fields |
| `collections` | User and sticker IDs with collection time; note resolved on each sync |
| `discoveries` | User-bound, sticker-bound, five-minute sessions |
| `blocks` | User/author pairs |
| `reports` | One report per reporter/sticker with moderation state |
| `quotas` | Per-user UTC-day publication counts |
| `accounts` | Deletion tombstones and cleanup state |
| `wallets`, `walletAddresses`, `walletChallenges` | Immutable wallet binding, address reservation, expiring ownership proofs |
| `nftMints`, `mintSigner` | Private mint outbox, signed transactions, and durable signer lease/nonce |
| `designs` | Private owner, immutable operation, decoded dimensions, lifecycle and publication references |
| `designQuotas` | Per-user UTC-day design creation counts |
| `designCounts` | Per-user active design count |

The nearby response maps only public summary fields. Publication operation IDs and collection IDs are deterministic per user, so retries do not duplicate them. A retry can use a fresh location within 100 m of its first placement; the original coordinate stays fixed. Text bounds are in the shared API contract. The built-in filter rejects configured spam or abuse phrases; add comma-separated phrases with `TAGTAG_BLOCKED_PHRASES` at deployment.

### Custom designs

`POST /v1/designs/prepare` accepts `{operationId,name,kind,imageBytes,width,height}` with `kind` `image`, `ai`, or `polaroid`. It returns `{id,uploadUrl,uploadHeaders}`. Upload the final PNG as a raw PUT with exactly the returned headers, then call `POST /v1/designs/:id/finalize` to receive `{design}`. `GET /v1/designs` returns the signed-in owner's ready designs as `{items}`; `DELETE /v1/designs/:id` archives the library entry. Prepare and finalize retries preserve the operation's identity and final artwork. An abandoned upload's operation ID stays reserved.

If prepare retries an archived or abandoned design with the same payload, the API returns `409 design_expired`. A changed payload with the same operation ID remains `409 conflict`. Clients may use `design_expired` to retry retained pixels under a fresh operation ID.

The PNG must be at most 5 MiB with a longest edge of 1024 pixels. Finalize checks the stored byte count and MIME type, decodes the image with a one-megapixel limit, checks the declared dimensions, strips metadata by re-encoding, and stores a separate thumbnail at most 256 pixels on its longest edge. The server uses decoded dimensions for publication. Each user can prepare 20 designs per UTC day and hold 100 active designs. The API stores final artwork and thumbnails in private `designs/<id>/` objects and returns five-minute signed read URLs only with a permitted design or visible publication/collection response. A previously issued URL stays valid until its five-minute expiry. Withdrawal keeps collected access; moderation removal suppresses collection artwork URLs. Archived assets remain while any publication references them. Account deletion removes all owned designs and assets.

Publication prepare accepts exactly one `presetId` or `designId`; the latter must name a ready design owned by the publishing account. Design summaries add `designId`, `artworkWidth`, `artworkHeight`, `artworkUrl`, and `thumbnailUrl`; preset summaries retain their existing fields. Deploy Firestore composite indexes for `designs(status, createdAt)` and `designs(ownerId, status)` with the backend before enabling client uploads.

One instance limits all `/v1` calls to 120/min per observed IP, nearby to 30/min per IP, recover to 20/min per user, reports to 10/hour per user, and collection/authored reads to 60/min per user. Responses use `429 rate_limited`. These in-memory limits reset on restart and are not a global cost cap. Keep max instances low, set budget alerts, and monitor Firestore reads and Storage egress.

### Map-confirmed publishing

Clients can send `locationConfirmed: true` and a separate `confirmedLocation`
pin on publication prepare and finalize. The API accepts a fresh measured fix
up to 5 km accuracy only when the confirmed pin lies inside its uncertainty
radius plus 100 m. It stores the pin and private measurement provenance
separately, preserves that pin across retries, and keeps discovery/collection
gates unchanged. Deploy this backward-compatible API before distributing the
map-confirmation client. See the [contract](../docs/plans/tagtag-api-contract.md).
