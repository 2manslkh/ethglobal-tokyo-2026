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
| Cold start → STICK | Paper/Taggi remains visible until the first valid camera frame; no blank scene | Paper UI build failed with false permission denial. Follow-up: owner confirms camera opens; first-frame cover timing not separately recorded. |
| Camera permission denied → Settings → return | Truthful recovery prompt; fresh preparation before live imagery | Pending |
| Home/Explore/STICK switching | Paper screens remain opaque; camera restarts without stale imagery | Pending |
| Background/interruption → resume | Cover returns immediately; camera reveals only after a new frame | Pending |
| Tracking loss with live imagery | Camera remains visible with tracking guidance | Pending |
| Note editing with keyboard | Fields and actions remain reachable; draft and selection survive status updates | Pending |
| Explore → sheet → Explore | Map hides under the overlay and restores the same viewport | Pending |

No device video has been received. The owner confirms camera opening and navigation in the follow-up below. First-frame cover timing, detailed permission/lifecycle recovery, software-keyboard clearance, and native-map interaction remain unverified.

## Device feedback follow-up — 2026-09-26

The owner reported two failures in the paper UI build: STICK remained on “Allow camera access to find stickers in AR” even though Settings already granted access, and Explore blocked navigation until its precise-location timeout. No physical success or video is claimed for that build.

The camera cause is confirmed in the shipped Unity export: `UNITY_USES_WEBCAM` and `UNITY_USES_MICROPHONE` are both zero, so Unity's `Classes/Unity/AVCapture.mm` compiles out its authorization query/request and returns denied. A dedicated AVFoundation bridge now reads and requests video permission independently of those stripped modules. Native status controls the decision; Unity's status is logged only for diagnosis. Unknown native status produces a recoverable failure rather than a false permission denial.

Nearby reads now have a separate loading state, cancel location waiting on navigation, and discard late results. Account/sync interruptions resume the lookup when Explore becomes active again. Session invalidation reaches application identity, and automatic nearby resumption preserves foreground errors. GPS accuracy and AR discovery requirements remain unchanged.

The owner also requested Taggi roof/star/map navigation art and Shadows Into Light for display/title text only. Instrument Sans remains on body text, fields, controls, navigation labels, and native map clusters. [Artwork and exact prompts](../Assets/Resources/Tagtag/Navigation/README.md) and [font licenses](../Assets/Resources/Tagtag/Fonts/README.md) are bundled in the repository.

- Edit Mode: **79/79 passed**, including camera permission disagreement and seven nearby navigation/lifecycle regressions. The new service failures were reproduced before their fixes.
- Play Mode: **2/2 passed**, including real-entry startup, mounted flows, font/resource inclusion, navigation during nearby loading, discovery action busy states, and compact enlarged-text footer bounds. The initial compact capture exposed footer clipping; intrinsic nav height and nonshrinking shell sections repaired it.
- [32 rendered screen/sheet captures](verification/device-followup/contact-sheet.png), [Edit Mode result](verification/device-followup/edit-mode.xml), and [Play Mode result](verification/device-followup/play-mode.xml) are retained. Native map/camera imagery and software keyboard are not represented by the fixture. Text fields contain Japanese/Latin content to exercise fallback fonts.
- Integrated static review closed with no remaining concrete findings after the service and UI corrections.
- Built macOS player: both local NUnit callbacks passed at 2026-09-25 20:14:09 UTC ([startup](verification/device-followup/tagtag-startup-result.xml), [mounted UI](verification/device-followup/tagtag-paper-visual-result.xml)). The editor connection hung again; the completed test pair was terminated only after reading the fresh passing callbacks. No native-camera result is implied.
- Separate ARKit preparation and iOS export passed. The Xcode Sources phase includes `TagtagCameraPermission.mm`; AVFoundation is linked explicitly. Generated IL2CPP code calls both bridge functions, and both symbols are present in the linked UnityFramework binary.
- Unsigned Xcode compilation and automatic development signing passed (`/private/tmp/tagtag-device-followup-xcode-unsigned.log`, `/private/tmp/tagtag-device-followup-xcode-signed.log`). USB installation on Dawg. returned `InstallComplete` without uninstalling (`/private/tmp/tagtag-device-followup-install.log`).
- Isolated arm64 simulator preparation, export, and Xcode build passed. Installed and launched on iPhone 17 Pro, iOS 26.2; the [native Home capture](verification/device-followup/simulator-home.png) confirms the handwritten heading, Instrument control text, three Taggi icons, and safe-area layout. Interactive native-map/keyboard checks remain unverified because the Simulator desktop application is unavailable on this Mac.
- Physical retest on Dawg. (iPhone 15 Pro Max, iOS 26.6.1 / 23G83): the owner replied **“Camera opens and navigation works”** after installing this follow-up. This confirms live camera opening and that Explore no longer traps navigation. Settings permission return, first-frame cover timing, and background/interruption recovery were not individually reported; no device video was received, so those detailed cases remain unverified.

## AR artwork and camera flow — 2026-09-26

After confirming camera startup and navigation, the owner reported that the sticker itself appeared as a magenta square. They requested a full-screen camera without the generic header/tab bar, an inventory button, and the source app's placement flow.

The standalone player reproduced the actual material-path failure: `ArExperience.CreateVisual` creates a quad, then `new Material(Shader.Find("Unlit/Transparent"))` throws `ArgumentNullException` because that shader is absent from the player. The camera-flow fixture also failed because the old tab bar remained mounted. All 79 previous Edit Mode tests passed while the 10 new UI/AR behavior tests failed against their placeholders. These establish the red checkpoint before implementation.

The repair bundles the source app's material/shader and checks resources before creating a quad. STICK now hides the generic header/navigation, has a floating Close and an 88-point inventory control, and offers the four existing Taggi designs. Selection precedes explicit tracked-surface placement. Yellow plane outlines, drag along the original plane, pinch/twist, and collapsed accessible size/rotation controls follow the source flow. Note writing requires an attached preview, while publishing retains the existing mapping/authentication gates. Discovery retains its selected clue and retry action.

- [Edit Mode](verification/ar-sticker-flow/edit-mode.xml): **94/94 passed**, including camera interaction bounds, original-plane movement, tracking interruption, multi-contact suppression, and gesture cancellation.
- [Graphics Play Mode](verification/ar-sticker-flow/play-mode.xml): **6/6 passed**, including real-entry startup, mounted UI continuity, full-screen inventory/placement/note flow, real UIDocument hit testing, missing-art cleanup, and rendered artwork/alpha.
- [Built macOS player](verification/ar-sticker-flow/player.xml): **6/6 passed**, with fresh local suite timestamps 2026-09-25 20:58:59–20:59:27 UTC, after the successful build at 20:58:56 UTC. The editor connection timed out; only the completed test editor/player were stopped after reading the local result. This is player rendering evidence, not physical AR evidence.
- [43 screen captures and sticker render](verification/ar-sticker-flow/contact-sheet.png) were inspected, including compact enlarged text, reduced motion, expanded controls, and inventory. The inventory overlap found during capture review was corrected and covered by a mounted geometry assertion. Simulated camera imagery is neutral gray; no native keyboard or physical plane imagery is implied.
- Static review closed with no remaining concrete findings after preserving discovery retry/clue and canceling the whole gesture on unexpected pointer loss.
- Separate ARKit preparation and iOS export succeeded (`/private/tmp/tagtag-ar-flow-prepare.log`, `/private/tmp/tagtag-ar-flow-export.log`). The export log confirms compilation and inclusion of `Tagtag/DeviceSticker` and its Resources material.
- Unsigned Xcode compilation and automatic development signing succeeded (`/private/tmp/tagtag-ar-flow-xcode-unsigned.log`, `/private/tmp/tagtag-ar-flow-xcode-signed.log`). USB installation on Dawg. (iPhone 15 Pro Max, iOS 26.6.1 / 23G83) returned `InstallComplete` without uninstalling (`/private/tmp/tagtag-ar-flow-install.log`).

The owner was asked to reopen STICK, choose inventory art, scan a wall/table, tap an outlined surface, and check transparent Taggi artwork, drag/pinch/twist, and Close. Physical verification of this AR artwork/placement update remains pending that retest. Earlier confirmation that the camera opens does not verify the new material, detected planes, tap placement, drag/pinch/twist, or publishing. No new device video has been received.
