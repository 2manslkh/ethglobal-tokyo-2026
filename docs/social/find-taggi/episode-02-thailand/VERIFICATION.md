# Thailand verification

Verified locally on 2026-11-02 with FFmpeg 7.1, Python 3.12 and Pillow 12.3.0.
The complete `verify.py` run passed on the delivered MP4.

## Export

- 20.000 seconds, 600 frames, 1080 × 1920, 30 fps.
- H.264/yuv420p, BT.709 primaries, transfer and matrix; stereo AAC.
- Decoded audio is silent; there are no timer, reveal or loop-boundary sounds.
- Full video/audio decode completed without errors.

## Targets

The generated background was visually inspected for unintended Taggi-like
figures; none were found. Exactly ten original app-artwork sprites are added.
The base sprite sizes are 56 × 72, 48 × 60, 34 × 48, 29 × 40, 26 × 32,
24 × 30, 24 × 33, 24 × 32, 24 × 32 and 24 × 35 pixels.

All ten remain at least 24 pixels on both sprite dimensions throughout all
600 frames. Every animated bounding box stays below the top 250-pixel clearance,
above the bottom 350-pixel clearance and left of the rightmost 150 pixels.
The targets are fully visible, preserve their proportions and do not cover
any human faces. Decoded frame comparisons confirm motion for all ten targets
during both the first and final five seconds.

## Loop and continuous search

- Source animation at exactly 20 seconds is pixel-identical to time zero.
- The final encoded frame is the preceding 1/30-second step; it is not a
  duplicated endpoint.
- All five text elements remain constant throughout the video. The ASS track
  has no reveal, drawing, fade, countdown, introduction or end-card events.
- Scene comparisons at 0, 14, 16, 18, 19 and 19.966667 seconds confirm the
  puzzle remains visible. Maximum mean encoding difference is 3.4890/255.
- The last-to-first encoded scene difference is 1.2384/255. The intended
  source motion step contributes 0.0556/255; measured compression variation
  in stationary texture is 1.1660/255. This passes the established loop check,
  which accounts for measured compression noise while requiring it below
  1.5/255.

The full cover and first/middle/final frame triptych were visually inspected at
360 × 640 per frame. Instructions remain readable, the larger characters offer
easy starting points, and the smallest characters provide harder finds. The
scene and typography remain unchanged across the sampled frames, without
answer markers or insets.

These are local file and desktop phone-size checks. Physical-device playback
and Instagram upload have not been tested.
