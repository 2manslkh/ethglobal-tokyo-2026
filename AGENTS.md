# Repository Guidelines

## Project Structure & Module Organization

This repository has the tagtag Unity app. Taggi is the mascot. `Assets/` holds project content; `Packages/` and `ProjectSettings/` hold Unity configuration. `README.md` describes the app and build workflow; `DESIGN.md` defines its visual direction. `docs/handwritten-notes/` contains planning photos. Document added services or smart contracts in the README.

## Build, Test, and Development Commands

Open the repository root in Unity Hub with Editor `6000.5.5f1` and iOS Build Support. Read the brief with `cat README.md` and inspect tracked files with `git ls-files`. Export iOS with `/Applications/Unity/Hub/Editor/6000.5.5f1/Unity.app/Contents/MacOS/Unity -batchmode -nographics -quit -projectPath "$PWD" -buildTarget iOS -executeMethod BuildIos.Build -logFile /tmp/tagtag-unity.log`. Check the Xcode build with `xcodebuild -project Build/iOS/Unity-iPhone.xcodeproj -scheme Unity-iPhone -configuration Debug -sdk iphoneos -destination 'generic/platform=iOS' -derivedDataPath Build/DerivedData CODE_SIGNING_ALLOWED=NO build`. See README for signing, installation, and launch commands. Run Edit Mode tests with the command in README. Run backend tests with `cd backend && npm test`. Prepare ARKit in a separate Unity invocation before export; see README. Record physical-device results in `docs/DEVICE_VERIFICATION.md`.

## Coding Style & Naming Conventions

Use descriptive names that match the app's concepts: stickers, locations, collections, and discovery. For future Unity C# code, use four-space indentation, `PascalCase` for types and public members, and `camelCase` for local variables and private fields unless the project's adopted formatter specifies otherwise. Keep scenes, scripts, and art assets in clearly named folders under `Assets/`. Keep documentation headings short and links current.

## Testing Guidelines

Unity Test Framework assemblies live beside their feature modules under `Assets/Tagtag/`. Add behavior tests when gameplay or location logic changes, placing them in clearly named Edit Mode or Play Mode test assemblies. Name tests for the behavior they verify, and include the test command and results in the pull request. For AR features that require a device, record the device and manual verification steps.

## Commit & Pull Request Guidelines

Recent commits include `feat: update README` and `docs: add figma link`, alongside earlier short descriptive messages. Prefer a concise `type: summary` subject, such as `feat: add sticker placement`, using `docs`, `fix`, or `test` where appropriate. Keep every commit small and atomic: include one coherent change and its related documentation or tests. Split larger work into independently reviewable commits instead of accumulating a large commit with multiple changes. Commit completed work and push it to the remote whenever possible. Pull requests should explain the change, link the relevant issue or design, list verification performed, and include screenshots or a short capture for visible Unity or AR changes. Call out required configuration without committing secrets.

## Versioning

Use Semantic Versioning for releases: `MAJOR.MINOR.PATCH`. Increment `MAJOR` for incompatible changes, `MINOR` for backward-compatible features, and `PATCH` for backward-compatible fixes. Tag releases as `vX.Y.Z` and record the version in the release notes once a releasable application exists.

## Orchestration

Use Herdr to orchestrate subagents. Give each feature its own named tab; use panes within that tab for related subtasks. Preserve the user's focused tab. Assign exclusive file ownership before parallel edits, and keep integration and Unity builds with the coordinator.

Choose the model and thinking level for each assignment and record them in the execution plan. Use GPT-6-Sol with high thinking for feature implementation, native integration, and backend work; medium for bounded changes. Reserve GPT-6-Astra with high thinking for complex architecture or security review, and GPT-6-Luna with medium thinking for straightforward documentation or mechanical edits. Honor any model explicitly requested by the user. The current UI, AR, and backend agents use GPT-6-Sol with high thinking.

Each subagent must commit completed, verified pieces frequently, following the atomic commit rules above. Stage only explicitly owned paths, inspect the staged diff, and coordinate Git writes so agents do not share the index concurrently. Report each commit and its verification to the coordinator; the coordinator pushes integrated commits after checking them.
