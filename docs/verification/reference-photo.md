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

## Unloaded photo message

The user clarified that the thumbnail said No reference photo. Production
request logs contained no recent recovery calls, while recent publications
recorded approximate location accuracy of roughly 1.4–2 km. Discovery requests
require a fresh fix within 50 m accuracy before loading the private map; this
is consistent with discovery stopping at location lookup, though the user's
on-device error is still needed to confirm the exact failure.

The UI also displayed No reference photo for the initial None state. A second
regression reproduced this after a simulated location failure. None now reads
Photo not loaded, Loading reads Loading photo, and the existing location error
and retry action remain visible. This does not relax location authorization.

The expanded mounted-view test also exposed a missing discovery status notice:
STICK never mounted the normal status component, so location errors and the
Open Settings action were absent. A discovery-only notice now shows progress,
errors, and location settings recovery in the bottom controls. The focused
regression passes (`/tmp/tagtag-photo-status-green.xml`).

Final full mounted UI suite: 11/11 passed (`/tmp/tagtag-photo-final-play.xml`).
