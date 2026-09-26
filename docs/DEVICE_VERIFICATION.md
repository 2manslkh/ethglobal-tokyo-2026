# Device verification

## Closing a loading AR search — 2026-09-26

Source `034236e` keeps X enabled during discovery loading. Closing cancels the
active search and releases navigation immediately; late GPS, recovery-map and
artwork completions cannot re-enter AR or release a newer search’s action lock.
Other foreground actions retain their existing close protection.

- Reproduced before the fix: closing during GPS wait remained on STICK instead of
  Home (`/tmp/tagtag-discovery-close-red.xml`).
- Edit Mode: **211/211 passed**, `/tmp/tagtag-discovery-close-edit.xml`, including
  cancellation during GPS, late map success/failure and starting a new search.
- Mounted UI: **1/1 passed**, `/tmp/tagtag-discovery-close-play.xml`; the X control
  is enabled during discovery and returns to Explore when activated.
- Separate ARKit preparation, iOS export and signed Debug build succeeded.
  Installed over the existing app on Dawg. (iPhone 15 Pro Max), and launched it;
  CoreDevice installation and launch both exited 0.
- Logs: `/tmp/tagtag-discovery-close-{prepare,export,signed,install,launch}.log`.

Physical tapping and real GPS/map-download timing remain to be verified by the
owner. Installation and launch do not establish physical interaction acceptance.

## Sign-in status text — 2026-09-26

Removed provider-opening progress text from login while preserving error and
service-configuration messages and disabling repeated sign-in actions.
`LoginSupportsRetryCompactTextAndReducedMotion` failed before the change and
passed afterward (`/tmp/tagtag-login-status-red.xml`,
`/tmp/tagtag-login-status-green.xml`). Separate ARKit preparation, iOS export and
signed Debug build succeeded. Source `48e177d` was installed over the existing app
on Dawg. (iPhone 15 Pro Max); CoreDevice installation and launch both exited 0.
Logs: `/tmp/tagtag-login-status-{prepare,export,signed,install,launch}.log`.
No physical provider-authentication interaction was verified in this pass.

## Login refresh installation — 2026-09-26

- Built source `d8c5ccc` with separate ARKit preparation, Unity iOS export and
  automatically signed Xcode Debug/iphoneos build; all exited 0.
- Installed over the existing app on **Dawg., iPhone 15 Pro Max**, UDID
  `00008130-001420500E41001C`, with CoreDevice. Installation exited 0; no
  uninstall or data reset was performed.
- Automatic launch was rejected because the phone was locked. Unlock and open
  tagtag manually. Physical login and AR interaction remain unverified here.
- Logs: `/tmp/tagtag-install-refresh-prepare.log`,
  `/tmp/tagtag-install-refresh-export.log`, `/tmp/tagtag-install-refresh-signed.log`,
  `/tmp/tagtag-install-refresh-device.log`, `/tmp/tagtag-install-refresh-launch.log`.

## Login refresh — 2026-09-26

Replaced the login background with the supplied `loading-screen-sticker.mp4`,
removed the header backing, footer fade and explanatory copy, and added the
existing tagline. Privacy Policy and Terms & Conditions links open in-app sheets
with copy drafted from the current app behavior. Login no longer has a scroll
view; only the modal content scrolls.

- The compact-layout regression failed before the fix because login contained a
  ScrollView (`/tmp/tagtag-login-refresh-red.xml`).
- Edit Mode: **208/208 passed**, `/tmp/tagtag-login-refresh-edit.xml`.
- Paper UI Play Mode: **9/9 passed**, `/tmp/tagtag-login-refresh-full-play.xml`.
  Coverage includes compact enlarged text, transparent sections, visible controls,
  opening and closing both legal sheets while signed out, retry, reduced motion,
  silent video looping, interruption and decoder fallback.
- Native simulator export and unsigned Xcode build: **BUILD SUCCEEDED**.
  Build logs: `/private/tmp/tagtag-login-refresh-review/`.
- Inspected native [iPhone](verification/login-refresh/iphone.png) and
  [compact iPhone](verification/login-refresh/compact.png) captures. Unity captures
  show [enlarged text](verification/login-refresh/login-compact-large-text.png),
  [Privacy Policy](verification/login-refresh/login-privacy.png),
  [Terms & Conditions](verification/login-refresh/login-terms.png), and
  [retry](verification/login-refresh/login-retry.png).

No physical iPhone installation or provider authentication was performed for this
refresh. Legal copy is an initial product draft, not a claim of legal review.

## Find in AR camera startup — 2026-09-26

The controller now opens the camera before acquiring discovery-grade GPS and
fetching the recovery map. Previously all of that work had to succeed before the
camera could start; a location failure left the user on Explore.

- Regression reproduced before the fix: `DiscoveryOpensCameraBeforeLocationCompletesAndKeepsRetryOnFailure`
  expected STICK while GPS was pending, but remained on Explore (`/tmp/tagtag-ar-red.xml`).
- Unity 6000.5.5f1 Edit Mode: **208/208 passed**, `/tmp/tagtag-ar-green.xml`.
  The regression covers immediate camera entry, duplicate taps, clearing the old
  placement selection, and retaining the target and error for retry after GPS failure.
- Separate ARKit preparation and iOS export succeeded (`/tmp/tagtag-ar-prepare.log`,
  `/tmp/tagtag-ar-export.log`). Unsigned Xcode Debug/iphoneos build: **BUILD SUCCEEDED**,
  `/tmp/tagtag-ar-xcode.log`. This build has not been installed on the phone.

Physical-device verification is pending. On an iPhone, select an Explore sticker
and tap **Find in AR** with slow/low-accuracy GPS: confirm the camera opens while
location is checked, then shows retry guidance on failure. Retry with a precise
fix and confirm map recovery and collection still work. Also verify first-use
camera permission and denied camera access. Automated controller tests do not
verify live imagery, ARKit relocalization, or collection on a physical device.

## UI improvements — 2026-09-26

Implemented on `feat/ui-improvements` in `/private/tmp/tagtag-ui-improvements`.
Added dashed die-cut treatments and pressed states, Your Designs creation placement,
active Placed locations with paginated map handoff, a tappable selected-artwork
camera preview, and progressive Explore loading with a short nearby-results cache.

- Unity 6000.5.5f1 Edit Mode: **186/186 passed**, `/tmp/tagtag-edit-green.xml`.
- Graphics-enabled Home/Paper Play Mode: **20/20 passed**, `/tmp/tagtag-ui-play-verified.xml`.
- After final button-local renderer and pagination corrections, Home Play Mode:
  **13/13 passed**, `/tmp/tagtag-home-verified.xml`.
- Compact enlarged-text capture after isolating worktree screenshot output:
  **1/1 passed**, `/tmp/tagtag-compact-isolated.xml`.
- Backend `npm test`: **71 passed**, one Firebase emulator-only test skipped.
- Separate ARKit preparation and iOS export succeeded: `/tmp/tagtag-ui-prepare.log`
  and `/tmp/tagtag-ui-export.log`. Unsigned Xcode Debug/iphoneos check:
  **BUILD SUCCEEDED**, `/tmp/tagtag-ui-xcode.log` (exit 0).
- Reviewed [Placed](verification/ui-improvements/home-placed.png),
  [camera selection](verification/ui-improvements/camera-selected-artwork.png), and
  [compact Your Designs](verification/ui-improvements/home-compact-designs.png).
  These are Unity-rendered fixtures with simulated camera imagery and test data,
  not physical-device captures. Per-checkout capture directories prevent concurrent
  login-work tests from overwriting this feature's evidence.

No physical-device performance improvement, GPS permission behavior, tile failure,
or AR gesture outcome is claimed by these results. On an iPhone, verify cold/warm
Explore visits, slow/offline tile and sticker loading, denied/low-accuracy GPS,
background/resume, panning followed by opening the same placed location, and
preset/custom-thumbnail picker taps while placing and adjusting. Record timing to
first map, location, pins, and artwork before reporting a quantified speedup.
The existing 50-metre nearby accuracy requirement is unchanged here; a concurrent
location-service diagnosis owns any adjustment. Deploy the backend and authored
pagination Firestore index before distributing the client. No backend deployment
or phone installation was performed in this work.

## NFT consolidation — 2026-09-26

Merged the NFT checkpoint `7d00a4c` with local publishing fixes and remote main `3402d97` (custom sticker creation). Preserved both wallet and design account lifecycles, collection artwork URLs and NFT status, API routes, dependencies, indexes, and regression tests. A new combined regression reproduced HTTP 500 when collecting custom artwork with NFTs enabled; custom discoveries now queue generic Taggi preset 0 without copying custom artwork or private notes into the mint job.

- Merged Unity Edit Mode: **166/166 passed**, `/private/tmp/tagtag-nft-main-edit.xml` (exit 0).
- Merged backend: **68 passed, 0 failed**, one emulator-only case skipped, `/private/tmp/tagtag-nft-main-backend.log`.
- Combined Firebase Auth/Firestore/Storage emulator: **1/1 passed**, `/private/tmp/tagtag-nft-main-emulator.log` (exit 0).
- Native preparation, export, signing and iPhone installation are handed to the build owner after this merge; no new native build/device result is claimed here. The pre-existing local `TODO.md` edits remain unstaged and unchanged.

## NFT integration — 2026-09-26

Implemented against the [approved NFT plan](plans/2026-09-26-nfts.md), including the user's later transfer-out decision. Operational defaults still apply: account deletion does not erase NFTs. The deletion screen offers individual Sepolia transfers, persisted submission and confirmation states, and explicit acknowledgement of access loss before the deletion fallback. Transfer gas requires Sepolia test ETH in the embedded wallet.

- [Unity Edit Mode](verification/nfts/edit-mode.xml): **124/124 passed**, including wallet lifecycle/account isolation, NFT presentation, recipient checksum validation, transfer persistence failures, restart recovery, and receipt states.
- [Graphics Play Mode](verification/nfts/play-mode.xml): **1/1 passed** for `NftStatusUpdatesWithoutReplacingPrivateNotes`. It exercises book detail updates, preserved private notes, wallet presentation, transfer submission, and deletion acknowledgement. The final switch has a geometry assertion preventing label-induced compression.
- Backend `cd backend && npm test`: **46 passed, 0 failed**, with one emulator-only case skipped. The separately approved Firebase Auth/Firestore/Storage emulator run passed **1/1** using `npx --yes firebase-tools@14.21.0 emulators:exec --only auth,firestore,storage --project demo-tagtag 'cd backend && npm run test:emulator'` (local log `/private/tmp/tagtag-nft-final-emulator.log`, exit 0).
- Contracts `cd contracts && forge test --offline`: **9/9 passed**, including mint authorization, duplicate prevention after transfer, metadata selection, pause behavior, and non-Sepolia deployment rejection.
- Security review closed all findings: interrupted wallet binding recovery, pending account-deletion handling, signer-specific nonce state, transfer persistence failure recovery/temporary-file cleanup, and checksum-invalid recipient rejection. An independent harness checked seven recipient cases across UI, service, and wallet boundaries.
- Rendered fixtures: [pending NFT](verification/nfts/nft-pending.png), [confirmed NFT](verification/nfts/nft-confirmed.png), [wallet](verification/nfts/nft-wallet.png), [transfer-out](verification/nfts/nft-transfer-out.png), [pending transfer](verification/nfts/nft-transfer-pending.png), and [informed deletion fallback](verification/nfts/nft-deletion-fallback.png). These use deterministic data, not live transactions.
- At implementation commit `7b61fb4`, separate `BuildIos.PrepareArKit` and `BuildIos.Build` invocations exited **0** using Unity `6000.5.5f1` and `-buildTarget iOS`. Export includes `libUnityARKit.a`, `ARKit.framework`, and `TagtagWalletSessionProtection.mm`. Local logs: `/private/tmp/tagtag-nft-final-prepare.log` and `/private/tmp/tagtag-nft-final-export.log`.
- The already-running unsigned native check completed with **BUILD SUCCEEDED**, exit **0**: `xcodebuild -project Build/iOS/Unity-iPhone.xcodeproj -scheme Unity-iPhone -configuration Debug -sdk iphoneos -destination 'generic/platform=iOS' -derivedDataPath Build/DerivedData CODE_SIGNING_ALLOWED=NO build`. Local log: `/private/tmp/tagtag-nft-final-xcode.log`. No deployment or installation was performed; the user owns consolidation and the subsequent combined build.

Live NFT rollout remains **disabled**. No live mint, live transfer, Thirdweb wallet restoration, or NFT physical-device result is claimed. Thirdweb JWT configuration, pinned metadata, a deployed contract, funded signer, worker deployment, and the two-device checks in [NFT setup](NFT_SETUP.md) remain required before enabling the feature.

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

Physical retest on Dawg.: after being asked to reopen STICK, choose inventory art, scan a wall/table, tap an outlined surface, and check transparent Taggi artwork, drag/pinch/twist, and Close, the owner replied **“Yup it all looks good”**. This records the owner's successful overall retest of the requested artwork and placement flow, with no failures reported. Publishing and the earlier detailed permission/interruption cases were not part of this confirmation. No device video was received.

## Publishing and control feedback — 2026-09-26

The owner reported slow publishing followed by “We need a more accurate location. Move outdoors and try again,” and said Precise Location was disabled or uncertain. They also requested no button outlines or content movement during hover/tap and a more prominent collected count. The project tagline is now **Find your places, Collect your moments**.

Diagnosis found that Unity's exported location-enabled query reads the device-wide switch, not the app's permission/precision authorization. The old publishing path saved an AR map before discovering a known precision failure, and location updates used a two-metre movement filter despite the thirty-second freshness requirement. A native Core Location probe, early permission check, stationary updates, and placement-time prewarming address those avoidable waits. Genuine weak GPS can still fail the unchanged ≤50 m accuracy and ≤30 s freshness checks. No publication latency improvement has yet been measured on the phone.

Initial verification reproduced four location/publication failures in 101 Edit Mode cases and the growing focus border in a mounted UI test. After the first implementation, Edit Mode passed 108/108 and graphics Play Mode passed 7/8. The failing Settings assertion queried a hidden screen notice instead of the active sheet. Capture inspection additionally found a focus-marker child breaking text-only button sizing; that marker was removed. Review also identified draft persistence, in-flight editing, and lifecycle cleanup cases for repair before final verification. These initial results are checkpoints, not final acceptance.

The repaired publishing snapshot passed **120/120 Edit Mode** (2026-09-26 00:09:35–36 UTC) and **8/8 graphics Play Mode** (00:20:02–38 UTC). The final bounded source review found no residual blocker in the six repaired paths. Fresh captures confirm readable text-only buttons, borderless tonal pointer feedback, a stronger count, and a reachable compact Settings action. Mounted tests verify disabled in-flight editing and restoration of the same note field, text, caret, and selection afterward. An already-issued network request may finish remotely after interruption; the saved operation remains available for reconciliation by retry. Actual upload/finalize interruption on hardware is still unverified.

Physical follow-up remains pending: with Precise Location off, check immediate Settings guidance and draft retention; enable precision, return, retry publishing, and record the slowest visible stage. Check a stationary note-writing interval, interruption during map capture/upload, no duplicate retry, stable button labels, and the emphasized collected count. No device publication success is claimed for this update yet.

## Publishing location and latency follow-up — 2026-09-26

Publication now starts GPS acquisition alongside AR map capture, accepts a fresh publishing fix up to 100 m, and reuses it at finalization while fresh. Nearby/recovery/collection still require 50 m. The backend was deployed to Cloud Run revision `tagtag-api-00002-4kt`; `/health` returned 200, and live nearby validation continued to reject a 75 m fix while accepting an 8 m fix.

On Dawg. (iPhone 15 Pro Max, iOS 26.6.1 / 23G83), two physical attempts on the first updated build captured AR map stage at 150 ms and location-prepare failures at 19,870 ms and 20,004 ms. The location timeout reported FullAccuracy authorization, a cached 2,000 m accuracy fix, and age 72 seconds. This confirmed the location stage, rather than map capture, was the remaining wait. A follow-up fix restarts an already-running prewarmed location session when its cached timestamp is stale. Its regression test failed before that change and the full Edit Mode suite passed 124/124 afterward. On the new build, the retry updated the timestamp to 20 seconds but accuracy remained 2,000.149 m; location-prepare failed at 20,014 ms. The fix addressed stale prewarming, but the current device environment still does not provide a usable measurement. Physical publish success and latency improvement remain unverified; owner was asked to retry near a window or outdoors with Wi-Fi enabled.

## Navigation and camera simplification — 2026-09-26

The global brand/sign-in header and reserved space are removed. Home's profile
icon opens guest sign-in or the signed-in account screen. Camera Close uses a
48-unit circular paper sticker; the placement title sticker contains only
“Place Sticker.” Guidance stays in a separate scrollable notice. Placement uses
existing drag, pinch and twist gestures with unchanged size limits and blocking.
Recovery content scrolls between the header and action dock at enlarged text size.

Verification commands (Unity `6000.5.5f1`, repository root):

```sh
/Applications/Unity/Hub/Editor/6000.5.5f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -nographics -projectPath "$PWD" -buildTarget iOS \
  -runTests -testPlatform EditMode \
  -testResults /tmp/tagtag-navigation/edit.xml -logFile /tmp/tagtag-navigation/edit.log
/Applications/Unity/Hub/Editor/6000.5.5f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -projectPath "$PWD" -buildTarget StandaloneOSX \
  -runTests -testPlatform PlayMode -testFilter Tagtag \
  -testResults /tmp/tagtag-navigation/play.xml -logFile /tmp/tagtag-navigation/play.log
```

Evidence is retained in [navigation-camera](verification/navigation-camera/).
The captures use a 390×844 standard fixture and a 320×568 fixture with 1.4× text
and reduced motion. These are rendered Unity fixtures; camera imagery is simulated
and the native map/status bar are not present in these captures. Coverage includes
Home, Explore, placement, discovery, permission denial, interruption, unavailable
camera, guest profile access, authentication return, signed-in account access,
placement readiness, sheet input blocking, and recovery-action scrolling.

The new UI regressions first failed on the old header and Close size; the compact
recovery regression first failed before its scrollable layout was added. The full
Edit Mode suite passed 94/94 and the graphics-enabled Play Mode suite passed 6/6.
A separate GPT-6-Sol high review found no actionable correctness issues.

Native status-bar settings and the export hook now enforce visible dark status
text. The existing generated Unity iOS controller was inspected and reads those
plist keys. Native export/build, actual status-bar visibility, safe-area placement,
and camera coordinate alignment on iPhone remain **pending the combined publishing
dogfood integration build**, per coordinator instruction. No device build was
installed and no native build was run for this branch.

CoreDevice did not expose an available physical iPhone during this task. Physical
pinch/twist, surface drag, sheet blocking, tracking interruption, background/resume,
and VoiceOver remain **unverified** on this revision. Run and record these checks
after the coordinator installs the combined build; do not replace that build with
this branch's older standalone output.

## Combined publishing build — 2026-09-26

The publishing changes and the navigation/camera branch are integrated at `be3cf0a`. The exact tagline appears in the empty book and project copy. Button pointer/focus feedback changes color without outlines or content movement; Home and Account emphasize the collected count. The separate Home profile control, gesture-only camera adjustment, die-cut camera controls, and native status-bar configuration are retained.

- [Edit Mode](verification/publishing-dogfood/edit-mode.xml): **120/120 passed** at 00:34:03–04 UTC.
- [Graphics Play Mode](verification/publishing-dogfood/play-mode.xml): **8/8 passed** at 00:34:39–00:35:20 UTC.
- [Built macOS player](verification/publishing-dogfood/player.xml): **8/8 passed** at 00:38:10–52 UTC, after the successful build at 00:38:07 UTC. The completed owned editor/player pair was stopped after saving the fresh local callback because the editor return connection hung.
- [Fifteen built-player captures](verification/publishing-dogfood/contact-sheet.png) inspected: Home empty/populated/enlarged text, Account, default/hover/press/focus controls, publishing progress, and normal/compact Settings recovery. Camera imagery is simulated; no native keyboard, GPS or status-bar rendering is implied.
- Separate ARKit preparation and final iOS export succeeded (`/private/tmp/tagtag-dogfood-prepare.log`, `/private/tmp/tagtag-dogfood-export-final.log`). Standalone-test changes to player settings were restored before the final export. The Xcode Sources phase contains `TagtagLocationAuthorization.mm`, CoreLocation is linked, and exported plist keys request visible dark status text.

- Unsigned Xcode compilation and automatic development signing both passed (`/private/tmp/tagtag-dogfood-xcode-unsigned.log`, `/private/tmp/tagtag-dogfood-xcode-signed.log`). The linked UnityFramework contains `TagtagLocationAuthorizationStatus` and its IL2CPP caller. The signed app is `Build/DerivedData/Build/Products/Debug-iphoneos/tagtag.app` in the isolated publishing worktree, bundle `com.kenk.tagtag`, team `5Y6QUA9GA6`.
- After the owner connected Dawg (iPhone 15 Pro Max, iOS 26.6.1), USB installation with `ios-deploy` completed successfully (`InstallComplete`, `/private/tmp/tagtag-dogfood-install.log`). The existing app was updated without uninstalling. The owner was asked to enable Precise Location and report publication duration or the stalled progress stage. Publishing latency, Precise Location return, interruption/retry, native status-bar appearance and AR coordinate alignment remain pending hardware verification.

## Custom sticker creation — 2026-09-26

Implementation is isolated on `feat/sticker-creation`. Physical verification remains pending; no live Apple Intelligence, camera/selfie, two-phone custom publication, or production storage precondition success is claimed.

Device acceptance:

1. On an Apple Intelligence-capable phone, generate artwork, cancel and reopen, preview, save, and select it from My Stickers. On an unsupported phone, confirm unavailable guidance and working image import/Polaroid alternatives.
2. Import JPEG/HEIC and transparent PNG from Photos and Files. Verify full-image aspect ratio, square crop, cutout/no-subject recovery, white border, and final preview. Confirm unsupported cutout keeps full-image creation available.
3. Create front/rear-camera and library Polaroids. Confirm upright orientation, selfie mirroring, 40-character caption, frame, Edit from preview, and cancellation. Deny camera permission and confirm library imports remain usable.
4. Save while signed out, sign in, interrupt upload, restart, and retry. Confirm one immutable design appears in the correct account, is restored on another iPhone, and cached art works offline.
5. Place the same design twice; pinch/rotate it without stretching. Publish on phone A, recover and collect on phone B, restart, and verify matching pixels. A collector must not be able to place the creator's design.
6. Remove the design from My Stickers and confirm published copies survive. Block/report/remove content and confirm subsequent sync hides its artwork. Delete an account and verify backend asset cleanup.
7. Check VoiceOver, larger text, Reduce Motion, native sheet cancellation, and camera restoration with an existing placement/note.

Verification: Unity Edit Mode **134/134**, graphics Play Mode **10/10**, backend **38 passed / 1 emulator-only skipped**, Firebase emulator integration **1/1**, and final unsigned iOS Xcode build passed. Captured UI fixtures are in `docs/verification/sticker-creation/`; they use synthetic artwork. The Firebase Storage emulator does not prove GCS conditional-write enforcement; see backend README before live deployment.

### Integrated NFT and sticker build — 2026-09-26

- Source: `efc715aebff5045cf169346b4d19ffbb578d7b56`, with local main and origin/main synchronized after merging NFT and custom-sticker work.
- ARKit preparation and Unity iOS export succeeded. Automatic development signing with team `5Y6QUA9GA6` produced `Build/DerivedData/Build/Products/Debug-iphoneos/tagtag.app`; Xcode reported `BUILD SUCCEEDED`.
- Installed over the existing app on USB device **Dawg.**, UDID `00008130-001420500E41001C`, reported model iPhone16,2 (iPhone 15 Pro Max), iOS 26.6.1. `ios-deploy` reported `InstallComplete` and exited 0. No uninstall or data reset.
- Logs: `/tmp/tagtag-integrated-prepare.log`, `/tmp/tagtag-integrated-export.log`, `/tmp/tagtag-integrated-xcode.log`, `/tmp/tagtag-integrated-install.log`.
- This verifies build/signing/installation only; no new on-device interaction or live-backend deployment was performed in this pass.

## Home library update — 2026-09-26

Unity graphics-enabled Play Mode verification covers the Collected/My designs switch, sorted private designs, preview/placement/removal, failed operations, account changes, artwork recovery, and session scroll/page continuity. Standard and compact/large-text layouts were inspected; [Home screenshots](screenshots/home-library/home-collected-empty.png) use deterministic test content.

Physical-device verification for this update is **pending**. On an iPhone, check Home at normal and larger text sizes, create/save a design, open it from My designs, place it in AR, return to the same gallery, then cancel and confirm design removal. Verify saved copies remain in collections and sign-out clears private designs. Do not treat editor tests as proof of native creation or AR handoff.

## Creator and scan update — 2026-09-26

Source through `5fb423a` adds illustrated Upload/Photo/Imagine choices, Home My designs navigation, save confirmation, design-specific errors, four real AR scan stages, and translucent yellow pencil-hatched surface polygons. The existing Photo/Polaroid editor remains.

- [Edit Mode](verification/creator-scan/tagtag-creator-edit.xml): **174/174 passed**.
- [Graphics Play Mode](verification/creator-scan/tagtag-creator-play.xml): **21/21 passed**, including hatch rendering and Home/Paper UI suites.
- [Compact creator follow-up](verification/creator-scan/tagtag-creator-compact.xml): **7/7 PaperVisual tests passed**; [320-pixel enlarged-text capture](verification/creator-scan/creator-compact-largest.png) inspected for tile bounds and labels.
- [Save confirmation regression](verification/creator-scan/tagtag-creator-save.xml): **1/1 passed** after the final UI fix.
- Backend `npm test`: **68 passed, 1 emulator-only skipped**, no failures. Log: `/tmp/tagtag-creator-backend.log`.
- [Hatch capture](verification/creator-scan/tagtag-plane-hatch-render.png) verifies clipping to a concave polygon, transparency and stroke variation in the editor. [Scan indicator capture](verification/creator-scan/camera-surface-ready.png) uses simulated camera state.
- Production API revision `tagtag-api-00003-75l` now serves 100% of traffic. Both design indexes are ready; cleanup uses the matching image. NFT minting remains disabled. Synthetic authenticated smoke could not proceed because IAM signing returned HTTP 403; no synthetic account was created. Authenticated creation and publication remain pending device checks.
- Final separate ARKit preparation, Unity iOS export, and automatically signed Xcode Debug build succeeded. The exported player includes the hatch shader/material and all three creator illustrations. Logs: `/tmp/tagtag-creator-final-prepare.log`, `/tmp/tagtag-creator-final-export.log`, `/tmp/tagtag-creator-final-xcode.log`.
- Installed over the existing app on **Dawg., iPhone 15 Pro Max**, UDID `00008130-001420500E41001C`, using CoreDevice; installation and launch of `com.kenk.tagtag` both exited 0. No uninstall or data reset. Logs: `/tmp/tagtag-creator-final-install.log`, `/tmp/tagtag-creator-final-launch.log`. This establishes installation and launch, not interactive device acceptance.

Follow the [device test guide](CREATOR_SCAN_TEST_GUIDE.md). Native creation, Apple Intelligence, real AR tracking, and end-to-end publication are **not yet verified** for this revision.

## Device bug follow-up — 2026-09-26

- Image Playground now reads its temporary image before dismissal and copies the byte buffer before queuing Objective-C work. This addresses two source-level lifetime faults behind the reported unreadable-image error; actual generation still needs a device retry.
- Sheet backgrounds now reach the bottom edge, with the home-indicator inset inside the paper padding. Keyboard avoidance remains.
- The user clarified publishing was blocked by unclear multi-angle scanning guidance. Camera and note UI now explain the required motion; Continue scanning retains draft text.
- New maps include a shared Original spot photo, with a discovery thumbnail and enlarged saved-photo sheet. The photo does not bypass AR recovery or collection. Old v1 maps remain readable without photos; v2 maps require the updated app on both phones. No backend changes or deployment were needed.
- Swift 5 type-check against the iPhoneOS SDK passed. Separate ARKit preparation, Unity iOS export and automatically signed Xcode Debug build passed. Logs: `/tmp/tagtag-device-fixes-prepare.log`, `/tmp/tagtag-device-fixes-export.log`, `/tmp/tagtag-device-fixes-xcode.log`. No test suites were added or run in this follow-up.
- CoreDevice installed the app over the existing installation on **Dawg., iPhone 15 Pro Max**, UDID `00008130-001420500E41001C`; install and launch of `com.kenk.tagtag` exited 0. Logs: `/tmp/tagtag-device-fixes-install.log`, `/tmp/tagtag-device-fixes-launch.log`. Physical interaction acceptance remains pending; installation is not evidence of successful Image Playground generation or two-phone recovery.

Use the updated [device test guide](CREATOR_SCAN_TEST_GUIDE.md), including shared preview and sheet checks.


## Required login refresh — 2026-09-26

- **178 Edit Mode and 19 Play Mode tests passed.** Regression coverage includes the required sign-in gate, session-loss privacy, cancellation/retry, silent video looping, pause/resume, reduced motion, decoder fallback, and player/texture cleanup.
- Separate ARKit preparation, Unity iOS export, and unsigned device Xcode Debug build passed. The final IL2CPP Simulator export and Xcode build also passed.
- Login was installed and launched in **iPhone 17 Pro / iOS 26.2** and **iPhone SE (3rd generation) / iOS 18.5** simulators. Standard, compact, dark appearance, and enlarged-text captures are linked in the [verification record](verification/login/README.md). Changing pixels across native captures confirm video playback.
- Physical-device provider authentication, playback, and cancellation are **not verified** for this revision. The verification record includes the manual steps. No physical-device installation or account sign-in was performed.


## Combined main installation — 2026-09-26

- Merged login with the current Home/Explore UI in `53dfc4f`, then integrated map accuracy fix `8041ecb` as `e1c26f2`. The signed installed binary was built from `e1c26f2`; subsequent commits are documentation only.
- Combined validation: **198 Edit Mode passed**, **72 backend passed / 1 emulator-only skipped**. The login/current-UI merge passed **23 Play Mode tests** before the map policy cherry-pick; the map changes then passed the full combined Edit Mode/backend suites.
- Preserved map mounting before GPS resolves, map target/recenter behavior, camera selected-artwork touch blocking, and required sign-in. Initialized restored placement identity before the first controller update so an existing map selection is not cleared as an account transition.
- Separate ARKit preparation, fresh Unity iOS export, and signed Xcode Debug build succeeded with development team `5Y6QUA9GA6`.
- CoreDevice installed over the existing app on **Dawg., iPhone 15 Pro Max**, UDID `00008130-001420500E41001C`, and launched `com.kenk.tagtag`; both commands exited 0. No uninstall, data reset, or sign-in bypass was performed.
- Logs: `/tmp/tagtag-login-work/main-prepare.log`, `main-export.log`, `main-signed.log`, `main-install.log`, and `main-launch.log`. Integrated test results: `integrated-edit.xml`, `integrated-backend.log`, and `merge-play-final.xml` in that directory.
- Installation and launch are verified. The user must sign in normally and open Explore for interactive map verification; the map owner has filtered location logging active. Actual provider authentication and physical-device video/Explore acceptance remain pending.

- Latest-main reinstall requested after branch cleanup: rebuilt `72f961b` with separate ARKit preparation and Unity export, then signed Xcode Debug build; all succeeded. CoreDevice installation and launch on Dawg (iPhone 15 Pro Max) both exited 0, preserving existing data. Logs: `/tmp/tagtag-latest-prepare.log`, `/tmp/tagtag-latest-export.log`, `/tmp/tagtag-latest-signed.log`, `/tmp/tagtag-latest-install.log`, `/tmp/tagtag-latest-launch.log`. Interactive Explore verification is still pending.

## Map-confirmed publishing — 2026-09-26

When a fresh measured fix remains worse than 100 m for three seconds, publishing
opens a native MapKit confirmation screen for fixes up to 5 km. The user moves
the map under the pin and selects **Use this spot**. Cancel retains the captured
AR snapshot and note. Denied permission, absent/stale fixes and uncertainty over
5 km still cannot publish. Confirmed coordinates remain separate from the real
measurement and do not relax recovery or collection checks.

- Red: three DeviceLocation cases failed for approximate fallback, improvement
  during the short wait, and reduced-accuracy permission; two backend cases
  rejected confirmed publications before implementation.
- Unity Edit Mode: 207/207 passed (`/tmp/tagtag-confirm-final.xml`). Includes
  preserved cancellation/retry, pause handling and explicit confirmation flags
  to handle Unity's zero-valued serialization of null nested objects.
- Backend: 79 passed, one emulator test skipped, zero failed
  (`/tmp/tagtag-confirm-backend-final.log`). Tests cover fresh approximate
  confirmation, bounds, unchanged collection precision, pin immutability, authenticated legacy-operation recovery and
  legacy/Unity-default request compatibility.
- Native Objective-C++ compiled for device and simulator. Unity iOS export and
  signed Debug Xcode build succeeded (`/tmp/tagtag-confirm-export.log`,
  `/tmp/tagtag-confirm-xcode.log`).
- A temporary native harness used the production picker source on iPhone 17 Pro
  and iPhone SE (3rd generation) simulators. Reviewed
  [compact](verification/location-confirmation/compact.png) and
  [dark](verification/location-confirmation/dark.png) captures. A programmatic
  native check verified an outside-area pin disables confirmation and moving
  back inside enables it and returns the selected coordinate. Further checks
  verified the saved pin remains fixed while panning and cancellation during
  presentation returns a cancellation result. This does not
  verify the complete Unity/AR publication journey on physical hardware.

GPT-6-Astra/high performed a read-only Herdr review. Precision-transition and
legacy GPS-drift retry blockers plus stale picker seeds were fixed and re-reviewed
with no remaining actionable blockers.

TestFlight 0.1.0 (1) remains the earlier `01857d3` snapshot and does not contain
this fallback. Physical map-confirmed publication succeeded in the follow-up below. Subsequent
two-device discovery remains pending.

Delivered source `90398c8` as a signed Debug build to Dawg. (iPhone 15 Pro Max)
on 2026-09-26 with `devicectl`; installation succeeded. Automatic launch was
initially rejected because the phone was locked. After the owner unlocked it,
`devicectl` launched tagtag successfully. API `tagtag-api-00011-gut` serves 100% traffic after tagged/public health
and authentication checks. The physical map-confirmed publication follow-up below succeeded.

Physical follow-up at 14:56 JST: on Dawg., the owner published through the new
map-confirmation flow. Device logs show permission 22 ms, AR capture 179 ms,
location acquisition 3,023 ms, map confirmation 7,394 ms (including owner input),
prepare 972 ms, upload 4,706 ms, final location check 280 ms, and successful
finalization 927 ms. No location timeout occurred in this attempt. This verifies
one physical publication; it does not establish performance across environments
or verify two-device recovery/collection. The filtered stage log is
[publication stages](verification/location-confirmation/device-publish.log).
