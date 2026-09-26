# Home improvements execution

Approved: Home Collected / My designs switch; drawing Taggi creation button right of collected indicator; crying knees-hugging Taggi for zero collected; own saved design gallery with preview before placement/removal.

## Assignments

- Coordinator: assets, new Home library Play Mode tests, integration, Unity builds and verification. Main shared checkout has independent camera/inventory work; implementation uses `/private/tmp/tagtag-home-design-library`.
- `home-ui`, GPT-6-Sol high, Herdr tab Home improvements: exclusively owns `Assets/Tagtag/UI/TagtagAppView.Home.cs` (+ meta), `TagtagAppView.Screens.cs`, sheet integration edits in `TagtagAppView.cs` and `TagtagAppView.Account.cs`. Wait for coordinator tests before code; no Unity invocations. Commit only owned paths after coordinator verification and Git lock handoff.
- Fresh review: GPT-6-Sol high, read-only after implementation.

## Implementation and acceptance

Shared row retains collected count on left, illustrated Make a sticker on right. Button has readable caption and >=44-point target. Resources: `Tagtag/Home/make-sticker` and `Tagtag/Home/empty-book`.

Collected/My designs switch below row, Collected default; preserve selected view, per-view scroll and collected book page through navigation. Reset private designs/preview on user change. Crying Taggi only for empty Collected; existing invitation and Explore nearby remain.

My designs: two-column own finalized design gallery, newest first, excludes presets/foreign designs. Signed-out, empty, loading, failed artwork and refresh/retry states; cached designs remain during loading. Refresh on entry. Preview sheet with full artwork, name, Place sticker via existing SelectDesign and Remove design confirmation via existing DeleteDesign. Cancel returns to preview; removed design returns to gallery; existing published copies retained. Creation uses existing tools; draft retry unchanged.

No backend schema/public API changes. Existing camera inventory work is preserved at integration.

## Checks

New mounted Play Mode scenarios: header arrangement, empty/nonempty, switching, sorted owner-filtered gallery, cache/loading, preview/dismiss/place/remove, account changes, session navigation, compact/large text. Run existing Home/creation visual and Edit Mode tests as relevant; inspect generated screenshots. Device-only creation/AR requires device check, report separately.

## Ledger

Pre-flight: gallery consumes existing AppState.designs, RefreshDesigns, SelectDesign, DeleteDesign. Art consumes two new named Resources assets. Sheet hooks share files with separate inventory task, isolated until integration.

Baseline: 5 HomeLibraryVisualTests failed on absent Home My designs controls and the Make button left of count (2026-09-26, /tmp/tagtag-home-red.xml). Inventory work 6830928 fast-forwarded before UI implementation. Art generated, visually inspected, crying pose anatomy corrected, alpha confirmed by sips. Artifact import settings adapted from existing navigation assets.

Implementation verification: 9/9 Home Play Mode tests passed in `/tmp/tagtag-home-tests-2.xml`. Standard and compact/large-text captures inspected. Confirmed real artwork renders, caption no longer splits inside words, preview actions fit, and compact content remains scrollable.

Review: GPT-6-Sol high, read-only Herdr `home-review`, reviewed against 6830928. Important findings were loss of gallery artwork notices on overlay rebuild and dismissing an in-progress placement handoff. Both corrected and covered by new passing Home tests. Pending removal dismissal (reviewed as Minor) was already in the active failure-path fix scope, made consistent and covered by the failed-removal test. Removal render gate and scroll restore regressions were found in the first Home run, then fixed; final 9/9 passed.

Ruling: added a backward-compatible optional PaperSheet dismissal predicate to guard all close/escape/swipe paths centrally during Home placement and removal. Existing callers default to current dismissal behavior. No backend APIs changed.

Edit Mode verification: 166/166 passed (`/tmp/tagtag-home-editmode.xml`). Existing creation visual test initially queried the whole document and selected the hidden Home gallery image with the same design ID; the query is now scoped to the open creation sheet, preserving the original aspect-ratio assertion.

Existing PaperVisualTests final run: 6/6 passed (`/tmp/tagtag-home-existing-2.xml`, 2026-09-26 03:11:54–03:12:42 UTC). Final evidence: 9 Home + 6 existing UI Play Mode tests and 166 Edit Mode tests passed. Physical-device/native checks remain pending as recorded in DEVICE_VERIFICATION.md.
