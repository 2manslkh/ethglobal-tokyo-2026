# App Store screenshot campaign

English iPhone review draft: **Little discoveries worth keeping.**

- [Contact sheet](en-US/contact-sheet.png)
- [Portable editable gallery](en-US/index.html)
- [Six PNG exports](en-US/draft/)
- [Source and capture manifest](en-US/manifest.json)

Each PNG is 1320 × 2868 pixels. The HTML embeds original screenshot assets,
mascot PNGs and the licensed app fonts, so it opens without a server or network.
Layouts use paper white, charcoal, restrained yellow, Shadows Into Light display
text and Instrument Sans body text from the existing app design.

## Capture status

These are review drafts, **not upload-ready App Store assets**. The repository's
existing native test and Unity fixture captures are preserved within the layouts.
No generated UI or invented AR photography is represented as a real app capture.

| Slide | Current source | Final replacement |
| --- | --- | --- |
| Find your places | Native MapKit overlap test with placeholder labels | Current Explore, populated with representative stickers and meaningful names |
| A little curiosity goes places | Enlarged teaser excerpt from a Unity fixture | Current teaser with the place and clue visible |
| Find a sticker. Uncover a story | Unity discovery celebration | Physical iPhone AR discovery with real surroundings and a visible sticker |
| Collect your moments | Fresh 1170 × 2532 capture of current Unity UI, 20 sample stickers across twelve poses | Final device confirmation |
| Make it yours | Fresh 1170 × 2532 capture of current Add Sticker sheet, supported-device capabilities simulated | Final device confirmation |
| Leave a little discovery | Unity publication celebration | Current publication success capture |

Slide 2 deliberately crops to the teaser; its unavailable-map region is excluded.
The caption identifies the enlarged excerpt. Slides 3 and 6 use different success
states but need more varied imagery for the final campaign. Older fixtures remain low resolution. Slides 04 and 05 were recaptured at
1170 × 2532 from the current Unity UI; their content and device capabilities are
explicit fixture data, not evidence of device collection or Apple Intelligence.

Capture real app pixels without altering labels, inserting pins, or inventing
camera content. Record the device, build and capture source. After replacements,
inspect all exports again and remove draft status only when every image faithfully
represents the shipping app. No App Store upload was performed.

## Edit and render

Edit the `slides` records and CSS in `scripts/app-store/build.py`; all paths are
relative to the repository root. Then run with a Python environment that has
Playwright and Chromium installed:

```sh
python3 scripts/app-store/build.py
```

The renderer writes six PNGs, the portable HTML, manifest, and contact sheet.
Playwright renders the promotional layout only; it does not recreate app UI.

## Execution and verification

Coordinator owns `scripts/app-store/` and `docs/app-store/`. Review is read-only,
in a dedicated Herdr tab, using GPT-6-Sol with medium thinking. The coordinator
owns all Git writes. Existing app changes are outside this task's ownership.

Verification: render all six assets; inspect the contact sheet and individual
exports; check PNG dimensions and decode embedded images/fonts. No application
behavior changes or Unity build are needed for these marketing layouts.

Reference: [Apple screenshot specifications](https://developer.apple.com/help/app-store-connect/reference/app-information/screenshot-specifications).

Review result: a separate GPT-6-Sol reviewer inspected every PNG and the contact
sheet. Its one material layout finding (a clipped teaser button) was corrected;
the follow-up review marked that finding resolved and approved the set at draft
scope. The reviewer also confirmed consistency with DESIGN.md and the documented
capture limitations. All six exported dimensions were verified as 1320 × 2868.

## Revision: character and current screens

Explore Taggi was regenerated with the built-in image-generation tool to remove
the two extra raised arms. The corrected transparent PNG is used by both the
app navigation and slide 01. Its prompt is in [asset provenance](assets/README.md).

Slides 04 and 05 use fresh current-UI captures recorded on 2026-09-26 with
Unity 6000.5.5f1 at 1170 × 2532. Collected contains 20 sample stickers using all
twelve preset IDs. Add Sticker uses creationCapabilities=15 to show the supported
Upload, Photo and Imagine options. These are app-rendered fixture captures.

The capture harness is preserved in `scripts/app-store/CaptureScreens.cs`. To
repeat it, temporarily copy it into
`Assets/Tagtag/Application/PlayModeTests/AppStoreCaptureTests.cs` and run graphics-enabled
Play Mode tests with `-testFilter Tagtag.Tests.AppStoreCaptureTests`. Remove the
temporary source and its generated meta afterward. The capture run passed
(1 capture test, 0 failures); this does not certify real device features.

An additional native editor capture is retained in `captures/sticker-editor-current.png`,
from the EditorReview harness on the iPhone 17 Pro simulator (iOS 26.2), for future
layout use. Slide 05 uses the requested Add Sticker sheet.

Revision review passed at draft scope: the reviewer confirmed exactly two hands,
current Collected and Add Sticker controls, and no material clipping. Its stale
slide 05 table description was corrected.
