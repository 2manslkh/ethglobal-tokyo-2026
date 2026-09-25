# tagtag

Figma: <https://www.figma.com/design/8EuygwbCLAfBuSCuH1Yc5d/Untitled?node-id=1-21&t=6h6X6aktQUN8mjbW-1>

## Description

tagtag is an AR app built in Unity to allow users to place and find stickers in the real world. These stickers are collectible NFTs that represent unique locations for individuals. Taggi is the app's mascot.

## Problem Statement

People want to find unique curated experiences and have a desigre to experience new things all the time, but they don't know where to find them.

## Goal

To create a fun and engaging way to discover unique curated experiences.

## Techstack

1. Unity
2. Google AR Core
3. Supabase
4. Ethereum

## Unity project

Open this repository root as a project in Unity Hub with Unity Editor `6000.5.5f1`.
The committed `Assets/`, `Packages/`, and `ProjectSettings/` directories form the
project. Unity generates local `Library/`, `Temp/`, `Logs/`, and `UserSettings/`
directories on first open; these are ignored by Git. The `StickerHunt` scene
opens a discovery starter screen with Taggi's name and a working button. AR placement, locations,
Supabase, and Ethereum integration are planned but are not part of this build.

## Build for iPhone

Install Unity Editor `6000.5.5f1` with iOS Build Support and Xcode. From the
repository root, export the Xcode project with:

```sh
/Applications/Unity/Hub/Editor/6000.5.5f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -nographics -quit -projectPath "$PWD" -buildTarget iOS \
  -executeMethod BuildIos.Build -logFile /tmp/tagtag-unity.log
```

The scene is the build entry point, and the development bundle ID is
`com.kenk.tagtag`. Unity writes the Xcode project to `Build/iOS`, which is
ignored by Git. To check that the Xcode project compiles before signing:

```sh
xcodebuild -project Build/iOS/Unity-iPhone.xcodeproj -scheme Unity-iPhone \
  -configuration Debug -sdk iphoneos -destination 'generic/platform=iOS' \
  -derivedDataPath Build/DerivedData CODE_SIGNING_ALLOWED=NO build
```

For a device build, sign into an Apple Development account in Xcode and replace
`YOUR_TEAM_ID` with its team ID:

```sh
xcodebuild -project Build/iOS/Unity-iPhone.xcodeproj -scheme Unity-iPhone \
  -configuration Debug -sdk iphoneos -destination 'generic/platform=iOS' \
  -derivedDataPath Build/DerivedData -allowProvisioningUpdates \
  DEVELOPMENT_TEAM=YOUR_TEAM_ID CODE_SIGN_STYLE=Automatic build
```

Connect, unlock, and trust the iPhone, then check its identifier with
`xcrun devicectl list devices`. When it reports available, install and launch:

```sh
xcrun devicectl device install app --device YOUR_DEVICE_ID \
  Build/DerivedData/Build/Products/Debug-iphoneos/tagtag.app
xcrun devicectl device process launch --device YOUR_DEVICE_ID com.kenk.tagtag
```

For this first build, verify that the screen opens and tapping **Explore nearby**
shows “Discovery map coming soon.” No automated test framework is configured yet.
