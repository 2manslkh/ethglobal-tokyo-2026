# Feature readiness

Snapshot: **2026-09-26**, source through `90398c8`.

Tagtag is ready for controlled beta testing, but the complete two-phone experience is not yet proven on hardware. Most core features are implemented; the biggest remaining work is device verification and distributing the latest publishing-location fix.

The statuses below summarize recorded evidence, not newly executed tests. A successful build, installation, or launch does not establish that an interactive feature works on a physical device. TestFlight and deployment statuses reflect the linked records at the time of this snapshot.

## Features

| Feature | What we have | Readiness |
|---|---|---|
| Apple / Google login | Required sign-in, session restoration, sign-out, cancellation and retry handling | Implemented and automated-tested. Real provider flows still need documented iPhone verification. |
| Login redesign | Tagtag-only header, looping video, branded login buttons, reduced-motion/poster fallback | Simulator-verified and included in installed builds. Physical playback acceptance pending. |
| Home / sticker library | Collected, My designs and Placed views; paginated collection, previews, private notes and saved navigation state | Implemented and UI-tested. Full device journey pending. |
| Custom stickers | Image import, photo/selfie Polaroids, captions, cropping, foreground cutout and white borders | Implemented and build-tested. Native creation needs hardware acceptance. |
| AI sticker creation | Apple Image Playground integration on supported devices | Implemented; image-loading fix landed. Successful generation still needs a device retry. |
| Saved designs | Account-synced designs, reuse for placement, removal confirmation and upload retry | Implemented and tested; live create → save → reuse verification pending. |
| AR placement | Surface scanning, four mascot presets/custom artwork, tap placement, drag, resize and rotate | Camera opening/navigation confirmed on iPhone. Full tracking and gesture acceptance pending. |
| Publishing | Public teaser, private note, AR-map upload, progress, retained drafts and retry protection | Implemented, but coarse indoor GPS has blocked real use. The new map-confirmation fallback needs physical acceptance. |
| Map-confirmed publishing | Confirm a location on a native map when GPS remains approximate | Client and server changes committed through `90398c8`; local tests and signed build are recorded as passing. Not included in TestFlight build 1. Physical publication and rollout of the latest changes remain to be verified. |
| Explore map | Native Apple map, pins/clusters, teaser sheets, recentering, caching and approximate-location browsing | Fix is integrated, backend deployed and app installed. Actual post-fix GPS behavior on the phone remains unverified. |
| Discover / collect | Recover the original AR placement, collect within proximity, retain the original for others | Implemented and logic-tested. Two independently signed-in phones completing the loop is the main readiness gap. |
| Private notes / collection | Protected note reveal after collection, personal collection and offline caching | Implemented and authorization-tested. End-to-end hardware proof pending. |
| Original spot photo | Shared reference photo and enlarged preview to help find the placement | Implemented and build-tested. Real capture orientation and cross-phone recovery pending. |
| Account and safety controls | Account deletion, publication withdrawal, reporting, blocking and ownership checks | Implemented with backend coverage. Device checks and moderation operations still needed. |
| Accessibility / polish | Larger in-app text, reduced motion, safe-area layouts and interaction feedback | Some automated/visual coverage. VoiceOver, OS text sizing and physical haptics need review. |
| NFT souvenirs | Embedded-wallet integration, Sepolia mint queue/status and transfer-out flow | Implemented but disabled. Live configuration, contract/worker deployment and mint/transfer verification remain. |
| Backend infrastructure | Live API, authentication, private storage, indexes, cleanup, quotas and budget alerts | Deployed with health/authentication smoke checks. Complete authenticated user acceptance remains pending. |
| Distribution | Signed iPhone installation and TestFlight 0.1.0 (1) upload | Install/launch verified. Latest recorded external-beta status is Waiting for Review; public joining awaits approval. |

## Remaining product work

The flippable stickerbook that slides up inside the camera remains unfinished. The existing paginated collection is implemented. XP and levels are future work.

NFT code is not a live NFT feature: minting remains disabled until its configuration, deployment and live acceptance checks are complete.

## Demo priorities

1. Verify rollout and distribute the committed map-confirmed publishing changes in a fresh build. TestFlight 0.1.0 (1), built from `01857d3`, does not contain them.
2. Verify real sign-in and indoor publishing on the iPhone.
3. Complete publish on phone A → discover/recover/collect on phone B → read the private note, including restart.
4. Verify native sticker creation and account/safety flows.

## Evidence

- [Device verification](DEVICE_VERIFICATION.md): build/install evidence, automated results and outstanding hardware checks.
- [Beta distribution](BETA_DISTRIBUTION.md): uploaded source snapshot, review status and backend readiness.
- [Deployment](DEPLOYMENT.md): infrastructure and Explore accuracy rollout.
- [NFT setup](NFT_SETUP.md): disabled feature configuration and live acceptance requirements.
- [Project README](../README.md): implemented app workflows.

Some entries in [TODO](../TODO.md) lag behind implementation; use the verification records to distinguish missing functionality from missing acceptance evidence.
