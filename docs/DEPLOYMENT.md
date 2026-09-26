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

NFT minting is implemented but disabled in the committed app configuration. No NFT contract, pinned metadata, Thirdweb project configuration, or mint-worker deployment is recorded as live by this change. Follow [NFT setup](NFT_SETUP.md) before enabling it. The HTTP service receives only public NFT configuration; only the separate worker identity receives the signer secret. Existing app collections continue to work with NFT flags off. Do not enable the feature until the real two-device mint and transfer-out checks pass.

The `tagtag-cleanup` Cloud Run job runs `node src/cleanup.js` with one task, 512 MiB, one CPU, a ten-minute timeout, and one retry. Cloud Scheduler `tagtag-cleanup-daily` invokes it with OAuth at 04:00 Asia/Tokyo. It removes abandoned uploads and expired discovery sessions and resumes account-data cleanup.

The API has zero minimum instances and one maximum instance. Per-instance request limits, a five-publication daily user quota, 16 MiB maps, and bounded queries reduce accidental use. Limits reset on instance restart and are not a guaranteed spending cap.

Billing uses SGD. The project budget is S$10/month with 10%, 50%, and 100% alerts to default billing recipients. Budget ID: `4f874738-d088-4cab-bafe-a072c22e93e5`. Alerts do not shut down services.

## Verified on 2026-09-26

- Cloud Build completed and API revision `tagtag-api-00001-btl` serves traffic.
- `GET /health` → 200; unauthenticated `GET /v1/collection` → 401.
- Anonymous nearby request with a fresh synthetic Tokyo coordinate → 200 with an empty list. No test stickers were published.
- Cleanup execution `tagtag-cleanup-fqg5g` completed successfully.
- Firebase emulator test passed against Auth, Firestore, and Storage; see [device verification](DEVICE_VERIFICATION.md) for app-level evidence and pending hardware checks.

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
