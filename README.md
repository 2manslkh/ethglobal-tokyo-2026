# tagtag

Find your places, Collect your moments

tagtag is an iPhone AR sticker app starring **Taggi**. Its paper-white interface follows [DESIGN.md](DESIGN.md), with four die-cut mascot presets and three tabs:

- **Home:** a personal sticker book with 20 spaces per page, collection details, and account controls.
- **STICK:** place a sticker on a tracked surface, write a public teaser and private note, then publish. Recover a nearby sticker's AR map and tap it within three metres to collect a copy and reveal its note.
- **Explore:** a native Apple street map with sticker pins, clusters, and teaser sheets.

Browsing is available before sign-in. Publishing and collecting require Apple or Google sign-in. Collection leaves the original sticker available. Account-specific collections are cached offline; removed content is updated at the next successful sync.

## Stack

Unity `6000.5.5f1`, UI Toolkit, AR Foundation/ARKit, native MapKit and AuthenticationServices, Firebase Authentication, Firestore, private Cloud Storage, and a Node.js API on Cloud Run. This release uses app-based collections. See [PRODUCT.md](PRODUCT.md), the [implementation plan](docs/plans/2026-09-26-tagtag.md), and [API contract](docs/plans/tagtag-api-contract.md).

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

The startup regression lives in `Assets/Tagtag/Application/PlayModeTests/`. Use `-testPlatform PlayMode -testFilter Tagtag.Tests.StartupTests` with the Unity test command to check the real entry scene and rendered Home pixels. For a macOS player check, use `-buildTarget StandaloneOSX -testPlatform StandaloneOSX`; its test-only callback also writes `tagtag-startup-result.xml` beside the screenshot in `Application.temporaryCachePath` if the player cannot return results to the editor. Editor rendering alone does not verify that player builds contain the required UI text resources.

The paper UI uses a persistent shell and updates mounted controls when application state changes. Shared components, fonts, and motion are self-contained; [source provenance and font licenses](Assets/Tagtag/UI/SOURCE_PROVENANCE.md) document their adaptation. Camera presentation is independent of tracking: an opaque paper cover remains until AR reports displayable live imagery and returns after interruption. On iOS, the AVFoundation bridge is the authority for camera permission; Unity may strip its webcam authorization implementation from AR-only builds. Nearby lookup has an independent loading state so location acquisition does not lock navigation.

Publishing checks iOS app-level Location and Precise Location authorization before AR map capture. Select a sticker to start acquiring a fresh fix while placing and writing; during publishing, location acquisition overlaps AR map capture, and an already-running location session restarts if its cached measurement is stale. Publishing accepts a measured fix within 100 metres and 30 seconds, and reuses it for finalization while it remains fresh. Nearby discovery continues to require 50-metre accuracy. If precision is disabled, the note sheet offers Open Settings. Enable **Settings > Apps > tagtag > Location > Precise Location**, return, and retry. Weak indoor GPS can still require moving near a window or outdoors. Publishing shows map, location, preparation, upload, and finalization progress. Stage logs include elapsed time without coordinates or note content.

For mounted-view and visual regression checks, run Unity without `-nographics`, using `-buildTarget StandaloneOSX -testPlatform PlayMode -testFilter Tagtag.Tests.PaperVisualTests`. The fixture checks draft/caret/focus/scroll continuity, sign-in return, duplicate activation, and synced collection details. It captures normal and compact layouts under `Application.temporaryCachePath/tagtag-paper-review`; its standalone result is `tagtag-paper-visual-result.xml`. The neutral background in the live-camera fixture is simulated imagery. Native maps, the software keyboard, permissions, and AR still require simulator or physical-device checks.

STICK uses a full-screen camera with Close and a four-pose sticker inventory. Choose a design, scan for a yellow surface outline, then tap to attach it. Drag on the original surface, pinch to resize, and twist to rotate. The Home profile icon opens sign-in or account settings. The native iOS status bar stays visible with a transparent background over the camera and a paper backing on other screens. Write note becomes available after attachment; publishing requires a tracked anchor and a usable ARKit world map. ARKit's Extending and Mapped states both qualify; Extending allows capture while ARKit continues mapping the current area. The bundled [sticker material and shader](Assets/Tagtag/AR/SOURCE_PROVENANCE.md) keep artwork and transparent edges available in stripped player builds.

Run graphics-enabled placement and rendering checks with `-buildTarget StandaloneOSX -testPlatform PlayMode -testFilter Tagtag`. Repeat with `-testPlatform StandaloneOSX` to verify shader inclusion in a built player. The test callback writes the complete suite to `Application.temporaryCachePath/tagtag-player-result.xml`, and the real sticker render to `tagtag-sticker-render.png`.

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
