# tagtag

Find your places, Collect your moments

tagtag is an iPhone AR sticker app starring **Taggi**. Its paper-white interface follows [DESIGN.md](DESIGN.md), with twelve die-cut mascot presets and three tabs:

- **Home:** a personal sticker book with 20 spaces per page, collection details, and account controls.
- **STICK:** place a sticker on a tracked surface, capture the spot, write a private note, then publish. Recover a nearby sticker's AR map and tap it within three metres to collect a copy and reveal its note.
- **Explore:** a native Apple street map with sticker pins, clusters, and teaser sheets.

Apple or Google sign-in is required to enter the app. Returning authenticated sessions open Home; signing out or losing a session returns to login. Collection leaves the original sticker available. Account-specific collections are cached offline; removed content is updated at the next successful sync.

## Stack

Unity `6000.5.5f1`, UI Toolkit, AR Foundation/ARKit, native MapKit and AuthenticationServices, Firebase Authentication, Firestore, private Cloud Storage, and a Node.js API on Cloud Run. Collections can automatically mint transferable ERC-721 souvenirs on Ethereum Sepolia through Thirdweb embedded wallets and a backend mint worker. NFT rollout is disabled until configured and verified; existing book entries are not backfilled. The next rollout uses [isolated NFT staging](docs/NFT_STAGING.md) and Firebase-hosted public artwork. See [NFT setup](docs/NFT_SETUP.md), the [contract](contracts/README.md), [PRODUCT.md](PRODUCT.md), and [API contract](docs/plans/tagtag-api-contract.md).

## Development

Open the repository root in Unity Hub with Editor `6000.5.5f1` and iOS Build Support. Project content is under `Assets/Tagtag/`; native bridges are in `Assets/Plugins/iOS/`. Generated exports, build caches, and local settings are ignored by Git.

Run behavior tests from the repository root:

```sh
/Applications/Unity/Hub/Editor/6000.5.5f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -nographics -projectPath "$PWD" -buildTarget iOS \
  -runTests -testPlatform EditMode -testResults /tmp/tagtag-tests.xml \
  -logFile /tmp/tagtag-tests.log
```

Backend commands are documented in [backend/README.md](backend/README.md). Live infrastructure and maintenance are recorded in [deployment](docs/DEPLOYMENT.md).

Login uses the supplied Taggi video as a silent looping background, with a matching poster for loading and playback failure. The transparent header and footer show the tagline, sign-in buttons, and in-app Privacy Policy and Terms & Conditions sheets. Login fits without scrolling; policy content scrolls inside its sheet. Provider cancellation and errors remain on the login screen for retry.

The startup regression lives in `Assets/Tagtag/Application/PlayModeTests/`. Use `-testPlatform PlayMode -testFilter Tagtag.Tests.StartupTests` with the Unity test command to check the real entry scene and rendered login pixels. For a macOS player check, use `-buildTarget StandaloneOSX -testPlatform StandaloneOSX`; its test-only callback also writes `tagtag-startup-result.xml` beside the screenshot in `Application.temporaryCachePath` if the player cannot return results to the editor. Editor rendering alone does not verify that player builds contain the required UI text resources.

Text uses Standard sizing and animations stay enabled; legacy reading and motion preferences are ignored.

The paper UI uses a persistent shell and updates mounted controls when application state changes. Shared components, fonts, and motion are self-contained; [source provenance and font licenses](Assets/Tagtag/UI/SOURCE_PROVENANCE.md) document their adaptation. Camera presentation is independent of tracking: an opaque paper cover remains until AR reports displayable live imagery and returns after interruption. On iOS, the AVFoundation bridge is the authority for camera permission; Unity may strip its webcam authorization implementation from AR-only builds. Nearby lookup has an independent loading state so location acquisition does not lock navigation. **Find in AR** opens the camera before waiting for location or the saved AR map, with progress and retry guidance in the camera view. Close stays available while discovery loads: leaving the camera cancels the search, and late results cannot reopen AR or overwrite a newer search. Discovery still requires a precise location fix.

Tap the circular **STICK** control after Scan ready to capture a camera-only Original spot photo and AR world map. The Note sheet then contains only **Your note**, Close, and Publish. New drafts use the public place **Sticker spot** and teaser **Find this sticker to read its note**; existing saved draft metadata stays intact. The private note never becomes a public clue. Closing and reopening the sheet reuses the captured spot until the placement or artwork changes. Publishing checks iOS app-level Location authorization after capture. Select a sticker to start acquiring a fresh fix while placing; an already-running location session restarts if its cached measurement is stale. Publishing automatically accepts a measured fix within 100 metres and 30 seconds. After three seconds without that precision, a fresh approximate fix up to 5 km opens **Confirm this spot**: move the map under the pin and tap **Use this spot**. The API stores the confirmed pin separately from the measured fix. Cancel preserves the captured placement and note for retry. Finalization reuses a fresh measurement or refreshes it. Explore browsing accepts a fresh approximate fix within 5 km accuracy and labels it as approximate; AR recovery and collection still require 50-metre accuracy. Reduced-accuracy permission also supports map confirmation. Denied location access still offers Open Settings. A stale, unavailable, or more-than-5-km measurement cannot seed confirmation; the draft remains saved. Publishing shows location, preparation, upload, and finalization progress. Stage logs include elapsed time without coordinates or note content.

For mounted-view and visual regression checks, run Unity without `-nographics`, using `-buildTarget StandaloneOSX -testPlatform PlayMode -testFilter Tagtag.Tests.PaperVisualTests`. The fixture checks draft/caret/focus/scroll continuity, sign-in return, duplicate activation, and synced collection details. It captures normal and compact layouts under `Application.temporaryCachePath/tagtag-paper-review`; its standalone result is `tagtag-paper-visual-result.xml`. The neutral background in the live-camera fixture is simulated imagery. Native maps, the software keyboard, permissions, and AR still require simulator or physical-device checks.

STICK uses a full-screen camera with Close and a sticker inventory. Choose a design, scan for a yellow pencil-hatched surface, then tap to attach it. Drag on the original surface, pinch to resize, and twist to rotate. The Home profile icon opens sign-in or account settings. The native iOS status bar stays visible with a transparent background over the camera and a paper backing on other screens. With artwork selected, the circular STICK control becomes the capture action and enables after Scan ready and capture readiness. Capture requires a tracked anchor and a usable ARKit world map. ARKit's Extending and Mapped states both qualify; Extending allows capture while ARKit continues mapping the current area. Publish reuses that captured map and photo even if live tracking later drops; saved publication retries continue to use their stored snapshot. The bundled [sticker material and shader](Assets/Tagtag/AR/SOURCE_PROVENANCE.md) keep artwork and transparent edges available in stripped player builds.

New publications include a camera-only Original spot photo to help nearby finders match the surroundings. A thumbnail opens an enlarged saved-photo sheet during discovery; it never replaces AR recovery or collection checks. The bounded JPEG (maximum 1024-pixel edge and 512 KiB) travels inside private map envelope v2, using the existing map access, expiry and deletion lifecycle. The new app still reads v1 maps without photos; both publishing and finding phones need this update for v2 publications. See the [device test guide](docs/CREATOR_SCAN_TEST_GUIDE.md).

Run graphics-enabled placement and rendering checks with `-buildTarget StandaloneOSX -testPlatform PlayMode -testFilter Tagtag`. Repeat with `-testPlatform StandaloneOSX` to verify shader inclusion in a built player. The test callback writes the complete suite to `Application.temporaryCachePath/tagtag-player-result.xml`, and the real sticker render to `tagtag-sticker-render.png`.

Successful publication and new AR collection open a full-screen sticker celebration with a short artwork reveal, drawn yellow stars, and one iOS success haptic when the artwork becomes available. Tap **Keep exploring** to return to the camera or **Read the note** to open the collected sticker. Rewards do not replay on refresh or when reopening a sticker. See [celebration verification](docs/verification/celebration/README.md).

## iPhone build

Prepare ARKit in a separate invocation so its loader settings are imported before export:

```sh
/Applications/Unity/Hub/Editor/6000.5.5f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -nographics -quit -projectPath "$PWD" -buildTarget iOS \
  -executeMethod BuildIos.PrepareArKit -logFile /tmp/tagtag-prepare.log
/Applications/Unity/Hub/Editor/6000.5.5f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -nographics -quit -projectPath "$PWD" -buildTarget iOS \
  -executeMethod BuildIos.Build -logFile /tmp/tagtag-export.log
```

The entry scene is `Assets/Scenes/Tagtag.unity`, the bundle is `com.kenk.tagtag`, and the export is `Build/iOS`. Public service configuration belongs in `Assets/Resources/Tagtag/ServiceConfiguration.json`: `apiBaseUrl`, `firebaseApiKey`, `googleClientId`, and `googleReversedClientId`. Firebase API keys identify the project; authorization is enforced by Firebase tokens and the API. Never bundle service-account keys, OAuth client secrets, or signing private keys.

Build with automatic development signing and a provisioning profile that supports Sign in with Apple:

```sh
xcodebuild -project Build/iOS/Unity-iPhone.xcodeproj -scheme Unity-iPhone \
  -configuration Debug -sdk iphoneos -destination 'generic/platform=iOS' \
  -derivedDataPath Build/DerivedData -allowProvisioningUpdates \
  DEVELOPMENT_TEAM=5Y6QUA9GA6 CODE_SIGN_STYLE=Automatic build
```

For a compilation-only check, replace signing arguments with `CODE_SIGNING_ALLOWED=NO`. Connect, unlock, and trust the iPhone; enable Developer Mode if requested. Inspect its identifier with `xcrun devicectl list devices`, then:

```sh
xcrun devicectl device install app --device YOUR_DEVICE_ID \
  Build/DerivedData/Build/Products/Debug-iphoneos/tagtag.app
xcrun devicectl device process launch --device YOUR_DEVICE_ID com.kenk.tagtag
```

If CoreDevice cannot see a trusted USB phone, the verified installation fallback is `ios-deploy --id YOUR_USB_UDID --no-wifi --bundle Build/DerivedData/Build/Products/Debug-iphoneos/tagtag.app`. Open tagtag manually if the debugging service cannot launch it. Changing signing teams may require removing the previous installation, which deletes its local data; obtain the device owner's approval first.

## iPhone simulator

Build in a new temporary review project so device ARKit settings remain intact:

```sh
scripts/build-ios-simulator.sh /private/tmp/tagtag-simulator-review
xcrun simctl list devices available
xcrun simctl boot YOUR_SIMULATOR_ID
xcrun simctl install YOUR_SIMULATOR_ID \
  /private/tmp/tagtag-simulator-review/Build/DerivedDataSimulator/Build/Products/Debug-iphonesimulator/tagtag.app
xcrun simctl launch YOUR_SIMULATOR_ID com.kenk.tagtag
```

This arm64 simulator build retains the native map and identity bridges, and disables the device-only ARKit loader in the review copy. Use it for startup and layout checks; physical iPhones are required for AR placement and shared recovery. The script accepts only a new directory under `/private/tmp` and leaves preparation, export, and Xcode logs there.

## Verification

Automated tests cover book pagination, swipe/tap distinctions, collection ordering, map visibility, AR gates, API authorization, idempotent operations, and failure paths. AR recovery and native sign-in require physical devices. Follow [device verification](docs/DEVICE_VERIFICATION.md); record results before claiming the shared journey works.

An AR tap plus a server discovery session is a gameplay gate, not cryptographic proof of presence. World-map recovery depends on recognizable surroundings. The server never includes full notes in nearby summaries.

## Home library

Home keeps the collected total beside an illustrated **Make a sticker** button. Switch between **Collected** and **My designs**: the book holds discovered stickers, while My designs shows your saved artwork newest first. An empty book shows Taggi hugging their knees and crying, with **Explore nearby** below.

Tap one of your designs for a larger preview, then **Place sticker** to open the camera or **Remove design** to confirm removal from your library. Published and collected copies keep their artwork. The selected Home view, book page, and each view's scroll position survive navigation during the session; changing accounts clears the private design presentation. Refresh keeps cached cards visible. Signing out returns to login.

Run the Home UI checks with Unity `-buildTarget StandaloneOSX -testPlatform PlayMode -testFilter Tagtag.Tests.HomeLibraryVisualTests` (graphics enabled). Captures are written under `Application.temporaryCachePath/tagtag-home-review`. Native creation, sign-in, and AR handoff still require an iPhone.

## Create stickers

Open **My Stickers** from Home or the STICK inventory. Import a photo/file, make a photo or selfie Polaroid, or open Apple Image Playground on a supported iPhone. The native editor offers full-image or square crop, optional foreground cutout with a white border, and a Polaroid caption. Preview the finished design before saving.

Designs sync to the creator's account and can be placed repeatedly. Collecting someone else's placement adds a book copy, without granting design publication rights. Removing a library design preserves published copies. The twelve bundled Taggi presets work without creating a custom design. The eight new poses are waving, heart-hugging, laughing, sleepy, surprised, cheering, shy, and thinking; artwork prompts are recorded in [Taggi presets](docs/mascot/TAGGI_PRESETS.md). New poses retain their artwork in the app and use the existing generic Taggi NFT souvenir. Deploy backend support for all twelve preset IDs before distributing the updated app. Creation drafts survive failed uploads and sign-in; use **Retry saving sticker** to continue. Source photos stay on the device; only the finished PNG is uploaded.

Image Playground is checked at runtime and has no cloud fallback. Foreground cutout requires iOS 17 or later; imports and Polaroids retain the iOS 15 minimum. Native creation uses PhotosUI, Vision, UIKit, and a Swift Image Playground bridge. The backend uses `sharp` to validate PNG pixels, remove metadata, and generate thumbnails. Limits are 5 MiB/1024 pixels per image, 20 new designs per account per day, and 100 active designs. See [the creation plan](docs/plans/2026-09-26-sticker-creation.md) and [backend API details](backend/README.md).

Deploy backend support and both `designs` Firestore indexes before installing a client with creation enabled. Update the cleanup job to the same backend image. A native image-generation or camera experience still requires physical-device verification; successful compilation does not verify Apple Intelligence availability or camera capture.

## Home and Explore updates

Home includes Collected, Your Designs, and Placed tabs. Make a sticker is inside
Your Designs. Placed shows active publications with pagination; tapping a row
opens that location in Explore without changing the phone's measured location.
The camera shows the selected artwork above its bottom dock; tap it to change
stickers before or after placing the preview.

Explore mounts its native map before GPS completes, preserves the viewport and
existing pins during refresh, and reuses successful nearby results for 60 seconds.
Tap a grouped map pin to open **Stickers here**, a scrollable chooser with artwork,
place names and teasers. Every member can be selected even at identical coordinates
or maximum zoom; selecting a row opens its usual teaser and Find in AR action.
Explicit refresh bypasses that cache. Timing logs report location acquisition,
nearby fetch, and native map mounting. Device measurements of tile/artwork readiness
and cold/warm visits are required before claiming a real-world speedup.

Deploy the updated backend and the authored pagination index in
`firestore.indexes.json` before distributing this client. The new authored query
is backward-compatible with older clients; see the
[API contract](docs/plans/tagtag-api-contract.md). No production deployment is
performed by the local implementation/build workflow.


Deploy the map-confirmation API update before installing this client. Existing
TestFlight 0.1.0 (1), built from `01857d3`, does not include this fallback.
