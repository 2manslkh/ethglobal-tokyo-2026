# Find Taggi: Thailand v2

A second Thailand Reel for comparison with v1. This version adds the requested
“Can you find all 10 Taggis?” instruction, a large labelled Taggi example, and
a countdown lasting the complete 20-second search.

## Versions

| Element | Thailand v1 | Thailand v2 |
| --- | --- | --- |
| Prompt | Can you find all 10? | Can you find all 10 Taggis? |
| Character example | None | 92 × 120 px, labelled EXAMPLE in the header |
| Countdown | None | 20 through 01, one number per second |
| Scene and ten hidden characters | Thai canal market | Identical background, positions, sizes and motion |
| Duration and sound | 20 seconds, silent | 20 seconds, silent |
| Answers | None | None |
| Caption | Original Thailand caption | Identical |

The example is outside the puzzle and is not one of the ten hidden targets.
Both the instruction and example stay visible for the entire Reel. The country
label moves above the title to make room in the header. The footer stays the
same. All targets remain at least 24 pixels in both sprite dimensions.

The countdown displays 20 during 0–1 seconds, 19 during 1–2 seconds, and so on,
ending with 01 during 19–20 seconds. On replay it resets to 20. The underlying
character animation loops continuously; the countdown reset is deliberately
visible. There is no intro, answer segment, end card or countdown sound.

This compares the combined instruction/example/countdown treatment against v1;
it does not isolate the effect of any one addition. No performance results are
claimed, and neither version has been published by this task.

## Files

- `find-taggi-thailand-v2.mp4`: 1080 × 1920, 30 fps, H.264/BT.709 with silent AAC.
- `cover.jpg`: first frame, without answers marked.
- `CAPTION.txt`: unchanged caption for use with either version.
- `episode.json`: placement, motion, reference-character and background inputs.
- `render.py`, `edit.ass`, `requirements.txt`: reproducible video production.
- `verify.py`, `VERIFICATION.md`: export and comparison checks.

The existing source illustration and its generation prompt remain in
`../episode-02-thailand/clean-background.png` and
`../episode-02-thailand/BACKGROUND_PROMPT.txt`. No new image was generated.
The source for the example is the existing waving `taggi-5.png` pose.

## Rebuild

From the repository root with Python 3 and FFmpeg/ffprobe 7+:

```sh
python3 -m pip install -r docs/social/find-taggi/episode-02-thailand-v2/requirements.txt
python3 docs/social/find-taggi/episode-02-thailand-v2/render.py
python3 docs/social/find-taggi/episode-02-thailand-v2/verify.py
```

The renderer reuses the v1 background, original pilot typography helpers, and
the repository's mascot PNGs, Shadows Into Light and Instrument Sans fonts.
Pillow composites character frames; FFmpeg renders the instruction/countdown
and encodes the final video. Only actual font files are copied into a temporary
font directory, excluding Unity metadata and license documents from libass.

Rebuilding overwrites only v2's MP4, cover and ASS file. Verification reads v1
to confirm the puzzle and caption match; keep the sibling episode folders and
`Assets/Resources/Tagtag/` available. No network access is needed to rebuild.
