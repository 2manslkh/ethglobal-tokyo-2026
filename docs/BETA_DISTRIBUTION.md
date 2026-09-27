# Beta distribution

## v0.2.0 (2) — 2026-09-27

Uploaded successfully to App Store Connect at 09:00 JST from release commit
`6522602`. Apple reported that the uploaded package was processing. Assignment
to the existing internal and external beta groups and any required external
review remain unverified: computer-use failed with `Sky Computer Use native
pipe startup failed`, including after reconnect and reset attempts.

Release changes include thirteen Taggi presets, map-confirmed publishing with
approximate GPS, updated AR recovery, a capture gate based on a validated spatial
map, and the Sepolia NFT souvenir integration. Backend support is recorded in
[deployment](DEPLOYMENT.md) and the [NFT rollout](NFT_TOKYO_ROLLOUT.md).

Verification: Unity Edit Mode **273/273 passed**; backend **86 passed, 1
emulator-only skipped**; separate ARKit preparation and Unity export succeeded;
Xcode Release archive and system-trust code-signature verification passed.
Fresh physical-device interaction testing was not performed for this release.

Archive: `Build/Beta/tagtag-0.2.0-2.xcarchive`. Logs:
`/tmp/tagtag-v020-{prepare,edit,export,archive,upload,backend-tests}.log`;
test results: `/tmp/tagtag-v020-edit.xml`. Upload retained the non-blocking
Vision Pro/ARKit compatibility and missing UnityRuntime dSYM warnings from the
previous release. Missing runtime symbols limit Unity runtime crash symbolication.

Before this upload, App Store Connect showed **0.1.0 (1) Approved** in the
`tagtag Public Beta` group, superseding its older Waiting for Review record below.
The public invitation remains https://testflight.apple.com/join/cUnAbdSY;
availability of v0.2.0 through that link is not yet confirmed.

## Build

- App: tagtag, bundle `com.kenk.tagtag`, Apple team `5Y6QUA9GA6`.
- [App Store Connect](https://appstoreconnect.apple.com/apps/6816329625/testflight).
- Uploaded on 2026-09-26: version **0.1.0 (1)**, source `01857d3ff3ac8e34a658f4fee21fb71b14abc399`.
- Archive: `Build/Beta/tagtag-0.1.0-1.xcarchive` (ignored local build artifact).
- Separate ARKit preparation, Unity export, signed Xcode Release archive, and App Store Connect upload succeeded.
- Unity Edit Mode: **198/198 passed**. Backend: **72 passed, 1 emulator-only skipped**.
- Physical-device interaction and two-phone shared AR recovery remain pending; compilation and upload do not establish those results.

Apple accepted the upload with non-blocking warnings about ARKit preventing Vision Pro compatibility and missing `UnityRuntime.framework` dSYM symbols. The latter limits symbolication of Unity runtime crash frames.

## Distribution status

The app record, internal `tagtag Team` group, and external `tagtag Public Beta` group exist. Build 0.1.0 (1) finished processing, its encryption declaration was completed, and it was submitted for external beta review on 2026-09-26. App Store Connect reports **Waiting for Review**. Reviewer instructions explain free Apple/Google sign-in and physical-device AR testing; no separate password login is claimed.

Public invitation link: **https://testflight.apple.com/join/cUnAbdSY**.

The link is created and open to anyone, but App Store Connect currently states: "Testers cannot join public link until this group has an approved build." Apple review is the remaining gate. The same link should be used after approval; the App Store Connect administration URL is not an installation link.

## Backend readiness

Revision `tagtag-api-00007-zix` was built from the same source snapshot, checked at zero traffic, then promoted to 100%. The `tagtag-cleanup` job uses the matching image. Existing runtime configuration and NFT-disabled settings were preserved.

The authored pagination index `CICAgJjF9oIK` is READY: `stickers` by `authorId` ascending, `status` ascending, `createdAt` descending, and `id` descending. A read-only query with a synthetic nonexistent author returned HTTP 200. No test account or sticker was created.

Both the candidate and public API returned HTTP 200 for `/health` and HTTP 401 for unauthenticated collection/authored requests. These checks establish service readiness and authentication enforcement, not full user acceptance.

## Local evidence

Logs: `/tmp/tagtag-beta-prepare.log`, `/tmp/tagtag-beta-export.log`, `/tmp/tagtag-beta-archive.log`, `/tmp/tagtag-beta-upload-retry.log`, `/tmp/tagtag-beta-backend-tests.log`, `/tmp/tagtag-beta-deploy.log`, `/tmp/tagtag-beta-promote.log`, and `/tmp/tagtag-beta-cleanup-update.log`. Unity test results: `/tmp/tagtag-beta-edit.xml`.

Later local edits from concurrent development are not included in this uploaded build. Export a fresh build with a higher build number to distribute them.
