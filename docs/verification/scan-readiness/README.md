# Verified scan readiness

STICK and the full yellow ring now require a successfully serialized ARKit map
for the current placement, current tracking, and a sticker visible in live camera
imagery. Capture reuses those bytes instead of requesting another map after the
button is tapped. Maps expire after ten seconds and refresh after five; failures
retry after two seconds. Tracking loss, placement changes, and cancellation
invalidate readiness. These checks establish save readiness, not guaranteed
future relocalization on another phone.

## Checks

- Unity iOS Edit Mode: 273/273 passed, including native request status rejection,
  byte reuse, expiry, cancellation, changed placement, and failed refresh.
  Results: `/tmp/tagtag-map-readiness-tests.xml`.
- Graphics-enabled `PaperVisualTests`: eleven other cases passed; the focused
  `CameraInventoryFlowUsesFullScreenAndExplicitPlacement` passed after updating
  its obsolete twelve-preset inventory assertions. It verifies the full ring and
  enabled button return to incomplete/disabled when validation is lost.
  Results: `/tmp/tagtag-map-readiness-ui.xml` and
  `/tmp/tagtag-map-readiness-ui-focused.xml`.
- Inspected simulated-camera captures:
  [scanning](scanning.png), [ready](ready.png). These do not show physical AR.
- Read-only native review: GPT-6-Sol, high thinking, in Herdr's **Scan readiness**
  tab. Coordinator owned all edits, Unity builds, installation, and Git writes;
  reviewer owned no files. No remaining correctness finding. Native serialization
  and copying remain on Unity's main thread; device responsiveness needs checking.

## Device check

On the updated iPhone build, place a sticker in the low-detail scene that previously
produced `ErrorInsufficientFeatures`. Check that STICK stays disabled and the ring
incomplete until successful validation. Scan surrounding edges and objects, keep
the sticker in view, and capture once ready. Then check that moving/resizing the
placement or interrupting tracking disables capture until validation succeeds
again. Watch for pauses while background checks refresh the map.

Physical results are recorded in [device verification](../../DEVICE_VERIFICATION.md).
