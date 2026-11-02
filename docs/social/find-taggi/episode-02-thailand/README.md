# Find Taggi: Thailand

Ten animated Taggis hide in an illustrated Thai canal market. The entire
20-second Reel is a continuous search, with constant text and a looping scene.
Each target is at least 24 pixels in both dimensions. There is no countdown,
answer reveal, introduction card or end card. The AAC track is silent.

## Delivery

- `find-taggi-thailand.mp4`: 1080 × 1920, 30 fps, H.264/yuv420p, BT.709, AAC.
- `cover.jpg`: the first frame, with no answers marked.
- `CAPTION.txt`: the ready-to-copy English caption.
- `clean-background.png`: original generated illustration, 1024 × 1536.
- `BACKGROUND_PROMPT.txt`: exact generation prompt.
- `episode.json`: internal target positions, pose crops and animation settings.
- `render.py`, `edit.ass`, `requirements.txt`: reproducible rendering inputs.
- `verify.py`, `VERIFICATION.md`: export checks and recorded results.

The background depicts an imagined canal market, rather than a specific real
location. It was generated with the built-in image-generation tool, using
`../episode-01-tokyo-v3/clean-background.png` solely as a style reference.
The plate contains no Taggis; ten transparent app-artwork sprites are composited
onto it separately, preserving their white bodies and handmade black linework.

## Rebuild

From the repository root, with Python 3 and FFmpeg/ffprobe 7+ installed:

```sh
python3 -m pip install -r docs/social/find-taggi/episode-02-thailand/requirements.txt
python3 docs/social/find-taggi/episode-02-thailand/render.py
python3 docs/social/find-taggi/episode-02-thailand/verify.py
```

The renderer imports typography helpers from `../episode-01-tokyo/render.py`.
It uses the existing Shadows Into Light and Instrument Sans fonts in
`Assets/Resources/Tagtag/Fonts/`, and mascot poses in
`Assets/Resources/Tagtag/Presets/`. Keep those repository assets available.
Rebuilding needs no image-generation call or network access and overwrites only
this episode's video, cover and ASS file.

FFmpeg crops four pixels from the top and bottom of the background, then fits
it into the scene rectangle at (60, 220), sized 960 × 1432. Pillow composites
the individual character animations into one RGB video stream. FFmpeg renders
the constant typography and encodes the final output.

## Motion and placement

Waving, heart-hugging, sleeping, cheering, laughing, surprised, shy and thinking
poses are distributed across the scene. Each target bobs, rocks or breathes with
its own phase; five to nine complete cycles fit exactly into 20 seconds. The
source animation at 20 seconds equals its starting frame. The export contains
600 frames, with no duplicated endpoint frame.

The shorter sprite dimension stays at or above 24 pixels, preserving the
original proportions. Every target stays clear of the top 250 pixels, bottom
350 pixels and rightmost 150 pixels throughout its motion. Coordinates are
retained only as production inputs; no answer-key image is produced.
