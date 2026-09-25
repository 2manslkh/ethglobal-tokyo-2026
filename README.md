# tagtag

Leave a little discovery. Find one worth keeping.

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

## Verification

Automated tests cover book pagination, swipe/tap distinctions, collection ordering, map visibility, AR gates, API authorization, idempotent operations, and failure paths. AR recovery and native sign-in require physical devices. Follow [device verification](docs/DEVICE_VERIFICATION.md); record results before claiming the shared journey works.

An AR tap plus a server discovery session is a gameplay gate, not cryptographic proof of presence. World-map recovery depends on recognizable surroundings. The server never includes full notes in nearby summaries.
