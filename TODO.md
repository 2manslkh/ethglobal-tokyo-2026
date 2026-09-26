# Project TODO

## Triage

### UI Improvements

- [ ] There are 2 top navbars now, remove the top one with tagtag and login button
- [ ] Statusbar is currently hidden. Make it visible.
- [ ] Some buttons and containers can have a die cut sticker design as well. In the Camera screen, the close button could be a Circular sticker with a circular dotted die cut line and an X in the middle. The Place sticker component in the same screen can be replaced with a sharp edged rectangular sticker with the same dotted die cut line. Remove all other texts except "Place Sticker" text.
- [ ] Remove the Size and rotation component, size and rotation should be done by finger gestures

## 1. Verify the current iPhone app

- [ ] Run the latest build on the iPhone and record real Apple and Google sign-in, session restore, cancellation, sign-out, and token refresh results. Confirm a second tester account can sign in. [Auth setup](docs/AUTHENTICATION_SETUP.md)
- [ ] Exercise create → draw → place → save → force-quit → recover → read the private post on hardware. Include keyboard acceptance, camera tap and gesture edge cases, tracking loss, and a wrong-room attempt. Record failures and the device/iOS versions. [Local journey](docs/JOURNEY_POLISH.md) · [placement acceptance](docs/STICKER_PLACEMENT_UX.md)
- [ ] Verify a new reference-photo save on hardware: upright real-camera image, sticker included, UI excluded, restart persistence, legacy save, and interrupted save. [Photo acceptance](docs/RECOVERY_REFERENCE_PHOTO.md)
- [ ] Check VoiceOver, larger OS text, Reduce Motion, and physical haptic feel on the supported iPhone journey; fix concrete failures before release. [Haptics](docs/HAPTICS.md) · [device integration](docs/DEVICE_INTEGRATION.md)

## 2. Prove the shared iPhone journey

- [ ] Deploy the authenticated tester backend with the verified tester allowlist, deny-all client Firestore rules, nearby index, conservative service limits, and cost alerts. Configure the app service URL. Record live authorization, revoked-user, wrong-user, quota, and failure-path results. [Backend setup](backend/README.md) · [auth setup](docs/AUTHENTICATION_SETUP.md)
- [ ] On two physical iPhones and independent accounts, publish a placement on phone A and recover it at the original surface on phone B after restart. Repeat under changed lighting, tracking loss, and similar-looking surfaces; record accuracy, time, and failure rate. [Two-device test](docs/TWO_DEVICE_RECOVERY_TEST.md) · [release plan](docs/APP_STORE_RELEASE_PLAN.md)
- [ ] Decide and implement production map storage, capacity, expiry, and cleanup beyond the current 6 MiB / 20-placement / seven-day test transport. Verify interrupted upload and retry behavior without duplicate placements. [Release plan](docs/APP_STORE_RELEASE_PLAN.md)
- [ ] Complete the real loop: independently written public preview, post publishing, nearby discovery, original-surface recovery, protected post reveal, private journal reference/discovery stamp, and return to discovery. Verify withdrawal and blocking revoke later reads. Define server-side post access without trusting a client `resolved=true` claim. [Product spec](MVP_SPEC.md) · [release plan](docs/APP_STORE_RELEASE_PLAN.md)

## 3. Prepare a public release

- [ ] Complete account deletion, reporting, blocking management, moderation operations, ownership checks, session revocation, and an assigned moderation workflow. Replace tester-only auto-approval with reviewed launch controls. [Release plan](docs/APP_STORE_RELEASE_PLAN.md)
- [ ] Seed permissioned starter placements in the launch area and watch a new tester complete a find without coaching. Choose launch area, audience, and moderation owner. [Release plan](docs/APP_STORE_RELEASE_PLAN.md)
- [ ] Verify App Store Connect access, distribution provisioning, supported devices, signing, privacy declarations, and a reproducible archive. Prepare the listing, real screenshots, privacy policy, support contact, review access, and TestFlight build. [Distribution checklist](docs/APP_STORE_RELEASE_PLAN.md) · [releasing](docs/releasing.md)
- [ ] Run TestFlight device regression, resolve blocking defects, submit for review, then verify a fresh App Store installation completes the core loop. [Release plan](docs/APP_STORE_RELEASE_PLAN.md)

## Product polish after the core-loop proof

- [ ] Generate and integrate the planned 15-second portrait login film and poster; inspect its loop and test playback, still fallback, Reduce Motion, and accessibility on a device. The current asset is a temporary fallback. [Video brief](docs/ONBOARDING_VIDEO_BRIEF.md)
- [ ] Review recent native UI and motion on hardware, including interrupted transitions and the stickerbook. Keep any changed interaction quiet by default; use a haptic only for a meaningful semantic outcome under [HAPTICS.md](docs/HAPTICS.md). [Motion coverage](docs/MOTION.md)

## Deferred

- Android persistence, Android authentication, and Google Play release. [Historical Play plan](docs/PLAY_STORE_RELEASE_PLAN.md)
- Authored trails, feeds, chat, and automatic private-draft sync. [Release scope](docs/APP_STORE_RELEASE_PLAN.md)

## Future ideas (unscheduled)

### Sticker creation

- [ ] Generate sticker artwork with AI; explore Apple Intelligence.
- [ ] Upload an image to make a sticker.
- [ ] Make photo or selfie Polaroid stickers.

### Rewards

- [ ] Placers: XP and levels for placing stickers.
- [ ] Collectors: unique-sticker count, XP, and levels.

### Blockchain

- [ ] Let collectors claim stickers as NFTs on an Ethereum testnet only.
