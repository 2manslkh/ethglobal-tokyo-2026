# Draft 3 verification

Verified locally on 2026-11-02 with FFmpeg 7.1, Python 3.12 and Pillow 12.3.0.
The full `verify.py` run passed on the final MP4.

## Export

- 20.000 seconds, 600 frames, 1080 × 1920, 30 fps.
- H.264/yuv420p, BT.709 primaries, transfer and matrix; stereo AAC.
- Silent decoded audio, without timer, reveal or boundary sounds.
- Complete video/audio decode without errors.

## Size and animation

The base pose sizes, before rocking/breathing, are:

| Target | Width × height |
| --- | --- |
| 1 | 56 × 72 |
| 2 | 48 × 60 |
| 3 | 34 × 48 |
| 4 | 29 × 40 |
| 5 | 26 × 32 |
| 6 | 24 × 30 |
| 7 | 24 × 33 |
| 8 | 24 × 32 |
| 9 | 24 × 32 |
| 10 | 24 × 35 |

All ten meet the minimum of 24 pixels on both sprite dimensions through all
600 animation frames, and remain within the target-safe margins. Checks against
decoded frames confirm independent motion during both the first and final five
seconds. Target artwork proportions are preserved.

## Continuous search and loop

- Source animation at exactly 20 seconds is pixel-identical to time zero.
- The last encoded frame is the preceding 1/30-second animation step, not a
  duplicated first frame or an end card.
- The five text elements are unchanged for the entire clip. There are no drawing,
  reveal, fade, countdown or end-card events in the subtitle track.
- Encoded scene frames at 0, 14, 16, 18, 19 and 19.966667 seconds match the
  uninterrupted source scene within compression error; maximum mean difference
  is 2.6327/255. No answer panel or closing card replaces the puzzle.
- Last-to-first encoded scene difference is 1.0609/255. The intended source motion
  step accounts for 0.0552/255; measured compression variation in stationary
  texture is 0.9729/255. The loop check accounts for this measured texture noise,
  capped below 1.5/255, rather than requiring lossless equality of encoded frames.

Inspected first, middle and final frames at 360 × 640: the puzzle stays visible,
text stays readable, the larger formerly tiny targets fit their hiding places,
and no answer markers or magnified insets appear. This is desktop/file validation;
no physical-phone, Instagram upload or audience-retention measurement is claimed.
