# Device feedback follow-up

The owner reported a false camera-permission denial and blocked navigation while Explore awaited a precise location. The approved visual changes are Taggi roof/star/map navigation artwork and Shadows Into Light for display/title roles only. Instrument Sans remains body/control/navigation/native cluster text.

## Ownership and execution

Reuse isolated branch `fix/device-ui-followup` in `/private/tmp/tagtag-paper-ui`, based on `5987acb`. Herdr tabs preserve the focused tab.

- UI: `tagtag-ui-pass`, GPT-6-Sol, high thinking. Owns UI and Fonts/UI Resources; test-first status, typography, and navigation integration.
- AR: `tagtag-camera-pass`, GPT-6-Sol, high thinking. Owns AR implementation/tests and the new native camera bridge.
- Review: `tagtag-paper-review`, GPT-6-Astra, high thinking. Read-only integrated correctness review.
- Coordinator: Core/Services loading state and tests, native export dependency, Navigation artwork/provenance, docs, Unity/Xcode/device checks, serialized Git integration and push.

## Evidence and implementation

The shipped `Classes/Preprocessor.h` defines `UNITY_USES_WEBCAM 0` and `UNITY_USES_MICROPHONE 0`. Unity's exported `AVCapture.mm` compiles out both native authorization lookup and request, returning denied even when iOS Settings grants camera access. Query/request AVFoundation directly; use native authorization as authority and Unity's result only for diagnostics. Unknown status must remain a recoverable failure, never a permission denial.

Nearby lookup formerly used global `busy`, which blocked navigation for the location provider's 20-second precise-fix timeout. Add independent `nearbyLoading`, cancel location waiting on departure, and ignore stale network results. Preserve accuracy, discovery, and server requirements. The three controller regression tests were observed failing before the change and passing afterward.

## Delivery checks

Run all Edit Mode and mounted UI/actual-scene Play Mode tests. Inspect updated screenshots for handwritten titles, readable control text, and new Taggi icons. Prepare ARKit separately, export iOS, confirm native bridge in Xcode Sources, then compile unsigned and signed. Install on Dawg. without uninstalling. Record device-owner results and remaining physical checks in `docs/DEVICE_VERIFICATION.md`. Commit only verified owned paths in serialized atomic stages; push integrated changes.

## Completed checkpoints

- `7e0ead1`: native camera authorization and regressions.
- `337fd85`: independent nearby lookup, cancellation/resumption, and seven service regressions.
- `1cbe1c4`: generated Taggi navigation artwork and exact provenance.
- `b774e7a`: handwritten titles, Instrument controls, nav artwork integration, loading presentation, and compact shell repair.
- `848668b`: mounted UI resource/action/geometry regressions.
- `6bc01fc`: explicit AVFoundation export linkage.

Final Edit Mode passed 79/79; Play Mode passed 2/2; both standalone player callbacks passed. Export, unsigned compilation, signing, and USB installation succeeded. The device owner confirmed “Camera opens and navigation works”; detailed permission/interruption cases remain unverified. Detailed logs and capture links are in `docs/DEVICE_VERIFICATION.md`.
