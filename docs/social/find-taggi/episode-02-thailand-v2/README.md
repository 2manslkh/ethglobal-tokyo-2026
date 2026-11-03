# Find Taggi: Thailand v2

Thailand v2 opens with a centered cream popup matching the supplied visual
reference: a thin rounded border, handwritten question, large waving Taggi,
and a short time-limit instruction. The popup clears after two seconds,
followed by an 18-second search countdown. Total Reel length remains 20 seconds.

## Edit

| Time | Picture and text |
| --- | --- |
| 0–2 seconds | Cream popup: “Can you find / all 10 Taggis?”, a 300 × 391 px waving example, and “18 seconds. Look closely.” |
| 2–20 seconds | Complete puzzle; “Can you find all 10 Taggis?” above it; countdown from 18s through 01s at the top right. |
| Replay | The introduction plays again. No answer segment or end card. |

The popup fades in over 160 ms and out over the final 140 ms. Its card, mascot
and text disappear together by the first search frame at exactly 2 seconds.
The example is part of the introductory card, not an eleventh hidden target.
There is no permanent example in the header.

## Comparison with v1

The Thai market illustration, ten hiding positions, sprite sizes, independent
animation settings, 20-second duration, silent audio and caption match v1.
The smaller sprite dimension stays at least 24 pixels. The v1 files are intact.

This variant tests the combined introductory popup, instruction and countdown.
V1 offers 20 seconds of unobstructed searching; v2 offers 18 seconds after its
introduction. It does not isolate the effect of the timer alone. The underlying
character animation remains periodic, but the full video now visibly restarts
the popup on replay. No audience-performance result is claimed.

This revision replaces the previous v2 header-example treatment. That earlier
version remains in Git commit c961f02.

## Files

- `find-taggi-thailand-v2.mp4`: 1080 × 1920, 30 fps, H.264/BT.709, silent AAC.
- `cover.jpg`: popup fully visible at 0.9 seconds; no answers marked.
- `search-preview.jpg`: unobstructed search at 3 seconds.
- `CAPTION.txt`: the unchanged Thailand caption.
- `episode.json`: target, popup, example and timing inputs.
- `render.py`, `edit.ass`, `requirements.txt`: reproducible rendering inputs.
- `verify.py`, `VERIFICATION.md`: export, popup and countdown checks.

The source illustration and generation prompt remain in
`../episode-02-thailand/clean-background.png` and
`../episode-02-thailand/BACKGROUND_PROMPT.txt`. No new scene artwork was generated.
The popup mascot uses the original waving `taggi-5.png` pose. Its layout follows
the earlier pilot's centered card, as shown in the user's attached reference.

## Rebuild

From the repository root with Python 3 and FFmpeg/ffprobe 7+:

```sh
python3 -m pip install -r docs/social/find-taggi/episode-02-thailand-v2/requirements.txt
python3 docs/social/find-taggi/episode-02-thailand-v2/render.py
python3 docs/social/find-taggi/episode-02-thailand-v2/verify.py
```

The renderer uses the v1 background, original pilot typography helpers, and
repository mascot PNGs, Shadows Into Light and Instrument Sans fonts. Pillow
composites the character frames and introductory card; FFmpeg renders the
text/countdown and encodes the video. Rendering needs no network access.

Rebuilding replaces only v2's MP4, ASS file, cover and search preview. Keep the
sibling episode folders and `Assets/Resources/Tagtag/` available. Verification
compares source puzzle frames with v1, checks that all 540 search frames are
free of the popup, and inspects every encoded countdown frame.
