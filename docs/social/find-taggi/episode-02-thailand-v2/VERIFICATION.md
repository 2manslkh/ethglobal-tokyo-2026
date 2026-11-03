# Thailand v2 verification

Verified on 2026-11-03 using FFmpeg 7.1, Python 3.12 and Pillow 12.3.0.
The complete `verify.py` check passed on the delivered MP4.

## Export

- 20.000 seconds; 600 frames; 1080 × 1920; 30 fps.
- H.264/yuv420p with BT.709 color metadata and stereo AAC.
- The decoded audio is silent, matching the v1 treatment.
- Full video/audio decode completed without errors.
- Libass selected Shadows Into Light and Instrument Sans correctly.

## A/B comparison

The background path resolves to the original v1 image. Source dimensions,
crop, scene rectangle, every target's placement/size/motion setting, duration,
frame rate and caption match v1 exactly. Source puzzle pixels also match v1
at 0, 7.25, 13.5 and 19.966667 seconds.

Exactly ten animated targets remain inside the scene, each at least 24 pixels
on both sprite dimensions throughout all 600 frames. All stay within the
established target-safe margins. Encoded frame comparisons confirm that all ten
move during both the first and final five seconds.

The separate 92 × 120-pixel waving example stays in the header, outside the
puzzle, with an EXAMPLE label. Its appearance was checked against the encoded
first frame and remains constant at the end. The instruction reads exactly
“Can you find all 10 Taggis?”. No answer markers or answer segment are added.

## Countdown

The ASS schedule contains twenty consecutive one-second events, displaying
20 through 01, plus seven constant text elements. All 600 encoded timer frames
were decoded and compared. Every number holds for 30 frames, and all nineteen
number changes occur at the expected second boundaries.

Maximum mean variation during a held number is 0.2302/255; the smallest
number-change difference is 3.9384/255. The timer resets from 01 to 20 on replay.
There is no displayed zero frame or end-card interruption.

## Visual review and loop

The full cover and frames at 0, 10 and 19.966667 seconds were reviewed at
360 × 640 per frame. The countdown, full prompt and example are readable and
remain above the puzzle, leaving the scene unobstructed.

The underlying character animation at 20 seconds equals time zero exactly.
The encoded puzzle's last-to-first difference is 1.2383/255, within the expected
source motion step of 0.0556/255 plus measured stationary compression variation
of 1.1663/255. The maximum mean difference between encoded and source puzzle
frames is 3.4898/255. The scene stays present throughout the 20 seconds.

The countdown makes the full composition's replay boundary visible by design;
the puzzle animation itself remains continuous. These are local file and
desktop phone-size checks, not an Instagram upload or physical-device test.
