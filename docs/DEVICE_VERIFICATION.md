# Device verification

## Build under review

Unity 6000.5.5f1; bundle `com.kenk.tagtag`; Apple team `5Y6QUA9GA6`. Target: Dawg., iPhone 15 Pro Max. A second ARKit iPhone is required for shared recovery.

## Automated evidence

- 2026-09-26: final Unity Edit Mode run passed 39/39 tests (`/private/tmp/tagtag-final-tests.xml`), including service boundaries, account-specific storage, restart retries, and offline blocks.
- Backend behavior suite passed 18 tests with 0 failures (one emulator-only case skipped in the ordinary run); separate Firebase Auth/Firestore/Storage emulator integration passed 1/1.
- Unity export passed with `libUnityARKit.a` and `ARKit.framework` explicitly asserted. Native Xcode compilation and development signing succeeded with the app-specific Apple-capable profile.
- Live API health/authentication/nearby and cleanup-job checks passed; see [deployment](DEPLOYMENT.md).

## Physical checks

On 2026-09-26, the signed build installed on Dawg., iPhone 15 Pro Max running iOS 26.6.1 (23G83), through USB with `ios-deploy`. The user approved replacing the previous installation after the signing-team change. A Documents backup was attempted and contained no files. Installation returned `InstallComplete`; a subsequent `--exists --bundle_id com.kenk.tagtag` check returned `true`.

CoreDevice still reported the phone unavailable despite valid USB trust pairing. The USB launch attempt failed because its developer-image service was unavailable, and screenshot capture could not start. The user subsequently reported a black screen on manual launch; see the startup investigation below.

Record device/iOS/build, observed result, and screenshot paths for each check:

1. Cold launch; Home has exactly 5×4 spaces, safe areas, no clipped text; 0/1/20/21 collections paginate correctly.
2. Apple/Google login, cancellation, app restart, sign-out, reauthentication, and independent second account.
3. Explore map gestures, clusters, marker selection, recenter; overlays fully hide the native map.
4. Camera and location permission denial/retry; poor GPS/tracking shows useful errors without losing the draft.
5. Place each preset; position, pinch, rotate, keyboard editing; interrupt upload and retry without duplicates.
6. Phone A publishes. Phone B sees only teaser, enters AR, relocalizes, directly taps within 3m, reveals note, and gains one copy. Original remains. Repeat tap does not duplicate the collection.
7. Restart and changed lighting recovery. Wrong room, stale recovery, tracking loss, and proximity without a tap never unlock the note.
8. Offline collection viewing; reconnect; withdrawal, blocking, moderation, and account deletion hide or revoke content as specified.
9. Smaller screen, large text, VoiceOver, Reduce Motion, and keyboard controls.

## App branding — 2026-09-26

- App icon: unchanged `docs/mascot/Taggi 3.png` artwork copied to `Assets/Tagtag/Branding/AppIcon.png`. The import is uncompressed with alpha disabled; Unity generates the iOS sizes, including the 1024-pixel entry, from the 512-pixel source.
- `BuildIos.PrepareArKit` and `BuildIos.Build` passed in separate Unity invocations. Export inspection verified all 10 icon catalog entries have the declared dimensions and no alpha channel.
- `xcodebuild -project Build/iOS/Unity-iPhone.xcodeproj -scheme Unity-iPhone -configuration Debug -sdk iphoneos -destination 'generic/platform=iOS' -derivedDataPath Build/DerivedData CODE_SIGNING_ALLOWED=NO build` passed.
- Compiled app metadata verified: display name `tagtag`, bundle `com.kenk.tagtag`, version `0.1.0`, build `0`, `AppIcon` catalog reference, and the updated camera/location descriptions. `BuildIos.ConfigureAppMetadata` also exposes a **tagtag > Configure App Metadata** editor menu command.
- [Icon preview](verification/app-icon-preview.png) inspected at 60-point home-screen and 29-point settings sizes with rounded corners. This is a rendered preview of exported assets, not a device screenshot.
- Physical branding verification is **pending**: CoreDevice reported no available physical iPhone during this check. On an available iPhone, install the updated build, confirm the home-screen name/icon, and check both permission prompts on a fresh permission state. Record the device, iOS version, and screenshots here.

## Startup rendering — 2026-09-26

- Reproduced the player failure independently of the phone: Unity reported missing ICU text data and threw `NullReferenceException` while measuring UI text. The editor had rendered the same scene successfully, so the previous logic-only tests did not detect it.
- The UI created `PanelSettings` entirely at runtime. Added a saved Resources panel with its theme, text data, and shader references, and instantiate that panel for the runtime document.
- Added `EntrySceneRendersHomeOnFirstLaunch`, which loads the actual entry scene, checks Home controls, renders the panel to a texture, and requires a visible paper background. The built macOS test player passed and produced this [Home capture](verification/tagtag-startup-player.png), with no missing-ICU or null-reference errors.
- The player's editor connection timed out on this Mac. A test-only result callback saved the passing NUnit case to `Application.temporaryCachePath/tagtag-startup-result.xml`; the captured result is also at `/private/tmp/tagtag-startup-player-result.xml`. This verifies the player render, not native iPhone AR or authentication.
- An isolated arm64 iPhone simulator export and Xcode build succeeded with `scripts/build-ios-simulator.sh`. Installed and launched on iPhone 17 Pro, iOS 26.2 (`47DFB6CC-45B4-422E-9ECD-5D0329FBB600`). The [simulator Home screenshot](verification/tagtag-simulator-home.png) shows readable text, all 20 book spaces, and the three navigation tabs. Startup logs contain no missing-ICU or null-reference errors. The simulator retains the native identity/map bridges; ARKit is disabled only in its temporary project.
- Re-exported the physical iPhone build with the ARKit archive assertion enabled; automatic signing with team `5Y6QUA9GA6` passed. Updated Dawg. through USB without uninstalling; deployment returned `InstallComplete`. Physical launch still needs a manual reopen because CoreDevice remains unavailable. The simulator screenshot is the verified launch evidence for this fix.

## Current limits

No successful two-device AR recovery, live provider sign-in, or current-build hardware launch is claimed until the checks above are recorded. Budget alerts warn about spend; they are not a spending cap.

## Paper UI pass — 2026-09-26

The UI pass adapts self-contained paper components, Bricolage/Instrument fonts, mounted screen updates, keyboard-safe sheets, die-cut map pins, and camera presentation states. The fixed collection layout remains five rows by four columns. Authentication, service contracts, discovery distance, and direct-tap requirements remain in force.

- Final Edit Mode: 66/66 passed (`/private/tmp/tagtag-paper-edit-delivery.xml`).
- Integrated Play Mode: 2/2 passed (`/private/tmp/tagtag-paper-play-final.xml`), covering startup, mounted draft/caret/selection/scroll preservation, sign-in return, duplicate activation, synced note revocation, publication draft clearing, and compact long-clue geometry.
- Built macOS player: both local NUnit callbacks passed at 2026-09-25 19:32:19 UTC. The editor/player test connection still does not complete reliably on this Mac; local test results provide the evidence. No physical-camera result is implied.
- Source review identified an additional return-focus case when sync replaces the originating book cell. The expanded regression reproduced it in `/private/tmp/tagtag-paper-focus-red.xml`; the ID-based repair passed the final full Play Mode run, 2/2 (`/private/tmp/tagtag-paper-final-green.xml`). The reviewer closed all findings.
- [31 settled screen/sheet captures](verification/paper-ui/contact-sheet.png) use deterministic content at 390×844 and 320×568, with enlarged text, reduced motion, long Japanese/Latin notes, and disabled/error states. Native imagery is absent from the fixture; its neutral camera background is simulated. Individual captures and the [Edit Mode](verification/paper-ui/edit-mode.xml) / [Play Mode](verification/paper-ui/play-mode.xml) results are retained alongside the contact sheet.
- Separate ARKit preparation and iOS export succeeded. Export includes `libUnityARKit.a`, `ARKit.framework`, four native Taggi pin PNGs, and Instrument Semibold registered in `UIAppFonts`.
- Unsigned Xcode compilation and automatic development signing both succeeded, then passed again for the final copy correction (`/private/tmp/tagtag-paper-xcode-unsigned-delivery.log`, `/private/tmp/tagtag-paper-xcode-signed-delivery.log`). The device build is `/private/tmp/tagtag-paper-ui/Build/DerivedData/Build/Products/Debug-iphoneos/tagtag.app`.
- Updated Dawg. (iPhone 15 Pro Max, iOS 26.6.1 / 23G83) through USB with `ios-deploy`; the final installation returned `InstallComplete` (`/private/tmp/tagtag-paper-install-delivery.log`) without uninstalling or changing the signing team. Device owner was notified to open the new build and perform the camera checks below.
- Isolated arm64 simulator preparation, export, and Xcode build succeeded in `/private/tmp/tagtag-paper-simulator`. Installed and launched on iPhone 17 Pro, iOS 26.2. The [native startup capture](verification/paper-ui/simulator-home.png) verifies bundled typography, safe areas, and the paper layout. The first checkpoint exposed a duplicate initial Home status invitation; the final actual-scene regression passed 1/1 after suppressing only that redundant notice while preserving errors. The Simulator desktop application is absent from this Mac, so interactive native-map and software-keyboard verification could not be completed through the UI tooling.

### Physical camera recording

The device owner has offered to test the iPhone once the new build is installed. Record a device video and its path, then mark each observed result:

| Case | Expected observation | Current result |
| --- | --- | --- |
| Cold start → STICK | Paper/Taggi remains visible until the first valid camera frame; no blank scene | Pending |
| Camera permission denied → Settings → return | Truthful recovery prompt; fresh preparation before live imagery | Pending |
| Home/Explore/STICK switching | Paper screens remain opaque; camera restarts without stale imagery | Pending |
| Background/interruption → resume | Cover returns immediately; camera reveals only after a new frame | Pending |
| Tracking loss with live imagery | Camera remains visible with tracking guidance | Pending |
| Note editing with keyboard | Fields and actions remain reachable; draft and selection survive status updates | Pending |
| Explore → sheet → Explore | Map hides under the overlay and restores the same viewport | Pending |

Video has not yet been recorded. Hardware startup, camera permissions/lifecycle, software-keyboard clearance, and native-map interaction are unverified for this pass until device results are entered here.
