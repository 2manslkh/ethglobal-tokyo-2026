# Device verification

## Build under review

Unity 6000.5.5f1; bundle `com.kenk.tagtag`; Apple team `5Y6QUA9GA6`. Target: Dawg., iPhone 15 Pro Max. A second ARKit iPhone is required for shared recovery.

## Automated evidence

- 2026-09-26: final Unity Edit Mode run passed 39/39 tests (`/private/tmp/tagtag-final-tests.xml`), including service boundaries, account-specific storage, restart retries, and offline blocks.
- Backend behavior suite passed 18 tests with 0 failures (one emulator-only case skipped in the ordinary run); separate Firebase Auth/Firestore/Storage emulator integration passed 1/1.
- Unity export passed with `libUnityARKit.a` and `ARKit.framework` explicitly asserted. Native Xcode compilation and development signing succeeded with the app-specific Apple-capable profile.
- Live API health/authentication/nearby and cleanup-job checks passed; see [deployment](DEPLOYMENT.md).

## Physical checks

On 2026-09-26, the signed build installed on Dawg., iPhone 15 Pro Max running iOS 26.6.1 (23G83), through USB with `ios-deploy`. The user approved replacing the previous installation after the signing-team change. A Documents backup was attempted and contained no files. Installation returned `InstallComplete`; a subsequent `--exists --bundle_id com.kenk.tagtag` check returned `true`.

CoreDevice still reported the phone unavailable despite valid USB trust pairing. The USB launch attempt failed because its developer-image service was unavailable, and screenshot capture could not start. Manual launch and a Home screenshot have been requested; installation alone does not verify runtime behavior.

Record device/iOS/build, observed result, and screenshot paths for each check:

1. Cold launch; Home has exactly 5×4 spaces, safe areas, no clipped text; 0/1/20/21 collections paginate correctly.
2. Apple/Google login, cancellation, app restart, sign-out, reauthentication, and independent second account.
3. Explore map gestures, clusters, marker selection, recenter; overlays fully hide the native map.
4. Camera and location permission denial/retry; poor GPS/tracking shows useful errors without losing the draft.
5. Place each preset; position, pinch, rotate, keyboard editing; interrupt upload and retry without duplicates.
6. Phone A publishes. Phone B sees only teaser, enters AR, relocalizes, directly taps within 3m, reveals note, and gains one copy. Original remains. Repeat tap does not duplicate the collection.
7. Restart and changed lighting recovery. Wrong room, stale recovery, tracking loss, and proximity without a tap never unlock the note.
8. Offline collection viewing; reconnect; withdrawal, blocking, moderation, and account deletion hide or revoke content as specified.
9. Smaller screen, large text, VoiceOver, Reduce Motion, and keyboard controls.

## Current limits

No successful two-device AR recovery, live provider sign-in, or current-build hardware launch is claimed until the checks above are recorded. Budget alerts warn about spend; they are not a spending cap.
