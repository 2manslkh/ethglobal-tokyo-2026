# AR sticker rendering and placement flow

User feedback: the camera now opens, but a placed sticker is a magenta square. Hide the generic header and bottom navigation in camera mode, expose a sticker inventory button, and copy the placement flow from `~/Desktop/coding-projects/sticker-app`.

## Scope

Adapt the source camera flow: floating Close, 88-point STICK inventory action, a four-design Taggi drawer, selected artwork/name, tracking/surface guidance, yellow tracked boundaries, short-tap placement, then drag/pinch/rotate and accessible size/rotation controls. Keep tagtag note composition and explicit authenticated publishing. Camera Close returns to the entry destination. Four existing poses remain the available placement inventory; collected notes and backend contracts do not change. No native glass, custom sticker creator, or source app private-save system is imported.

The paper cover remains until camera imagery is live. All overlays block camera input. UI Toolkit owns placement pointer classification, while AR validates screen bounds, tracked polygon hits, and attachment/movement. Collection retains its existing direct-tap, distance, recovery, and authentication gates.

## Assignment

Use the clean isolated `/private/tmp/tagtag-paper-ui` worktree, branch `fix/ar-sticker-flow`, based on `31536b3`. Preserve Herdr focus.

- `tagtag-camera-pass`: GPT-6-Sol, high thinking. Own AR implementation/tests and new `Resources/Tagtag/AR` material/shader assets. Diagnose with a render regression before fixing; adapt source plane guidance and placement.
- `tagtag-ui-pass`: GPT-6-Sol, high thinking. Own UI implementation/tests and UI stylesheet resources. Adapt source gesture recognizers and camera/drawer flow; record provenance.
- Coordinator: Core contract, existing service/Application test doubles, mounted camera-flow tests, documentation, all Unity/Xcode/device verification, and serialized Git integration.
- `tagtag-paper-review`: GPT-6-Astra, high thinking. Read-only correctness review after integration.

## Interface

Extend `IArExperience` with `HasPlacementSurface`, `HasPlacementPreview`, `PlacementBusy`, `PlacementWidthMeters`, `PlacementRotationDegrees`, `SetCameraInteraction(Rect,bool blocked)`, `Place(Vector2)`, and `AdjustPlacement(float,float,Vector2?)`. Rect/points use bottom-left screen pixels; size uses metres and rotation uses degrees. Presentation/placement readiness stays independent of `CanPublish`.

## Verification

Reproduce missing/magenta artwork through the real material creation/render path in a player, verify alpha silhouette and visible ink/paper after repair. Observe red camera-shell/inventory behavior tests before implementation. Run Edit Mode, mounted Play Mode and built-player rendering checks. Inspect normal/compact enlarged-text camera/drawer/note states. Export ARKit in separate prepare/export invocations, compile unsigned and signed, install on Dawg. without uninstalling, and ask the owner to verify visible artwork, placement gestures, inventory, and Close. Record hardware limits and results in `docs/DEVICE_VERIFICATION.md`. Commit verified atomic stages with exclusive index windows and push the integrated branch.
