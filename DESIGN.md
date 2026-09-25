# Design

## Key Motifs

1. Hand Drawn
2. Die-cut Stickers
3. Paper White

## App References

1. Instagram
2. POAP

## Mascot (Taggi)

A hand-drawn rabbit-like figure.

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
as a sticker book.

- Each page uses a fixed grid of **5 rows and 4 columns**, holding 20 stickers.
- Stickers are arranged automatically for the first version.
- Users swipe between pages and tap a collected sticker to revisit its details.
- The total collected appears above the book.

### STICK

STICK is the AR camera view. Users can see and collect nearby stickers or
choose to stick a new sticker.

For the first version, placement uses a preset sticker library:

1. Open the sticker picker from the AR view.
2. Choose a preset sticker.
3. Position the sticker, pinch to resize it, and twist to rotate it.
4. Add a teaser and the full note.
5. Confirm placement at the current location.

### Explore

Explore shows sticker locations on a street map so users can discover places
to visit. The map provides access to teasers; unlocking the full note and
collecting a sticker require finding and tapping it in STICK's AR view.

The map pin style, clustering behavior, and teaser presentation are still open
design decisions.

## Project Reference

The existing app at `~/Desktop/coding-projects/sticker-app` is a reference for
the collection-focused Home, sticker book, combined discovery and placement
camera, and map-based Explore screen.

This document records the current design direction. It does not imply that
these screens or behaviors are implemented in the Unity starter project.
