# Sticker creation execution

Approved: native Apple Image Playground only; Photos/Files imports; optional foreground cutout with white border; Polaroids from library or front/rear camera with crop and optional 40-character caption. Reusable account-synced My Stickers. Only creator may publish a design. Existing presets remain compatible. iOS 15 minimum with availability gates.

## Ownership and execution

Shared worktree `/private/tmp/tagtag-sticker-creation`, branch `feat/sticker-creation`, Herdr tab Sticker creation. Preserve unrelated main-checkout AR work. All implementers GPT-6-Sol/high. Coordinator owns Core, Services, AR integration and builds; backend agent owns backend/**; native agent owns new Assets/Plugins/iOS/TagtagStickerCreation* and Assets/Editor/BuildIos.cs; UI agent owns Assets/Tagtag/UI/**. Git writes serialized by coordinator permission. No agent runs Unity or changes other owned paths.

## Contract

Design JSON: `{id, ownerId, name, kind, width, height, revision, createdAt, artworkUrl, thumbnailUrl}`. `kind` is image, ai, or polaroid. Signed URLs are temporary, not identity. Publication/draft/summary add `designId`, `artworkWidth`, `artworkHeight`, `artworkUrl`, `thumbnailUrl`; presets keep presetId. Exactly one presetId/designId on publication prepare. Design dimensions are server-authoritative.

Backend: GET /v1/designs => {items}; POST /v1/designs/prepare body {operationId,name,kind,imageBytes,width,height} => {id,uploadUrl,uploadHeaders}; POST /v1/designs/:id/finalize => {design}; DELETE /v1/designs/:id. Upload a single PNG <=5MiB and <=1024 longest edge. Server validates decoded image and creates 256px thumbnail. 20 creations/day, 100 active/user. Signed image reads follow publication/collection visibility; unfinished designs private. Immutable assets retained while publications reference them. Removal only archives library entry. Account deletion and abandoned uploads clean assets. Idempotent prepare/finalize. Existing map uploads unchanged.

Native C exports: `TagtagStickerCreationAvailable()` int bit flags (1 import, 2 camera, 4 AI, 8 cutout); `TagtagStickerCreationOpen(const char* source,const char* receiver,const char* callback)` source import/ai/polaroid; `TagtagStickerCreationCancel()`. Unity callback JSON `{status,path,name,kind,width,height,error}` where status success/cancelled/unavailable/error. Path points to finalized PNG in app storage. Native owns complete source/crop/cutout/frame/caption editor; only final PNG leaves bridge. AI interface via Image Playground, imports Photos/Files, front/rear capture for Polaroid. Normalize orientation/remove metadata/alpha PNG max1024. Pause AR before presentation; caller restores afterward.

Unity interfaces: add `StickerDesign` DTO above; AppState.designs list, creationOpen bool, designsLoading bool, selectedDesign string; controller methods `OpenCreation()`, `CloseCreation()`, `CreateSticker(string source)`, `RefreshDesigns()`, `SelectDesign(string id)`, `DeleteDesign(string id)`. View supplies My Stickers creation sheet with source buttons/library and pending-save status. Coordinator adds controller native creation orchestration, sign-in draft retention and retry, backend save, account-scoped persistence/cache. Art helper accepts optional designId/URLs, dimensions; shared `StickerArtwork` service provides asynchronous texture lookup. Native editor supplies actual image adjustments; Unity UI handles source choice/library only.

## Acceptance

Behavior tests for ownership, immutable retries, image validation/quotas/cleanup; Unity selection/persistence/reference compatibility; native compile; graphics tests for alpha/aspect ratio; unsigned iOS build. Device-only capture, AI, two-phone recovery and permission flows recorded as pending unless physically observed. Deploy additive backend first, then client. Document configuration and verification. No cloud generation or source photo upload.

## Progress

- Native editor: 86de8c9, 5552aa5, 9d208b1. Objective-C++ syntax/static analyzer and Swift iOS 15 typecheck passed.
- UI: ca6ffdb, 1d72e50 plus startup geometry fix. Captured and corrected long-sheet overflow, initial NaN layout dimensions, and selection label layout. The sheet header and bottom bounds are asserted in the mounted graphics test.
- Backend: e34945a, c159d5d. Coordinator rerun: 38 tests pass, 1 emulator-only skipped. Agent Firebase emulator run: 1/1 pass. Storage emulator cannot prove GCS conditional-write enforcement; production adapter uses generation preconditions.
- Client integration: custom artwork, account-scoped library/upload drafts (including queued guest claims), URL renewal/cache invalidation, native suspension, AR aspect ratio and map thumbnails implemented.
- Review fixes: expired uploads restart only on design_expired; restored authorized artwork clears local revocation; replacement drafts save before prior publication retry is cleared; inactive camera creation does not retain resume state; thumbnail fallback does not overwrite full image cache.
- EditMode: final 134/134 passed at 2026-09-26 01:54:39 UTC, including queued guest draft ownership and expired-upload restart.
- Graphics PlayMode: 10/10 passed, including custom texture/aspect ratio and My Stickers screenshots. Fixtures use synthetic artwork, not device camera or AI output.
- Final unsigned iOS Xcode Debug build passed after separate ARKit preparation/export, including the native editor, Swift shim, custom AR artwork, and native map thumbnails.
- Physical iPhone, Apple Intelligence, and two-device acceptance remain pending in DEVICE_VERIFICATION.md. Includes main's AR publishing fix 267db78. No live backend deployment or feature merge into the main checkout; concurrent NFT work remains separate.

## Reproduce verification

Use README Unity commands with this worktree path. Logs/results: /private/tmp/tagtag-creation-edit.{log,xml}, /private/tmp/tagtag-creation-play.{log,xml}, /private/tmp/tagtag-creation-xcode.log, /private/tmp/tagtag-creation-backend.log. Run backend npm test. Prepare ARKit in its own Unity invocation before BuildIos.Build, then xcodebuild with CODE_SIGNING_ALLOWED=NO.

Screenshots: [sources](../verification/sticker-creation/my-stickers-sources.png), [library](../verification/sticker-creation/my-stickers-library.png), [pending save](../verification/sticker-creation/my-stickers-pending-save.png).
