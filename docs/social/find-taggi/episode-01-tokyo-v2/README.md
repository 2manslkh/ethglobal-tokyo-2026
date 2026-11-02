# Find Taggi: animated draft 2

Ten individually animated Taggis in the original Tokyo street, with two tiny
4 × 4-pixel targets. The first draft is preserved in the sibling episode folder.

## Deliverables

- `find-taggi-tokyo-v2.mp4`: 20-second, 1080 × 1920, 30 fps H.264/AAC Reel.
- `cover.jpg`, `CAPTION.txt`: cover and caption for this draft.
- `search-preview.jpg`: representative frame during the challenge.
- `answer-key.jpg`: ten numbered solutions and magnified micro-target locations.
- `clean-background.png`, `BACKGROUND_PROMPT.txt`: background plate and exact
  built-in image-generation edit prompt. The three original baked-in Taggis were
  removed before compositing the ten animated targets.
- `episode.json`: positions, preset IDs, sprite sizes and individual motion settings.
- `render.py`, `edit.ass`, `sound-effects.wav`: reproducible edit and sound effects.
- `verify.py`, `VERIFICATION.md`: encoded-output checks and review results.

## Timing

| Time | Content |
| --- | --- |
| 0–2 s | “Can you find all 10 Taggis?” with a large reference mascot. |
| 2–14 s | Twelve-second search; all ten move independently against a fixed scene. |
| 14–18 s | Characters hold their final positions; ten numbered rings appear in sequence. |
| 16–18 s | Two inset crops magnify the actual 4-pixel hiding spots. |
| 18–20 s | “How many did you find?” and an invitation to report a score out of ten. |

The target heights are 72, 60, 48, 40, 32, 26, 18, 12, 4 and 4 pixels before
rotation/breathing. The last two use exactly 4 × 4 RGBA sprite canvases at the
final 1080 × 1920 resolution, with one-pixel movement and no scaling or rotation.
At that size their poses cannot be recognized normally: these are deliberately
extreme targets. The insets enlarge the real low-resolution scene pixels, not
higher-resolution substitute characters. Instagram recompression may soften them
further; no upload test is claimed.

Animation uses separately phased rocking, bobbing and breathing. These are
cutout animations of the original mascot poses, not newly drawn limb-animation
cycles. The original PNG artwork and eight distinct poses supply all ten targets.
The background contains no additional targets. Sound effects retain the first
draft's soft opening pop, countdown ticks, reveal chimes and closing chime.

## Rebuild

From the repository root, with Python 3 and FFmpeg/ffprobe 7+:

```sh
python3 -m pip install -r docs/social/find-taggi/episode-01-tokyo-v2/requirements.txt
python3 docs/social/find-taggi/episode-01-tokyo-v2/render.py
python3 docs/social/find-taggi/episode-01-tokyo-v2/verify.py
```

This draft reuses font, subtitle-geometry and sound helpers from the sibling
`episode-01-tokyo/render.py`, and fonts/presets from the app's existing resources.
Keep those paths available when rebuilding. The generated clean background is
local, so rendering needs no image-generation call, network access or API key.

Pillow renders the ten transparent cutouts into a single RGB video stream.
FFmpeg prepares the background layout, adds the titles and animated answer rings,
mixes the sound track and encodes the final MP4. This avoids the local FFmpeg 7.1
stall observed after ten frames when chaining twelve looping image inputs.
The final full-length render and output checks exercise the corrected path.

The renderer overwrites only this draft's derived MP4, WAV, ASS and JPG outputs.
Original assets and the first draft remain intact. Work was coordinated locally;
this chat has no Herdr-managed pane, so no parallel editing agents were used.
