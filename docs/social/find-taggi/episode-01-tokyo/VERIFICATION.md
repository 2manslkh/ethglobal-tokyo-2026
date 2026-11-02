# Pilot verification

Verified locally on 2026-11-02 with FFmpeg 7.1. This records file checks and
desktop previews at 360 × 640, not an Instagram upload or physical-phone test.

## Export checks

Command, from the repository root:

```sh
python3 docs/social/find-taggi/episode-01-tokyo/verify.py
```

All checks passed on the final export:

- 1080 × 1920, square pixels, H.264/yuv420p, 30 fps, 600 frames, 20.000 seconds.
- BT.709 matrix, primaries and transfer; limited-range video.
- Stereo AAC at 48 kHz.
- Scene comparisons at 2, 5, 8, 11 and 13.966667 seconds confirm a stationary
  search window. Maximum mean decoded pixel difference: 0.7745/255; the small
  difference is consistent with lossy video compression.
- One, two and three reveal circles at 14.6, 15.6 and 16.6 seconds respectively.
  All three remain at 17.966667 seconds. Positions checked against the measured
  target coordinates in the encoded video.
- All eight sound cues present; decoded mono peak −16.68 dBFS, without clipping.
  The main search interval has no music or narration.
- Complete video and audio decode without errors.

## Visual review

- Inspected the master and full-size export frames for exactly three intentional
  Taggis: waving among books, heart-hugging among flowers, sleeping by café crates.
  No additional mascot-like figure was identified elsewhere in the scene.
- Checked introduction, search, first reveal and closing card at 360 × 640.
  Characters remain identifiable, text remains readable, and targets are clear
  of the timer and margins. Difficulty has not been tested with an external viewer.
- Inspected the final answer frame: circles surround the correct characters,
  without obscuring their faces. Targets remain inside the planned safe area.
- Cover uses the opening frame and has no answer markers. The introductory
  mascot is absent throughout the timed search.
- FFmpeg's font-selection log confirms the real Shadows Into Light, Instrument
  Sans Regular and Instrument Sans SemiBold files; the timer uses no synthesized bold.

Audio validation is based on waveform, cue timing and decode checks; no subjective
listening test is claimed. Visual review was performed by the producing agent.
