# UI improvements execution

Approved in conversation: dashed die-cut buttons and containers with pressed states; move Make a sticker into Your Designs; responsive Explore loading; selected sticker thumbnail above the camera dock at right, tapping opens picker; Home Placed tab lists active placements newest first and opens them in Explore.

## Ownership and models

All implementation runs in `/private/tmp/tagtag-ui-improvements`, branch `feat/ui-improvements`. Herdr workspace UI improvements preserves the user's focused tab.

- Home and camera (`ui-polish`): GPT-6-Sol, high. Own UI files except MapPresentation.cs and PaperFlow.cs, plus Application/PlayModeTests. Shared Screens.cs is exclusively UI-owned; Explore agent sends integration requirements to coordinator.
- Explore (`explore-speed`): GPT-6-Sol, high. Own TagtagController.cs, NativeMapView.cs, TagtagMap.mm, MapPresentation.cs, PaperFlow.cs, NearbyNavigationTests.cs. Do not edit models or other UI files.
- Placed API (`placed-api`): GPT-6-Sol, high. Own backend and API contract documentation. Add backward-compatible status and authored pagination.
- Coordinator: shared models, new placed service partial and tests, integration, design/readme/device documentation, Unity tests/builds, Git write coordination and push.

## Contracts

Placed UI calls RefreshPlacements(), LoadMorePlacements(), OpenPlacedLocation(StickerSummary) on controller. State fields: placements (List<StickerSummary>), placementsLoading (bool), placementsLoaded (bool), placementsError (string), placementsNextCursor (string). Active status is published. Existing authored list and account management remain separate.

Map browse target is State.mapSelection (StickerSummary), separate from physical location. Controller OpenPlacedLocation opens Explore and sets selected and mapSelection. Nearby refresh preserves it. Map rendering includes that pin if missing from nearby.

## Verification

Coordinator runs Unity EditMode and graphics-enabled Home/Paper PlayMode, ARKit prepare/export and Xcode check sequentially. Backend agent runs npm test. Device-only checks are recorded honestly when unavailable. Commit only explicitly owned paths; agents request coordinator's Git lock before staging/committing. No concurrent index writes.

## Progress

- Implementation started. Existing unrelated TODO.md changes remain in original checkout.

- Backend API committed as 693bad2; 71 tests pass, one emulator test skipped.
- Placed service RED: all four new tests failed against empty methods for expected missing behavior. GREEN: full EditMode suite 183/183 passed, including pagination/filtering/retry, sign-out stale response rejection, and physical-location separation.
- Added optional IPlacedLocationsController and IMapLoadingExperience interfaces to avoid breaking existing ITagtagController/IMapExperience implementations and fakes.
- Shared models and authored pagination index are coordinator-owned. Publication status is only relied on for the new active-placement query.

- Services/map integration committed as 00af208. Review caught a withdrawal race; new test failed with a restored row, then passed after request invalidation and refresh. Latest EditMode 184/185: only a UI copy expectation pending update; all service/map tests passed.
- Initial graphics run 15/18 passed. Two fixture assumptions were outdated after moving controls; a real button intrinsic-width regression from outline children was identified from both test and screenshot and sent for correction.

- Final review fixed same-target recentering (6985f65), pagination after both tab revisit and background refresh, intrinsic text sizing, and button-local outline coordinates.
- Final verification: EditMode 186/186; Home/Paper 20/20; final Home 13/13; isolated compact capture 1/1. ARKit preparation/export and unsigned Xcode Debug iphoneos build succeeded.
- Physical-device timing/GPS/AR checks remain unperformed. No backend deployment. Concurrent location diagnosis owns DeviceLocation changes; this work preserves the current 50m browse requirement.
