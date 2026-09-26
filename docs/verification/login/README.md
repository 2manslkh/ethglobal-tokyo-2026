# Login verification — 2026-09-26

Required login with the supplied video background, Tagtag header, and branded Apple/Google controls.

## Automated checks

- [Edit Mode results](editmode.xml): **178 passed**, no failures. Includes signed-out navigation/creation gates, restored-session routing, provider cancellation/retry, duplicate activation, and private authored/draft state removal on session loss.
- [Play Mode results](playmode.xml): **19 passed**, no failures. Includes startup, Home and paper UI regression coverage, compact enlarged-text login, retry/configuration states, reduced motion, real video decoding/looping, pause/resume, repaint-schedule reuse, simulated decoder failure, and texture/player cleanup.
- Both review findings (private data surviving session loss and accumulating repaint schedules) were reproduced by failing tests before fixes.

Commands follow README: Unity 6000.5.5f1 `-runTests -testPlatform EditMode -buildTarget iOS`, then graphics-enabled `-runTests -testPlatform PlayMode -buildTarget StandaloneOSX -testFilter 'Tagtag.Tests.PaperVisualTests;Tagtag.Tests.HomeLibraryVisualTests;Tagtag.Tests.StartupTests'`.

## Visual evidence

- [iPhone 17 Pro, iOS 26.2 Simulator](iphone-login.png)
- [iPhone SE (3rd generation), iOS 18.5 Simulator](iphone-compact.png)
- [Dark system appearance](iphone-dark.png) — the app retains its paper theme.
- [320×568, largest in-app text](compact-large-text.png) — rendered by Unity Play Mode.
- [Retry message at largest text size](retry.png) — rendered by Unity Play Mode.

Native captures came from the final IL2CPP iOS Simulator app. Two time-separated captures show changing video-region pixels, confirming playback on the simulator. Provider text, logos, safe areas, and compact bounds were inspected.

Simulator export and unsigned Xcode Debug build succeeded. Logs: `/private/tmp/tagtag-login-simulator/export-final.log` and `xcodebuild-final.log` in the same directory.

Device export also passed after a separate `BuildIos.PrepareArKit` invocation, followed by `BuildIos.Build` and unsigned `xcodebuild -sdk iphoneos -destination 'generic/platform=iOS' CODE_SIGNING_ALLOWED=NO build`. Logs: `/tmp/tagtag-login-work/prepare-device.log`, `export-device.log`, and `xcode-device.log`.

## Physical-device follow-up

Real Apple/Google account authentication and physical-device playback are not claimed as verified. On an iPhone: open signed out, confirm silent looping, cancel each provider and retry, complete sign-in to Home, background/resume, sign out, then enable Reduced motion in Account before signing out and confirm the poster remains still. A decoder failure must leave the buttons usable. Session expiration must return to login without exposing the previous account's data.
