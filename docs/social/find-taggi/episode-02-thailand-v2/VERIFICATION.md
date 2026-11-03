# Thailand v2 popup verification

Verified on 2026-11-03 with FFmpeg 7.1, Python 3.12 and Pillow 12.3.0.
The complete `verify.py` run passed on the delivered popup revision.

## Export

- 20.000 seconds, 600 frames, 1080 × 1920, 30 fps.
- H.264/yuv420p with BT.709 color metadata and stereo AAC.
- Silent decoded audio, matching v1.
- Full video/audio decode completed without errors.
- Shadows Into Light and Instrument Sans loaded correctly.

## Popup and search timing

The cream card and large waving example are present during the 0–2-second
introduction. The card uses the supplied reference's centered layout, rounded
corners, thin outline and subtle shadow. The text reads “Can you find / all 10
Taggis?” and “18 seconds. Look closely.” The example measures 300 × 391 pixels.

The card, image and text share a 160 ms entrance fade and 140 ms exit fade.
The solid card and mascot were checked in the encoded frame at 1 second; its
last fading frame was checked at 1.966667 seconds. A plain card patch differs
from the uncovered artwork by 85.0697/255, confirming that it covers the scene.
Its encoded cream differs from the source by 2/255, consistent with RGB/YUV
quantization. The mascot-region encoding difference is 1.8339/255.

The popup is completely absent at frame 60 (2 seconds). All 540 source frames
from there through the end match the unobstructed puzzle exactly. Encoded scene
samples at 2, 14, 16, 18, 19 and 19.966667 seconds confirm no remaining popup,
example or answer panel. The initially transparent entrance frame at time zero
was also checked; maximum scene encoding difference across samples is
3.7804/255.

The countdown is absent throughout the introductory 60 frames. It then shows
18s through 01s, each for exactly 30 frames. All 600 encoded timer-region frames
were inspected. Maximum mean variation within a held number is 0.2504/255;
minimum transition difference is 3.3014/255. The ASS event schedule independently
confirms the 2-second start and uninterrupted 18-second countdown.

## Puzzle and comparison

Background, source crop, scene rectangle, ten target positions, sprite sizes,
animation settings, total duration and caption match v1. The clean base has no
permanent header example. Source puzzle frames match v1 at 0, 7.25, 13.5 and
19.966667 seconds.

All ten underlying sprite bounding boxes stay at least 24 pixels in both
dimensions and within target-safe margins across 600 frames. Targets covered by
the introductory card become visible when it clears. Decoded comparisons
confirm that every target animates during both the early and late search.

Underlying character motion at 20 seconds equals time zero exactly. The full
presentation intentionally restarts the popup on replay, so this revision is
not assessed as an uninterrupted full-composition loop. V1 offers 20 seconds of
unobstructed searching; this variant offers 18 seconds after its introduction.

The full cover and a 360 × 640-per-frame triptych at 0.9, 2 and 19.966667 seconds
were visually reviewed. The popup matches the reference style, its copy and
mascot are readable, and the search scene is clear after dismissal. The cover
uses the complete popup at 0.9 seconds; the search preview uses 3 seconds.

These are local file and desktop phone-size checks. Physical-device playback,
Instagram upload and audience performance have not been tested.
