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
