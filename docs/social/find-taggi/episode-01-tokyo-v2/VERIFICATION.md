# Draft 2 verification

Verified on 2026-11-02 using FFmpeg 7.1, Python 3.12 and Pillow 12.3.0.

## Encoded export

`verify.py` completed successfully on the final MP4:

- 20.000 seconds, 600 frames, 1080 × 1920, 30 fps, H.264/yuv420p, BT.709.
- Stereo AAC; decoded mono audio peak −16.68 dBFS, without clipping.
- Complete video/audio decode without errors.
- Individually sampled each target at 2.2, 2.8, 3.6, 4.7 and 6.1 seconds. Every
  target has visible pixel changes above 20/255 in the encoded video:

| Target | Maximum changing pixels in its region |
| --- | ---: |
| 1 | 1819 |
| 2 | 1096 |
| 3 | 526 |
| 4 | 653 |
| 5 | 422 |
| 6 | 349 |
| 7 | 136 |
| 8 | 68 |
| 9 | 16 |
| 10 | 9 |

- Background outside the animated regions remains stationary: mean sampled
  decoded difference 0.0070/255.
- Targets 9 and 10 have exactly 4 × 4 RGBA canvases before compositing, with no
  rotation or breathing resize. In the final encoded frame, 11/16 and 12/16
  pixels respectively differ from the underlying background by more than 15/255.
- All ten reveal rings were detected at the recorded coordinates and remain
  within the target-safe margins.

## Visual review

Inspected the clean plate for removal of all three original targets and absence
of other mascot figures. Inspected the encoded search and answer frames at full
size, plus introduction, search, answer and closing frames at 360 × 640.

The reveal insets use nearest-neighbour magnification of real 16 × 16 scene
crops containing the four-pixel sprites. They deliberately remain pixelated and
do not invent extra detail. The inset panel covers none of the ten answer markers.
The original draft and its assets are unchanged.

This is local file verification and desktop visual review by the producing agent.
No Instagram upload, physical-phone playback, audience difficulty test or
subjective audio listening test is claimed. At 4 × 4 pixels, character identity
is not visually recoverable; platform recompression may soften those targets.

## Render correction

The initial twelve-image FFmpeg graph stalled after ten output frames in bounded
diagnostic runs, including without zooms and with overlays continuously enabled.
The final renderer composites the ten animated cutouts into one RGB stream, then
uses FFmpeg for titles, answer rings, audio and encoding. The complete 600-frame
render and encoded-output checks passed through this corrected path.
