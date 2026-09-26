# Overlapping map stickers

Tapping a MapKit cluster previously only called `showAnnotations`. Pins at the
same coordinates could never separate, so neither sticker was selectable.
The bridge now presents a native **Stickers here** sheet with one row per member,
including artwork, place name and public teaser. Each row returns the existing
sticker ID through the normal map-selection bridge. Single pins keep their normal
selection behavior. Closing the sheet, hiding/disposal of the map, and refreshed
pins clear the chooser; stale callbacks cannot select an old item.

## Native regression

Run with a booted Apple Silicon iPhone simulator:

```sh
scripts/test-map-overlap.sh YOUR_SIMULATOR_UDID
```

The script compiles the actual `TagtagMap.mm` into a separate UIKit harness app,
`com.kenk.tagtag.map-overlap-tests`, and writes its result under the printed
`/private/tmp/tagtag-map-overlap-test.*` artifact directory. It does not sign in,
modify account data or replace tagtag. Pass `--review` to leave a two-sticker
chooser open for inspection.

- Red run reproduced failure for two pins at identical coordinates:
  `/tmp/tagtag-map-overlap-red.log`.
- Final green runs on iPhone 17 Pro / iOS 26.2 and iPhone SE (3rd generation) /
  iOS 18.5: `/tmp/tagtag-map-overlap-green.log` and
  `/tmp/tagtag-map-overlap-compact.log`.
- Tests cover selecting both coincident pins, repeated opening, Close, map hiding,
  ignoring a stale callback, a 25-member scrollable group, invalidation after a
  pin refresh, and ordinary single-pin selection.
- The harness waits for actual UIKit presentation/dismissal state between steps.

Native screenshots: [iPhone](iphone.png), [compact iPhone](compact.png),
[dark appearance](iphone-dark.png), and [compact enlarged text](compact-large-text.png).
The harness uses synthetic stickers and the production map bridge; these captures
are not an authenticated end-to-end app or physical-device test.

Implementation references: Apple’s [cluster annotation documentation](https://developer.apple.com/documentation/mapkit/mkclusterannotation)
and [native sheet presentation](https://developer.apple.com/documentation/uikit/uisheetpresentationcontroller).
