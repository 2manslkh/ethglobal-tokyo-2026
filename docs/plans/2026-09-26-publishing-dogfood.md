# Publishing and control feedback

The owner reports slow publishing followed by an inaccurate-location error, unwanted button outlines and content movement on hover/tap, and an understated collected count. They are unsure whether Precise Location is enabled.

## Scope

Diagnose the real location/publication path with deterministic failing tests, then reduce avoidable waits and distinguish permission/precision issues from weak or stale fixes. Preserve backend accuracy/freshness checks, AR mapping gates, saved drafts, and publication idempotency. Keep button geometry fixed across pointer states, retain a visible keyboard cue without pointer outlines, and emphasize the collected number within the existing paper layout.

## Ownership

Reuse `/private/tmp/tagtag-paper-ui`, branch `fix/publishing-dogfood`, based on `19b9887`. Preserve the focused Herdr tab.

- `tagtag-publish`, Publishing tab: GPT-6-Sol, high thinking; exclusively Services and new location-specific native bridge files. Diagnosis and tests before fixes.
- `tagtag-controls`, Button polish tab: GPT-6-Sol, high thinking; exclusively UI and Resources/Tagtag/UI.
- Coordinator: shared Core contracts, Application tests/lifecycle integration, build configuration, documentation, all serial Unity/Xcode/device runs, and Git integration.
- `tagtag-dogfood-review`, Dogfood review tab: GPT-6-Astra, high thinking; read-only review of location lifecycle, publication sequencing/idempotency, and UI integration.

Git writes use exclusive windows and explicit owned paths. Commit verified stages, integrate and push, then record the owner's device retest without inferring unreported outcomes.

## Verification

Observe red location/publication tests and actual mounted button-state geometry before fixes. Run the full Unity Edit Mode and graphics Play Mode suites, inspect normal/compact count and pointer-state captures, run the built player, prepare ARKit/export iOS separately, and compile unsigned/signed. Install on Dawg. and request a publishing/interaction retest. No backend contract migration or accuracy relaxation is planned.

## Review follow-up

Initial integrated Edit Mode verification passed 108/108. Rendered Play Mode passed 7/8; the failed Settings assertion selected a hidden screen notice rather than the active sheet. Actual capture inspection also found that a child focus marker broke text-only Button sizing, so the cue is being changed to color-only feedback.

Review requires active publication drafts to stay immutable, a separately persisted editable draft before Settings recovery, cancellation of capture waits and late publication continuations on suspension/disposal, request-scoped location cleanup, and retained location recovery through account resume. These are bounded repairs to the same publishing/recovery path, with regression coverage before final builds.

The user supplied the exact tagline **Find your places, Collect your moments**. Use it in project copy and the empty-book invitation while retaining the Explore nearby action.

The six review repairs are implemented. Fresh pre-integration checks passed 120/120 Edit Mode and 8/8 graphics Play Mode; source review found no remaining concrete blocker. The parallel `feat/simplify-camera-navigation` task will be merged before the final player/native build, preserving both sets of changes and rerunning affected checks.

Integration completed with navigation commits d638da6, a5b6714 and f79d599, preserving both branches' tests and documentation. Combined verification: Edit Mode 120/120, graphics Play Mode 8/8, fresh standalone player 8/8, ARKit preparation/export, unsigned Xcode compilation and development signing. Fifteen built-player captures and XML results are retained under docs/verification/publishing-dogfood. USB installation on Dawg completed successfully without uninstalling the existing app. The owner’s publishing and interaction retest remains pending.
