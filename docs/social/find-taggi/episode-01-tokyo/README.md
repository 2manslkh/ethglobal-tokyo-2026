# Find Taggi: Tokyo

A 20-second, vertical hidden-character Reel. Find the waving, heart-hugging,
and sleepy Taggis, then see the answers. English text, original soft sound
effects, no narration or background music.

## Deliverables

- `find-taggi-tokyo.mp4`: final 1080 × 1920, 30 fps H.264/AAC video.
- `cover.jpg`: spoiler-free opening cover, without answer markers.
- `CAPTION.txt`: the approved Instagram caption.
- `source-illustration.png`: final generated master, 916 × 1717.
- `search-preview.jpg`: clean search-frame preview.
- `answer-key.jpg`: all three answers, for internal reference.
- `sound-effects.wav`: original synthesized sound effects, 48 kHz mono source.
- `PROMPT.txt` and `REFINEMENT.txt`: exact generation and refinement prompts.
- `episode.json`: source crop, placement, target bounding boxes and reveal times.
- `render.py` and `edit.ass`: reproducible edit and generated subtitle/vector track.
- `verify.py`: checks the actual exported video, timing, image stability and audio.

## Rebuild

From the repository root, with Python 3 and FFmpeg/ffprobe 7 or newer on PATH:

```sh
python3 docs/social/find-taggi/episode-01-tokyo/render.py
python3 docs/social/find-taggi/episode-01-tokyo/verify.py
```

No Python packages, API keys or network access are required to rebuild the
approved edit. FFmpeg must include libx264, AAC, libass and PNG decoding.
Rendering uses the existing project fonts and `taggi-5.png` for the introduction.
Fontconfig uses a temporary cache, without installing fonts into the OS.
The render command overwrites this episode's derived video, audio, subtitle track
and three JPG previews; it leaves the source illustration and prompts intact.

## Edit

| Time | Content |
| --- | --- |
| 0–2 s | “Can you find all 3 Taggis?” with the original waving mascot. |
| 2–14 s | Stationary puzzle; countdown from 12 to 1. Soft ticks at 11, 12 and 13 s. |
| 14–18 s | Yellow rings appear at 14, 15 and 16 s; all remain visible through 18 s. |
| 18–20 s | “How many did you find?” / “Tell us below.” / tagtag. |

## Artwork and layout

Created with the built-in image-generation tool on 2026-11-02. The original
Taggi poses 5, 6 and 8 supplied the character references. A second generation
reduced the three figures and filled the surrounding displays to improve the
search difficulty. The final source contains exactly three intentional Taggis;
all three were visually inspected along with the rest of the scene.

The master includes extra paper space. FFmpeg crops to source rectangle
`(0, 200, 916, 1367)`, then fits it into the video rectangle
`(60, 220, 960, 1432)`. The removed strips contain blank paper, not targets.
Target coordinates are measured in the original master and transformed by the
same crop and scale for the reveal. All targets remain above the bottom 350-pixel
clearance and left of the rightmost 150 pixels. The scene itself extends into
those margins; essential instructions stay in the header and central cards.

Typography uses Shadows Into Light and the actual Instrument Sans Regular and
SemiBold font files already bundled with the app. Sound effects are synthesized
from sine waves in `render.py`; there are no third-party music recordings.

## Production record

Implemented in the existing checkout on `codex/find-taggi-reel`, with edits
restricted to this episode directory. No parallel editing assignments were used.
Herdr orchestration was unavailable because this chat is not a Herdr-managed pane;
the coordinator performed the export and visual review locally.

The check results and manual review are recorded in `VERIFICATION.md`.
