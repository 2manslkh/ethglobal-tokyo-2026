# Repository Guidelines

## Project Structure & Module Organization

This repository has a Unity starter project. `Assets/` holds project content; `Packages/` and `ProjectSettings/` hold Unity configuration. `README.md` describes the proposed AR sticker hunt and links to Figma. `docs/handwritten-notes/` contains planning photos. Document added services or smart contracts in the README.

## Build, Test, and Development Commands

Open the repository root in Unity Hub with Editor `6000.5.5f1` and iOS Build Support. Read the brief with `cat README.md` and inspect tracked files with `git ls-files`. Export iOS with `/Applications/Unity/Hub/Editor/6000.5.5f1/Unity.app/Contents/MacOS/Unity -batchmode -nographics -quit -projectPath "$PWD" -buildTarget iOS -executeMethod BuildIos.Build -logFile /tmp/stickerhunt-unity.log`. Check the Xcode build with `xcodebuild -project Build/iOS/Unity-iPhone.xcodeproj -scheme Unity-iPhone -configuration Debug -sdk iphoneos -destination 'generic/platform=iOS' -derivedDataPath Build/DerivedData CODE_SIGNING_ALLOWED=NO build`. See README for signing, installation, and launch commands. No automated test command is configured; manually open the app and tap **Explore nearby** to check its response.

## Coding Style & Naming Conventions

Use descriptive names that match the app's concepts: stickers, locations, collections, and discovery. For future Unity C# code, use four-space indentation, `PascalCase` for types and public members, and `camelCase` for local variables and private fields unless the project's adopted formatter specifies otherwise. Keep scenes, scripts, and art assets in clearly named folders under `Assets/`. Keep documentation headings short and links current.

## Testing Guidelines

There is no test framework or coverage target in this repository. Add Unity Test Framework tests when gameplay or location logic is introduced, placing them in clearly named Edit Mode or Play Mode test assemblies. Name tests for the behavior they verify, and include the test command and results in the pull request. For AR features that require a device, record the device and manual verification steps.

## Commit & Pull Request Guidelines

Recent commits include `feat: update README` and `docs: add figma link`, alongside earlier short descriptive messages. Prefer a concise `type: summary` subject, such as `feat: add sticker placement`, using `docs`, `fix`, or `test` where appropriate. Make each commit atomic: include one coherent change and its related documentation or tests. Commit completed work and push it to the remote whenever possible. Pull requests should explain the change, link the relevant issue or design, list verification performed, and include screenshots or a short capture for visible Unity or AR changes. Call out required configuration without committing secrets.

## Versioning

Use Semantic Versioning for releases: `MAJOR.MINOR.PATCH`. Increment `MAJOR` for incompatible changes, `MINOR` for backward-compatible features, and `PATCH` for backward-compatible fixes. Tag releases as `vX.Y.Z` and record the version in the release notes once a releasable application exists.
