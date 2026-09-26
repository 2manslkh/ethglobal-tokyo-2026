# Deployment

## Environment

- Project: `tagtag-tokyo-2026` (project number `329004805254`).
- API: https://tagtag-api-329004805254.asia-northeast1.run.app
- Region: Tokyo, `asia-northeast1`.
- Firestore: `(default)`, native mode, deny-all direct client rules.
- Private AR maps: `gs://tagtag-tokyo-2026-maps`, uniform bucket access and public-access prevention.
- Runtime account: `tagtag-api@tagtag-tokyo-2026.iam.gserviceaccount.com`.
- Apple and Google Firebase providers enabled. Public iOS client settings are in `Assets/Resources/Tagtag/ServiceConfiguration.json`.
- Apple signing team: `5Y6QUA9GA6`; bundle: `com.kenk.tagtag`.

## Access

Runtime permissions cover Firestore data, objects in the map bucket, signing its own short-lived map URLs, and the custom `tagtagAuthLookup` role containing only `firebaseauth.users.get`. It cannot assign admin claims or administratively delete Auth users. Account deletion uses the requesting user's own ID token. Admin claim assignment is an operator workstation action; see [backend instructions](../backend/README.md).

The operator page is `/admin`; its origin is authorized for Firebase sign-in. Sign-in does not confer admin access. An operator's Firebase UID still needs an explicit admin claim before moderation is available.

## Deploy

Use the existing project and region. Put public runtime configuration in a local JSON or YAML file containing `GOOGLE_CLOUD_PROJECT`, `TAGTAG_MAP_BUCKET`, `FIREBASE_API_KEY`, and `FIREBASE_AUTH_DOMAIN`. No downloaded service-account private key is required.

```sh
gcloud run deploy tagtag-api --source backend \
  --project=tagtag-tokyo-2026 --region=asia-northeast1 \
  --service-account=tagtag-api@tagtag-tokyo-2026.iam.gserviceaccount.com \
  --allow-unauthenticated --min-instances=0 --max-instances=1 \
  --concurrency=20 --memory=256Mi --cpu=1 --timeout=60 \
  --env-vars-file=/path/to/runtime-config.json
```

Cloud Run accepts public HTTP traffic for browsing; the application verifies Firebase ID tokens for protected operations. Update the `tagtag-cleanup` job to the new deployed image after changing cleanup code. Firebase rules and indexes are versioned at the repository root.

## Maintenance and cost

NFT minting is implemented but disabled in the committed app configuration. No NFT contract, hosted metadata, verified Thirdweb project configuration, or mint-worker deployment is recorded as live by this change. Follow [NFT setup](NFT_SETUP.md) and the [isolated staging setup](NFT_STAGING.md) before enabling it. The staging project `tagtag-nft-staging-2026` exists, but billing attachment hit the billing-enabled project quota; the user requested local completion with deployment pending. The HTTP service receives only public NFT configuration; only the separate worker identity receives the signer secret. Existing app collections continue to work with NFT flags off. Do not enable the feature until the real two-device mint and transfer-out checks pass.

The `tagtag-cleanup` Cloud Run job runs `node src/cleanup.js` with one task, 512 MiB, one CPU, a ten-minute timeout, and one retry. Cloud Scheduler `tagtag-cleanup-daily` invokes it with OAuth at 04:00 Asia/Tokyo. It removes abandoned uploads and expired discovery sessions and resumes account-data cleanup.

The API has zero minimum instances and one maximum instance. Per-instance request limits, a 100-publication daily user quota, 16 MiB maps, and bounded queries reduce accidental use. The publication quota counts newly prepared publications, including unfinished attempts, and resets at 00:00 UTC. Per-instance request limits reset on instance restart. These limits are not a guaranteed spending cap.

Billing uses SGD. On 2026-09-26, at the user's request, the project alert budget was changed from S$10 to **S$1/month**, retaining 10%, 50%, and 100% alerts to default billing recipients (S$0.10, S$0.50, and S$1). Budget ID: `4f874738-d088-4cab-bafe-a072c22e93e5`. Alerts do not shut down services or change the billing-enabled project quota blocking staging.

## Verified on 2026-09-26

- Cloud Build completed and API revision `tagtag-api-00001-btl` serves traffic.
- `GET /health` → 200; unauthenticated `GET /v1/collection` → 401.
- Anonymous nearby request with a fresh synthetic Tokyo coordinate → 200 with an empty list. No test stickers were published.
- Cleanup execution `tagtag-cleanup-fqg5g` completed successfully.
- Firebase emulator test passed against Auth, Firestore, and Storage; see [device verification](DEVICE_VERIFICATION.md) for app-level evidence and pending hardware checks.

## Collection location tolerance rollout

Revision `tagtag-api-00021-git` is serving 100% of API traffic. Collection now
accepts a fresh measured fix up to 5,000 m accuracy when its distance from the
sticker is within the measured accuracy plus 100 m, matching AR recovery.
Collection still requires a valid discovery ID. The response includes `isNew`
to remove a collection-list request from the client tap path. The backend suite
passed 84 tests with one emulator-only skip. At zero traffic and after promotion,
health returned 200, unauthenticated collection returned 401, and a fresh
5,000 m approximate nearby request returned 200. Authenticated live collection
was not exercised. Logs: `/tmp/tagtag-collect-accuracy-{deploy,promote}.log`.

## Explore accuracy rollout

On 2026-09-26, revision `tagtag-api-00005-sos` was built from the browse-location fix and checked at zero traffic before promotion to 100%. Nearby requests with fresh synthetic coordinates and 75, 2,000.149, and 5,000-metre accuracy returned 200. A 5,001-metre fix and a stale fix returned 400. Health returned 200 and unauthenticated collection returned 401. After promotion, the public app URL passed the 2,000.149/5,000-metre success and excessive/stale rejection checks again. No test stickers were published. Client regression and pending hardware checks are recorded in [Explore location verification](verification/explore-location.md).

## Publication map confirmation rollout

On 2026-09-26, backend commits `a72ce78` and `2ebf2cf` plus the final
legacy-operation lookup were deployed as `tagtag-api-00011-gut`. The tagged
revision passed health (200), unauthenticated collection/operation lookup (401),
and fresh approximate nearby (200) checks before promotion to 100% traffic.
The public app URL passed health, operation authentication and approximate
nearby checks after promotion. No synthetic production posts were created.
This API remains compatible with TestFlight build 1. The installed development
client is `90398c8`; TestFlight build 1 remains the earlier `01857d3` snapshot.

## Publication quota rollout

On 2026-09-26, commit `412c5c2` raised the per-user daily publication quota
from five to ten. Cloud Build `53e31721-5a5e-42d3-9b01-a87f9189ad7b`
succeeded and revision `tagtag-api-00013-bed` was promoted to 100% traffic
after health (200) and unauthenticated collection (401) checks. Both checks
passed again at the public app URL. The backend suite passed 79 tests with
one emulator-only test skipped; the quota test accepts ten publications and
rejects the eleventh. Existing app builds receive this server-side change.

On 2026-09-26, commit `3fff59f` raised the per-user daily publication quota
from ten to 100. The backend suite passed 84 tests with one emulator-only
test skipped. Revision `tagtag-api-00024-dub` passed zero-traffic health (200)
and unauthenticated collection (401) checks, then was promoted to 100% of
API traffic. Both checks passed again at the public URL. Existing app builds
receive this server-side change; no production publications were created
for verification.

On 2026-09-27, publication requests from the NFT candidate app were still
reaching revision `tagtag-api-00023-yoq` through its tagged URL, bypassing the
default 100% traffic assignment. Cloud Run logs showed 429 responses on
`/v1/publications/prepare` for that revision. The `nft-candidate` tag was
moved to `tagtag-api-00024-dub`, which has the same runtime environment and
the 100-publication quota. The tagged URL passed health (200) and anonymous
wallet (401) checks afterward. No app update is needed for clients already
using that tagged URL; an authenticated publication retry remains the final
user-level check.

## Twelve Taggi presets rollout

On 2026-09-26, backend commit `4437920` was deployed from an immutable source
snapshot. Cloud Build `fb5837a9-b73d-4e8e-b228-07fedde15955` succeeded and
revision `tagtag-api-taggi12-4437920` passed checks at zero traffic before
promotion to 100%. The previous production revision is `tagtag-api-00013-bed`.

The API accepts the exact preset IDs `taggi-1` through `taggi-12`; the added
presets map to generic NFT variant 0 if minting is enabled later. NFT minting
remains disabled. Existing runtime environment, service account, and resource
settings were preserved. The fresh backend suite passed 82 tests with no failures
and one emulator-only test skipped.

Candidate and public app URLs both passed seven checks: health (200), anonymous
collection/design listing/publication prepare (401), precise and 5,000-metre
nearby browsing (200), and 5,001-metre browsing rejection (400). No test content
was created. Authenticated publication of the new presets was tested locally;
these production smoke checks do not verify an authenticated publish journey.

The `tagtag-cleanup` job now uses the exact API image digest
`sha256:98b0864aebbb113b0d37ab9a1c93cf0ac2263345fa3aedd819d01f51200f7df9`.
Its command, environment and runtime identity are unchanged; no manual cleanup
execution was triggered. No error-severity logs were returned for the new API
revision during the post-promotion check.

## Approximate AR recovery — 2026-09-26

Deployed backend commit `d50cd93` (source archive from `d6653ce`) with Cloud
Build `b4c2b984-65c2-4ab6-8456-bc33656691d9`. Revision
`tagtag-api-00012-l95` uses image digest
`sha256:823d1e70045660b5b60e8b0c529505a0bd30a03da91a6a58ea1917479cd068a2`.
Traffic was explicitly promoted from `tagtag-api-taggi12-4437920` to 100% on
this revision; service readiness and the traffic allocation were verified.
Existing runtime configuration and NFT settings were preserved.

After promotion, the production app URL returned `200 {"ok":true}` for
`GET /health`, and unauthenticated recovery returned `401`. No test accounts or
stickers were created. Authenticated recovery on a physical device remains
pending; these probes verify readiness and the authentication boundary, not
visual relocalization. The local backend suite passed 84 tests with one emulator
test skipped, and all 242 Unity Edit Mode tests passed. See
[device verification](DEVICE_VERIFICATION.md#approximate-ar-recovery--2026-09-26).

Recovery now accepts a fresh fix with accuracy ≤5000 m and distance to the
sticker ≤measured accuracy + 100 m. Collection retains its 50 m accuracy and
100 m proximity checks. Signed iPhone integration and installation are owned
by the concurrent STICK coordinator. Rollback, if needed, targets the prior
`tagtag-api-taggi12-4437920` revision.

## 100 publications per day — 2026-09-26

Commit `df1f37f` increases the per-user UTC-day publication preparation limit
from 10 to 100. Revision `tagtag-api-quota100-df1f37f` was built from that
commit, promoted to 100% traffic, and verified Ready. After promotion,
`GET /health` returned 200 and unauthenticated `POST /v1/publications/prepare`
returned 401. No live test publications were created.

The backend suite passed 85 tests with one emulator test skipped. The quota
regression first failed on publication 11, then passed for 100 new prepares,
an idempotent retry at the limit, rejection of the 101st, and an independent
user allowance. Read-only GPT-6-Sol high review found no actionable issues.
Existing daily counts carry over; UTC reset and separate design quotas are
unchanged. No app rebuild is required. Prior revision `tagtag-api-00012-l95`
is available for rollback. Local logs: `/tmp/tagtag-quota100-deploy.log`,
`/tmp/tagtag-quota100-traffic.log`, and `/tmp/tagtag-quota100-green.log`.

## Thirteenth Taggi preset — 2026-09-27

Backend source at `eaf521a` (including preset commit `5037c61`) passed `npm test`
with 86 passed, one emulator-only skipped. Cloud Run source deployment created
revision `tagtag-api-00027-lih` with image digest
`sha256:e8c77467bbbe2c32ee5576ab4dcacb6a1a48316260ca2ea726d33c11620a62ce`.
It passed zero-traffic checks, then received 100% public traffic. The
`nft-candidate` tag now resolves to the same revision. Both `tagtag-cleanup`
and `tagtag-nft-mint` jobs use that exact image digest; their commands,
identities, environment variable names, and secret mappings were retained.
No manual job execution was triggered.

The public and `nft-candidate` URLs returned health 200, unauthenticated
collection 401, and excessive-location nearby rejection 400 after promotion.
The candidate also returned nearby 200 with a valid approximate fix before
promotion. These checks did not create or collect a sticker. Existing revision
`tagtag-api-00024-dub` remains available for rollback.
