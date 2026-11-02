# Find Taggi: continuous loop, draft 3

Ten animated Taggis, with a minimum size of 24 pixels on the shorter sprite
dimension. The search remains visible for the entire 20-second Reel. There are
no answer circles, answer insets, countdown, introduction card or end card.

The text stays constant and each character completes an integer number of motion
cycles in 20 seconds, so playback continues naturally across the loop boundary.
The export is silent: the earlier countdown and answer sounds have been removed.
Music can be added in Instagram if desired. No watch-time improvement is claimed
without publishing and measuring results.

## Files

- `find-taggi-tokyo-v3.mp4`: 1080 × 1920, 30 fps H.264/BT.709 with silent AAC.
- `cover.jpg`, `CAPTION.txt`: unmarked cover and updated caption.
- `clean-background.png`: the same generated clean plate used in draft 2.
- `episode.json`: internal placement, source crops and loop-cycle settings.
- `render.py`, `edit.ass`, `requirements.txt`: reproducible rendering inputs.
- `verify.py`, `VERIFICATION.md`: export checks and their recorded results.

The original two drafts are preserved. No answer-key image is generated for this
version. Internal placement coordinates are retained so the edit remains reproducible.

## Size and motion

The original drawings retain their proportions. The renderer scales each cropped
pose so **both width and height are at least 24 pixels**, rather than stretching
it into a square. Breathing cannot shrink a dimension below that minimum.
All ten remain within the established safe area throughout the animation.

Bobbing, rocking and breathing retain independent phases, with 5–9 cycles per
20-second loop. The pose at exactly 20 seconds equals the pose at time zero.
The encoded final frame is the preceding 1/30-second motion step; the first frame
is not duplicated at the end.

## Rebuild

From the repository root with Python 3 and FFmpeg/ffprobe 7+:

```sh
python3 -m pip install -r docs/social/find-taggi/episode-01-tokyo-v3/requirements.txt
python3 docs/social/find-taggi/episode-01-tokyo-v3/render.py
python3 docs/social/find-taggi/episode-01-tokyo-v3/verify.py
```

Pillow composites the animated video frames; FFmpeg adds the constant typography
and encodes the output. This uses the established single-stream renderer to avoid
the multiple-looping-image export stall found during draft 2.

The renderer reuses typography helpers from `episode-01-tokyo/render.py` and
existing app fonts and mascot PNGs. The background was created with the built-in
image-generation tool; its exact edit prompt is in the sibling draft 2 folder's
`BACKGROUND_PROMPT.txt`. No new image generation or network access is required.
Rebuilding overwrites only this draft's MP4, ASS and cover.

Work and verification were performed locally without subagents; this chat has no
Herdr-managed pane.
