# Explore location accuracy

## Cause and fix

Explore used `DeviceLocation.Current` with its default 50-metre accuracy limit, which is also used for close-range discovery. A fresh 75-metre fix therefore waited 20 seconds, reported “Location accuracy is still low”, and never populated the map center. `/v1/nearby` separately rejected the same fix with HTTP 400. A live read-only request using a synthetic coordinate confirmed that API behavior.

The Explore caller and only the nearby API now accept accuracy up to 5,000 metres, independently of the unchanged 2 km browse search radius. The measured accuracy is retained and the UI labels approximate locations. This does not invent a precise location or relax timestamp, permission, publication, recovery, or collection checks. Beyond 5 km uncertainty, refresh/permission guidance still applies.

## Verification

- Baseline: real controller + DeviceLocation tests failed with the exact timeout for fresh 75/500/2,000-metre fixes. A stale-fix rejection test passed. The new API regression failed with HTTP 400 versus expected 200.
- Fixed: 185 Unity Edit Mode tests passed, including the new browse/location-notice cases and existing strict location and cancellation cases.
- Backend: 69 tests passed; one emulator-only test skipped. New tests cover the 5,000-metre limit, rejection at 5,001 metres, stale timestamps, public summary privacy, and unchanged recovery/collection/publication limits.
- Independent review: GPT-6-Sol with high thinking, read-only Herdr `map-review` pane. Integration and deployment remain coordinator-owned.

## Device check

After installing the updated app, open Explore with Precise Location enabled. A fresh approximate fix should reveal the map and load nearby clues with an approximate-location notice, rather than remaining on the location error. Tap Refresh nearby to update the position. AR recovery and collection still need a sufficiently precise fix. Device interaction verification is pending; software regression results are not evidence of actual GPS performance.

Review found an existing physical-device record with FullAccuracy authorization and a fresh 2,000.149-metre fix. New client and API regressions reproduced failure at that exact value with the provisional 2 km ceiling. The final 5 km browsing ceiling covers this observed coarse reading without changing the search radius. Reduced Accuracy permission still directs the user to Settings, consistent with the app's existing permission policy; this specific error was observed under FullAccuracy, so permission-policy changes are outside this fix.
