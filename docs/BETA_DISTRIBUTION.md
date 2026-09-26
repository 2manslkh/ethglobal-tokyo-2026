# Beta distribution

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

The app record and internal `tagtag Team` group exist. The upload is processing; external beta review and the public invitation link are not yet complete. Do not share the App Store Connect administration URL as an installation link.

## Backend readiness

Revision `tagtag-api-00007-zix` was built from the same source snapshot, checked at zero traffic, then promoted to 100%. The `tagtag-cleanup` job uses the matching image. Existing runtime configuration and NFT-disabled settings were preserved.

The authored pagination index `CICAgJjF9oIK` is READY: `stickers` by `authorId` ascending, `status` ascending, `createdAt` descending, and `id` descending. A read-only query with a synthetic nonexistent author returned HTTP 200. No test account or sticker was created.

Both the candidate and public API returned HTTP 200 for `/health` and HTTP 401 for unauthenticated collection/authored requests. These checks establish service readiness and authentication enforcement, not full user acceptance.

## Local evidence

Logs: `/tmp/tagtag-beta-prepare.log`, `/tmp/tagtag-beta-export.log`, `/tmp/tagtag-beta-archive.log`, `/tmp/tagtag-beta-upload-retry.log`, `/tmp/tagtag-beta-backend-tests.log`, `/tmp/tagtag-beta-deploy.log`, `/tmp/tagtag-beta-promote.log`, and `/tmp/tagtag-beta-cleanup-update.log`. Unity test results: `/tmp/tagtag-beta-edit.xml`.

Later local edits from concurrent development are not included in this uploaded build. Export a fresh build with a higher build number to distribute them.
