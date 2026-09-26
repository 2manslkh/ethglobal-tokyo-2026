# Design

Tagline: **Find your places, Collect your moments**

## Theme

**Little discoveries worth keeping.**

tagtag feels like a personal sticker book of places, recommendations, and
encounters. A collected sticker is a souvenir of discovering something another
person chose to share. The mood is warm, curious, thoughtful, and lightly playful.

The visual direction is **subtle stationery**: paper-white surfaces, charcoal ink,
die-cut artwork, and small handmade details. Give the collection the care of a
keepsake while keeping navigation and reading effortless. Avoid competitive,
promotional, or overly childish presentation.

This document defines the intended design and implementation requirements. It
does not certify that the current app implements every treatment below.

## Visual Language

### Paper and ink

Use paper white for the main canvas and a slightly warmer paper tone for the
sticker book. Charcoal carries primary text and icons; softer neutral ink carries
supporting text. Thin dividers and gentle shadows provide separation without
heavy outlines around every surface.

Yellow is a restrained accent for the primary available action and meaningful
selection. Use dark text on yellow. Error and success treatments retain explicit
labels and distinct semantic colors; never communicate state through color alone.
Keep the rest of the interface quiet enough for sticker artwork to lead.

Use die-cut white borders and soft contact shadows to suggest a sticker resting
on paper. Preserve artwork proportions. Keep the book's grid orderly: handmade
irregularity belongs in the drawings, not in displaced controls or unreadable text.

| Use | Avoid |
| --- | --- |
| Clean paper tones and occasional ink doodles | Heavy grain, distressed textures, or decorative tape everywhere |
| Small depth cues around sticker artwork | Raised cards and large shadows around every control |
| Yellow for an action or selection | Yellow filling every panel or acting as the only status cue |
| Expressive artwork within stable layouts | Random rotation of labels, fields, or navigation |

### Typography

Use **Shadows Into Light** for display text and titles, following the device-review
direction of 2026-09-26. Use **Instrument Sans Regular / SemiBold** for body text,
fields, controls, navigation labels, and native map counts. Ship the actual font
files and their SIL Open Font License notices. Keep a readable fallback for
characters outside the handwriting font's coverage; do not synthesize bold.
Preserve the source's typography roles and scaling behavior.

Use sentence case, retaining **STICK** as the named camera tab. Notes, instructions,
and controls must remain easy to read. Handwriting belongs to display/title
roles, never essential instructions or full notes.

Navigation uses matching Taggi illustrations: raised arms forming a roof for
Home, a held yellow star for STICK, and an open folded map for Explore. Keep the
destination labels and selected marker; artwork must remain legible within
stable touch targets.

Support text scaling, wrapping, safe areas, and controls at least 44 points in
size. Preserve larger targets provided by the source components. Target 4.5:1
contrast for normal text and 3:1 for large text and essential control graphics;
verify camera overlays against changing real-world imagery. Validate font fallback
for supported languages rather than assuming the imported fonts cover every script.

## App References

- **Instagram:** familiar browsing, content hierarchy, and direct access to creation.
- **POAP:** the idea of collecting a memory connected to a place or experience.

These are interaction and concept references. tagtag's visual identity follows
the stationery direction above; these references do not add feeds, minting, or
other product features.

## Mascot (Taggi)

Taggi is the supplied hand-drawn rabbit-like figure. Preserve its irregular black
linework, simple expression, and recognizable silhouette. Related illustrations
and small decorative marks should feel drawn by the same hand.

Taggi is a friendly companion in onboarding, empty states, camera preparation,
and moments of discovery. Keep routine navigation quiet. Avoid repetitive mascot
interruptions or large illustrations that push the user's collection off screen.

## Voice

Use short, friendly, concrete language. Invite exploration without urgency or
pressure: “Your next little discovery is out there.” Celebrate a collected
sticker gently: “A little discovery, now in your book.”

Permission, loading, and error copy must explain the real state and next step.
Use “Getting the camera ready” while waiting for camera imagery, and “Allow camera
access to find stickers in AR” when access is needed. Do not disguise failures
with playful copy or celebrate before an operation succeeds.

## Component Sources

**Required source:** `~/Desktop/coding-projects/sticker-app`.

Adapt the existing Unity UI Toolkit primitives and their required styles,
resources, and behavior into tagtag. This is a requirement to reuse the actual
implementation, not just to imitate screenshots. The source paths below are
relative to that repository; they are local reference paths, not runtime
dependencies on another checkout.

| Concern | Source | Adaptation requirement |
| --- | --- | --- |
| Buttons, icon buttons, selections, switches, settings rows, and fields | `unity/Assets/StickerHunt/Playable/PrimitiveControls.cs` | Preserve activation, accessible naming, focus, selection, disabled, helper, counter, and validation behavior. |
| Shared visual states and typography | `unity/Assets/Resources/Playable.uss`, `unity/Assets/StickerHunt/Playable/AppTypography.cs`, `unity/Assets/Resources/Fonts/` | Carry the relevant styles, real font assets, and license notices; map surface colors to tagtag's paper and ink direction. |
| Shell and scrolling | `unity/Assets/StickerHunt/Playable/ScreenShell.cs` | Preserve safe-area and large-text behavior; its scrollers hide stock Unity scrollbar chrome. |
| Sheets and notices | `unity/Assets/StickerHunt/Playable/BottomSheet.cs`, `ConfirmationSheet.cs`, `AppNotice.cs` in the same directory | Preserve dismissal, scroll-versus-drag handling, modal focus, and clear feedback. |
| Navigation | `unity/Assets/StickerHunt/Playable/BottomNav.cs`, `AppNavigationMotion.cs` in the same directory | Adapt shared behavior to Home, STICK, and Explore while preserving navigation continuity. |
| Motion | `unity/Assets/StickerHunt/Playable/AppMotion.cs`, component code, and `unity/Assets/Resources/Playable.uss` | Reuse motion channels, timing, curves, component transitions, and reduced-motion behavior. |

Bring across only the dependencies needed by the adopted components, including
input and accessibility helpers where required. Preserve tagtag's naming,
artwork, account rules, and discovery flow. The source app's other features and
screen compositions are not automatically part of tagtag. No runtime import or
component migration is implied by this document alone.

## No Unity Defaults

Every app-owned visible surface must have an intentional tagtag treatment, from
the first rendered frame through loading, failure, interruption, and recovery.
Stock Unity styling is not an acceptable fallback.

- Style buttons, fields, switches, focus indicators, selection, disabled states,
  sheets, and loading feedback, including their internal UI Toolkit elements.
- Hide stock vertical and horizontal scrollbar chrome, following the source
  shell and sheets, while retaining scrolling. Where a visible indicator is
  needed, provide a slim neutral indicator with deliberate styling. Verify long
  forms and enlarged text rather than hiding overflow.
- Never expose the scene/editor-style background, skybox, default camera clear
  color, debug overlays, or placeholder geometry in the app viewport. Configure
  a deliberate opaque fallback behind the interface as well as the UI cover.
- Keep system-owned permission and sign-in dialogs native. The requirement
  concerns app-owned Unity presentation.

### Camera preparation and recovery

Cover the camera region with an opaque paper-white surface, Taggi, and concise
status text until camera imagery is ready. Keep the camera Close control available. Reveal the
live camera only after a valid frame is available for display, not after an
arbitrary delay or merely because the AR view has mounted.

Camera imagery and tracking readiness are separate states. Once imagery is
available, show it while readable guidance explains scanning or tracking recovery.
When the camera feed is unavailable, keep or restore the designed cover. Permission
denial, unavailable hardware, startup failure, and interrupted sessions need their
own truthful message and an appropriate recovery action.

Apply the same cover and fallback during cold startup, entering STICK, returning
from system dialogs, background/resume, and camera restart. There must be no
transient frame of an unstyled scene between these states.

## Motion

Motion reinforces touch, selection, and discovery without competing with the
sticker artwork. Source component motion from `sticker-app`; do not introduce a
parallel animation system or replace its interactions with unrelated effects.

| Shared role | Source timing | Use |
| --- | --- | --- |
| Feedback | 120 ms | Small immediate responses |
| State | 220 ms | Selection and switch changes |
| Arrival | 320 ms | Content and sheet appearance |
| Exit | 200 ms | Dismissal |

Reuse `AppMotion.EaseOut` and its restrained spring, which settles after a small
overshoot. Preserve component-specific timings where the source defines them:
the shared roles are not a command to replace every duration. Buttons use tonal
press feedback with stable geometry; selection markers and switch thumbs animate
within their controls. Sheets retain their drag, settle, and dismissal behavior.

Use the source artwork-confirmation motion for a successful collection where
applicable: a small settling response on the artwork, not a bouncing entire
screen. Navigation and routine refreshes must preserve focus, scroll position,
and ongoing input rather than replaying entrances unnecessarily.

Honor reduced motion by removing spatial travel, spring, and page-turn animation;
use immediate state changes or the source's brief non-spatial reveal. Cancel or
settle animation safely when interrupted, detached, or backgrounded. Do not leave
invisible or untappable content waiting for an animation callback.

Animation is presentation only. Publishing, collecting, and data persistence must
not depend on animation completion. Haptics, when appropriate for a meaningful
outcome, must also be independent of animation callbacks; keep decorative motion
quiet and verify hardware behavior separately.

## Experience

tagtag helps people discover places through other people. Anyone can leave a
sticker at a place they have visited, sharing a personal recommendation with
future visitors.

- Users must be physically at a place to leave a sticker there.
- Before discovery, users see only a teaser of the sticker's full note.
- The teaser guides visitors to an obvious, easy-to-find sticker.
- Users must find and tap the sticker through the AR camera to unlock its full
  note and collect it. Arriving nearby alone does not unlock the note.
- Collecting adds a copy to the user's collection. The original remains
  available for everyone else to discover.

The discovery loop is: see a teaser, visit the place, find and tap the AR
sticker, reveal the full note, and collect a copy.

## App Screens

### Home

Home shows the total number of stickers collected and the user's collection
as a sticker book. A profile icon beside “Your sticker book” opens sign-in for
guests and account settings for signed-in users. There is no global brand/sign-in
bar or reserved header space; screen titles and browsing tab navigation remain.

Treat Home as a calm personal keepsake. Sticker artwork is the focal point;
totals and page controls are secondary. A warm paper surface and gentle sticker
shadows are enough to suggest a book without surrounding it with scrapbook props.

- Each page uses a fixed grid of **5 rows and 4 columns**, holding 20 stickers.
- Stickers are arranged automatically for the first version.
- Users swipe between pages and tap a collected sticker to revisit its details.
- The total collected appears above the book.

### STICK

STICK is the AR camera view. Users can see and collect nearby stickers or
choose to stick a new sticker.

Let the real surroundings lead once the camera is ready. Keep controls compact
and legible without shrinking touch targets. Use deliberate high-contrast
backings for camera controls and guidance, and paper-toned sheets for reading and
composition. Follow the preparation and recovery rules above before revealing
the feed. Collection gets a small confirmation after success.

Camera mode fills the screen and hides the bottom tab bar. The native iOS status
bar stays visible, with dark text on an opaque paper backing above safe-area
controls. A 48-point circular paper-white Close sticker uses an inset charcoal
dashed circle, centered X, subtle shadow, and “Close camera” accessible label. It
returns to the destination that opened the camera. A
circular 88-point STICK action opens **Your stickers**, the four-pose inventory.
Keep the camera controls on readable paper backings rather than importing native
glass. This flow adapts `sticker-app`'s `DeviceApp.CameraScreen`, `SurfaceTap`,
`SurfaceGesture`, and `DeviceARSession.Placement` implementations.

1. Open the inventory and choose a Taggi pose without placing a preview automatically.
   The top placement sticker is a sharp-cornered paper-white rectangle with an
   inset dashed border and only **Place Sticker** centered inside. Keep tracking,
   placement guidance, discovery clues, and recovery messages in a separate compact
   scrollable paper notice below it. Dashed outlines do not intercept input.
2. Distinguish finding tracking, finding a surface, and a surface ready for a tap.
   Outline tracked, unsubsumed plane boundaries in yellow while choosing a spot.
3. A short tap inside a tracked polygon places the preview. Drag, long press,
   cancellation, controls, sheets, and multi-touch must not initiate placement.
4. Adjust with gestures only: drag along the original surface, pinch to resize,
   and twist to rotate. Retain the 10–50 cm size limits and input blocking; there
   is no size/rotation panel or move-to-center action.
5. Write the place, clue, and full note in the existing sheet. Explicit publishing
   still requires sign-in, mapped tracking, and all existing service gates.

Opening any sheet or losing camera readiness blocks placement and collection
input. Surface-found guidance is not publish readiness. Rendering uses a bundled
material/shader asset so the player retains the artwork shader and its transparent
silhouette.

### Explore

Explore shows sticker locations on a street map so users can discover places
to visit. The map provides access to teasers; unlocking the full note and
collecting a sticker require finding and tapping it in STICK's AR view.

Use die-cut sticker pins and count clusters. Selecting a pin opens a teaser
sheet with a Find in AR action.

Keep streets and place labels readable. Express the brand through artwork-led
pins and paper-toned teaser sheets rather than decorating the map itself.

## Visual Acceptance

The following checks are required when implementing this guide. Documentation
alone does not satisfy them.

- Capture Home, STICK, Explore, account screens, sheets, and long scrolling forms
  with empty and populated content. Inspect normal, pressed, focused, selected,
  disabled, loading, and error states for exposed Unity defaults.
- Verify standard and enlarged text, safe areas, readable contrast, accessible
  labels, and minimum touch targets. All content remains reachable by scrolling.
- Exercise primitive interactions and motion against the source components,
  including rapid input, interrupted transitions, sheet gestures, and reduced
  motion. Confirm that animations do not duplicate or gate product actions.
- Record physical-device video of cold startup, camera preparation, permission
  return, tab changes, tracking interruption, and background/resume. Inspect
  transitions for flashes of scene backgrounds or stock chrome; a static
  screenshot of the ready camera is insufficient.
- Record device, scenarios, results, and remaining gaps in
  [device verification](docs/DEVICE_VERIFICATION.md). Do not describe an untested
  camera transition or migrated component as verified.

### Die-cut stationery accents

Use inset charcoal dashes on featured paper containers and key sticker actions. Rounded rectangles use wider spacing on containers; action capsules use tighter spacing. Avoid decorating both an enclosing container and its inner action. The camera inventory button pairs a 52-point Taggi holding an open sticker book with a live STICK label inside its 88-point circular target. Bottom navigation keeps its star-holding Taggi illustration. Cut lines and artwork ignore pointer input; existing focus and disabled treatments remain visible.

### Home library and camera selection

Home has Collected, Your Designs, and Placed sections, each retaining its own
scroll position. Make a sticker lives inside Your Designs above the saved designs.
Placed lists active publications newest first, showing artwork, place, and date;
opening one centers and selects its location in Explore. Withdrawn stickers remain
available through existing account management, not the Placed section.

Featured containers and primary/secondary actions use inset charcoal dashed
cut lines. Quiet inline actions stay simple. Avoid decorating both a container
and its nested controls. Press feedback changes the paper tone without moving
or resizing controls; focus and disabled states remain distinct.

While choosing or adjusting a camera placement, show its artwork in an 80-point
paper thumbnail above the bottom dock, aligned to its right edge with a 12-point
gap. Tapping opens the existing sticker picker. This control blocks camera
placement input and is hidden during recovery, discovery, and modal presentation.

### Explore loading

Mount the map before GPS completes. Retain the viewport across visits and keep
previous pins visible while refreshing. Distinguish map tile loading, finding
location, and fetching nearby stickers; failures offer retry without blanking
existing content. Nearby results, including empty results, are reused for 60
seconds on automatic navigation; explicit refresh always requests fresh data.
A location opened from Placed is a browsing target, never a device location fix.
