# Sticker celebration verification

## Behavior

Successful publication (including existing local draft cleanup) and a new AR
collection open a full-screen paper reward. **Keep exploring** returns to the
camera; **Read the note** opens existing collection details. Neither action waits
for animation. Existing collection entries open their details without a reward.
The reveal and iOS success haptic wait for actual artwork. Missing custom artwork
has loading/error feedback and Retry artwork; Continue remains usable.

The collection flow checks the existing server collection endpoint when the local
book does not contain the sticker. Failed or capped reads suppress the reward but
still allow collection and note access. No backend deployment or wire change is
required. A simultaneous collection on another device between the membership read
and collect request remains indistinguishable from a new collection: the existing
API does not return an atomic first-collection indicator. This is a known limit.

## Evidence

Graphics-enabled Unity 6000.5.5f1 Play Mode captures, with simulated controller/AR:

- [Found](found.png) and [placed](placed.png), 390 × 844, reduced motion.
- [Found compact](found-compact-large-text.png) and
  [placed compact](placed-compact-large-text.png), 320 × 568, 1.4× text.
- [Missing custom artwork](custom-artwork-unavailable.png) and
  [artwork available](custom-artwork-ready.png). The test injects bundled PNG bytes
  into the real artwork store after a cold-load failure; this is not a network
  download verification. Synthetic non-square metadata exercises aspect sizing.

Automated coverage includes acknowledgement/deduplication, account reset, unknown
server membership, continuation destinations, blocked camera input, unrelated
busy refreshes, enlarged text, interruption, remount, and delayed artwork.
Run Edit Mode with the README command. For graphics-enabled UI checks, omit
`-nographics`, use `-buildTarget StandaloneOSX -testPlatform PlayMode`, and pass
`-testFilter Tagtag.Tests.CelebrationVisualTests` (or `Tagtag.Tests` for the broader
app suite). **217/217 Edit Mode tests passed after integration** ([XML](edit-mode.xml)); **30/30 selected Play Mode tests passed** ([XML](play-mode.xml)), including all seven celebration tests. After integrating the concurrent AR cancellation change, the seven celebration tests passed again ([integration XML](integrated-play-mode.xml)).

The native haptic bridge passed an Objective-C++ syntax check against the iPhoneOS
SDK with ARC and an arm64 iOS 16 target. This is not a complete Xcode app build.

## Device checks remaining

No physical iPhone verification was performed for this change. On a signed build:

1. Publish a preset and a custom sticker; confirm one reveal after final success,
   one success haptic when art appears, and camera input blocked until Continue.
2. Discover a new sticker; confirm Read the note reveals its full note. Recollect
   it, reopen details, refresh, and resume; verify no repeat reward or haptic.
3. Repeat with Reduce motion, enlarged text, safe-area insets, delayed/failed
   artwork, tracking loss, background/resume, immediate Continue, and account loss.
4. Exercise publication failure, cancellation, and local cleanup failure. None
   should celebrate; a successfully completed retry should celebrate once.
5. Verify the extra collection membership read with stale local data and a failed
   read; note access must remain independent of reward eligibility.

## Review

Coordinator owned implementation, Git writes, and Unity runs in the isolated
`codex/sticker-celebration` worktree. Independent read-only code and visual review
used GPT-6-Sol with high thinking in the Herdr **Sticker celebration** tab, without
changing the user's focus. The review accepted the four preset layouts and the
custom-artwork recovery; findings drove the cold-artwork and remount regressions.
The publication cleanup gate was retained as explicitly required by the approved
plan. Existing design tokens, fonts, assets, and motion infrastructure are reused.
