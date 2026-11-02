#!/usr/bin/env python3
"""A continuous 20-second search loop with ten animated targets, minimum 24 px."""

import importlib.util
import io
import json
import math
import os
from pathlib import Path
import subprocess
import tempfile
from xml.sax.saxutils import escape

from PIL import Image

HERE = Path(__file__).resolve().parent
module_spec = importlib.util.spec_from_file_location("pilot_render", HERE.parent / "episode-01-tokyo/render.py")
pilot = importlib.util.module_from_spec(module_spec)
module_spec.loader.exec_module(pilot)
SPEC = json.loads((HERE / "episode.json").read_text())
VIDEO = HERE / "find-taggi-tokyo-v3.mp4"
INK, PAPER = pilot.INK, pilot.PAPER


def dimensions(target):
    # Preserve the drawing's proportions while enforcing both dimensions.
    crop_width, crop_height = target["crop"][2:]
    scale = max(target["height"]/crop_height,
                SPEC["min_target_side"]/crop_width,
                SPEC["min_target_side"]/crop_height)
    return math.ceil(crop_width*scale), math.ceil(crop_height*scale)


def make_ass():
    header = pilot.make_ass().split("Dialogue:", 1)[0]
    events = []
    for style, text, x, y, size, align in [
        ("Display", "Find Taggi", 76, 55, 88, 7),
        ("Body", "TOKYO", 900, 112, 25, 9),
        ("Body", "Can you find all 10?", 78, 174, 34, 7),
        ("Body", "How many have you found?", 540, 1718, 32, 8),
        ("Display", "tagtag", 540, 1780, 49, 8),
    ]:
        tags = f"\\an{align}\\pos({x},{y})\\fs{size}\\1c{pilot.ass_color(INK)}"
        events.append(f"Dialogue: 1,0:00:00.00,0:00:20.00,{style},,0,0,0,,{{{tags}}}{text}")
    return header + "\n".join(events) + "\n"


def prepare_scene():
    x, y, w, h = SPEC["source_crop"]
    dx, dy, dw, dh = SPEC["scene_rect"]
    data = subprocess.check_output([
        "ffmpeg", "-v", "error", "-i", str(HERE / "clean-background.png"),
        "-vf", f"crop={w}:{h}:{x}:{y},scale={dw}:{dh}:flags=lanczos,pad=1080:1920:{dx}:{dy}:color=0x{PAPER},setsar=1",
        "-frames:v", "1", "-f", "image2pipe", "-c:v", "png", "pipe:1"])
    base = Image.open(io.BytesIO(data)).convert("RGBA")
    sprites = []
    for target in SPEC["targets"]:
        x, y, w, h = target["crop"]
        with Image.open(pilot.PRESETS / f"taggi-{target['preset']}.png") as source:
            sprites.append(source.convert("RGBA").crop((x, y, x+w, y+h)).resize(
                dimensions(target), Image.Resampling.LANCZOS))
    return base, sprites


def phase_at(target, at):
    return math.tau * target["cycles_per_loop"] * ((at % SPEC["duration"])/SPEC["duration"]) + target["phase"]


def animated_sprite(target, original, at):
    phase = phase_at(target, at)
    sprite = original
    if target["breathe"]:
        height = max(SPEC["min_target_side"], round(original.height*(1+target["breathe"]*math.sin(phase))))
        sprite = sprite.resize((original.width, height), Image.Resampling.BICUBIC)
    if target["angle"]:
        sprite = sprite.rotate(-math.degrees(target["angle"]*math.sin(phase)),
                               resample=Image.Resampling.BICUBIC, expand=True)
    cx, cy = target["center"]
    return sprite, (math.floor(cx+target["dx"]*math.sin(phase)-sprite.width/2),
                    math.floor(cy+target["dy"]*math.sin(phase+1.1)-sprite.height/2))


def scene_frame(at, base, sprites):
    frame = base.copy()
    for target, original in zip(SPEC["targets"], sprites):
        sprite, position = animated_sprite(target, original, at)
        frame.alpha_composite(sprite, position)
    return frame.convert("RGB")


def run():
    (HERE / "edit.ass").write_text(make_ass())
    base, sprites = prepare_scene()
    filters = (f"ass=filename='{pilot.filter_path(HERE / 'edit.ass')}':fontsdir='{pilot.filter_path(pilot.FONTS)}',"
               "scale=out_color_matrix=bt709:out_range=tv,format=yuv420p,"
               "setparams=range=limited:color_primaries=bt709:color_trc=bt709:colorspace=bt709")
    with tempfile.TemporaryDirectory(prefix="find-taggi-v3-") as scratch:
        scratch = Path(scratch)
        config = scratch / "fonts.conf"
        config.write_text(f'<?xml version="1.0"?><!DOCTYPE fontconfig SYSTEM "urn:fontconfig:fonts.dtd"><fontconfig><dir>{escape(str(pilot.FONTS))}</dir><cachedir>{escape(str(scratch / "font-cache"))}</cachedir></fontconfig>')
        env = dict(os.environ, FONTCONFIG_FILE=str(config))
        command = ["ffmpeg", "-hide_banner", "-y", "-filter_threads", "2",
                   "-f", "rawvideo", "-pixel_format", "rgb24", "-video_size", "1080x1920",
                   "-framerate", "30", "-i", "pipe:0", "-f", "lavfi", "-i", "anullsrc=r=48000:cl=stereo",
                   "-vf", filters, "-map", "0:v", "-map", "1:a", "-t", "20", "-r", "30",
                   "-c:v", "libx264", "-preset", "medium", "-crf", "12", "-pix_fmt", "yuv420p",
                   "-color_primaries", "bt709", "-color_trc", "bt709", "-colorspace", "bt709",
                   "-c:a", "aac", "-b:a", "128k", "-ar", "48000", "-ac", "2",
                   "-movflags", "+faststart", "-metadata", "title=Find Taggi — Tokyo / Continuous loop draft 3", str(VIDEO)]
        process = subprocess.Popen(command, stdin=subprocess.PIPE, env=env)
        try:
            for index in range(SPEC["duration"]*SPEC["fps"]):
                process.stdin.write(scene_frame(index/SPEC["fps"], base, sprites).tobytes())
            process.stdin.close()
            if process.wait() != 0:
                raise RuntimeError("FFmpeg did not complete the export")
        finally:
            if process.poll() is None:
                process.terminate()
                process.wait()
    subprocess.run(["ffmpeg", "-v", "error", "-y", "-i", str(VIDEO),
                    "-frames:v", "1", "-q:v", "2", str(HERE / "cover.jpg")], check=True)
    print(f"Rendered {VIDEO}")


if __name__ == "__main__":
    run()
