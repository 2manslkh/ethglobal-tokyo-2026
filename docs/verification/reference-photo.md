# Original spot visibility

## Diagnosis

On 2026-09-26, the latest six production AR-map objects contained version-2
photo envelopes. The newest embedded JPEG decoded successfully. No private
photos or map payloads were added to this repository.

The discovery UI required a live camera before displaying Original spot.
AR recovery resets camera presentation to Preparing, and interruptions also
hid the photo even after it had loaded. A mounted-view regression reproduced
this: the photo was visible while Live, then its display became None while
Preparing (`/tmp/tagtag-photo-red.xml`).

## Change

Original spot remains available during discovery independently of camera
startup or interruption. Its saved photo can still be enlarged. Pending
recovery downloads show Loading photo rather than No reference photo.
Capture, map storage, AR matching and collection eligibility are unchanged.

## Verification

- Focused regression: 1/1 passed (`/tmp/tagtag-photo-green.xml`).
- Full PaperVisualTests: 11/11 passed (`/tmp/tagtag-photo-play.xml`).
- Full Edit Mode suite after map integration: 217/217 passed
  (`/tmp/tagtag-photo-edit.xml`).
- Rendered loading, live, preparing, interrupted and enlarged states inspected.
  Captures use a synthetic magenta image, not a user's saved photo.
- Physical discovery of a newly published sticker remains to be checked after
  installing the updated iPhone build.
