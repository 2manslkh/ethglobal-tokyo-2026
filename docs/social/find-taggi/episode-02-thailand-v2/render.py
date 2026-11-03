#!/usr/bin/env python3
"""Thailand popup variant: two-second introduction, then an 18-second search."""

import importlib.util
import io
import json
import math
import os
from pathlib import Path
import shutil
import subprocess
import tempfile
from xml.sax.saxutils import escape

from PIL import Image, ImageDraw

HERE = Path(__file__).resolve().parent
module_spec = importlib.util.spec_from_file_location("pilot_render", HERE.parent / "episode-01-tokyo/render.py")
pilot = importlib.util.module_from_spec(module_spec)
module_spec.loader.exec_module(pilot)
SPEC = json.loads((HERE / "episode.json").read_text())
VIDEO = HERE / "find-taggi-thailand-v2.mp4"
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
    start = SPEC["intro_duration"]
    duration = SPEC["duration"]

    def text(begin, end, style, content, x, y, size, align=7, extra=""):
        tags = f"\\an{align}\\pos({x},{y})\\fs{size}\\1c{pilot.ass_color(INK)}{extra}"
        events.append(f"Dialogue: 1,{pilot.timestamp(begin)},{pilot.timestamp(end)},{style},,0,0,0,,{{{tags}}}{content}")

    for style, content, x, y, size, align in [
        ("Display", "Find Taggi", 76, 55, 88, 7),
        ("Body", "02  /  THAILAND", 900, 112, 25, 9),
        ("Body", "How many have you found?", 540, 1718, 32, 8),
        ("Display", "tagtag", 540, 1780, 49, 8),
    ]:
        text(0, duration, style, content, x, y, size, align)
    text(0, start, "Body", "A little discovery in Thailand.", 78, 174, 32)
    text(start, duration, "Body", "Can you find all 10 Taggis?", 78, 174, 34)
    fade_in, fade_out = (round(value*1000) for value in SPEC["popup_fade"])
    fade = f"\\fad({fade_in},{fade_out})"
    text(0, start, "Display", "Can you find", 540, 555, 79, 8, fade)
    text(0, start, "Display", "all 10 Taggis?", 540, 650, 104, 8, fade)
    text(0, start, "Body", f"{duration-start} seconds. Look closely.", 540, 1220, 32, 8, fade)
    for second in range(start, duration):
        text(second, second+1, "Body", f"{duration-second:02d}s", 900, 169, 38, 9)
    return header + "\n".join(events) + "\n"


def prepare_scene():
    x, y, w, h = SPEC["source_crop"]
    dx, dy, dw, dh = SPEC["scene_rect"]
    data = subprocess.check_output([
        "ffmpeg", "-v", "error", "-i", str(HERE / SPEC["background"]),
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


def prepare_popup():
    popup = Image.new("RGBA", (SPEC["width"], SPEC["height"]), (0,0,0,0))
    drawing = ImageDraw.Draw(popup)
    x,y,w,h = SPEC["popup_rect"]
    drawing.rounded_rectangle((x+5,y+8,x+w+5,y+h+8), radius=22, fill=(52,51,47,52))
    drawing.rounded_rectangle((x,y,x+w,y+h), radius=22, fill=f"#{PAPER}", outline=f"#{INK}", width=2)
    example = SPEC["example"]
    x,y,w,h = example["crop"]
    with Image.open(pilot.PRESETS / f"taggi-{example['preset']}.png") as source:
        reference = source.convert("RGBA").crop((x,y,x+w,y+h)).resize(
            (round(w*example["height"]/h), example["height"]), Image.Resampling.LANCZOS)
    popup.alpha_composite(reference, tuple(example["position"]))
    return popup


def popup_alpha(at):
    if not 0 <= at < SPEC["intro_duration"]:
        return 0
    fade_in, fade_out = SPEC["popup_fade"]
    return min(1, at/fade_in, (SPEC["intro_duration"]-at)/fade_out)


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


def video_frame(at, base, sprites, popup):
    frame = scene_frame(at, base, sprites)
    opacity = popup_alpha(at)
    if opacity:
        layer = popup.copy()
        layer.putalpha(popup.getchannel("A").point(lambda value: round(value*opacity)))
        frame = frame.convert("RGBA")
        frame.alpha_composite(layer)
    return frame.convert("RGB")


def run():
    (HERE / "edit.ass").write_text(make_ass())
    base, sprites = prepare_scene()
    popup = prepare_popup()
    with tempfile.TemporaryDirectory(prefix="find-taggi-thailand-v2-") as scratch:
        scratch = Path(scratch)
        fonts = scratch / "fonts"
        fonts.mkdir()
        for name in ("ShadowsIntoLight.ttf", "InstrumentRegular.ttf"):
            shutil.copy2(pilot.FONTS / name, fonts / name)
        filters = (f"ass=filename='{pilot.filter_path(HERE / 'edit.ass')}':fontsdir='{pilot.filter_path(fonts)}',"
                   "scale=out_color_matrix=bt709:out_range=tv,format=yuv420p,"
                   "setparams=range=limited:color_primaries=bt709:color_trc=bt709:colorspace=bt709")
        config = scratch / "fonts.conf"
        config.write_text(f'<?xml version="1.0"?><!DOCTYPE fontconfig SYSTEM "urn:fontconfig:fonts.dtd"><fontconfig><dir>{escape(str(fonts))}</dir><cachedir>{escape(str(scratch / "font-cache"))}</cachedir></fontconfig>')
        env = dict(os.environ, FONTCONFIG_FILE=str(config))
        command = ["ffmpeg", "-hide_banner", "-y", "-filter_threads", "2",
                   "-f", "rawvideo", "-pixel_format", "rgb24", "-video_size", "1080x1920",
                   "-framerate", "30", "-i", "pipe:0", "-f", "lavfi", "-i", "anullsrc=r=48000:cl=stereo",
                   "-vf", filters, "-map", "0:v", "-map", "1:a", "-t", "20", "-r", "30",
                   "-c:v", "libx264", "-preset", "medium", "-crf", "12", "-pix_fmt", "yuv420p",
                   "-color_primaries", "bt709", "-color_trc", "bt709", "-colorspace", "bt709",
                   "-c:a", "aac", "-b:a", "128k", "-ar", "48000", "-ac", "2",
                   "-movflags", "+faststart", "-metadata", f"title={SPEC['title']}", str(VIDEO)]
        process = subprocess.Popen(command, stdin=subprocess.PIPE, env=env)
        try:
            for index in range(SPEC["duration"]*SPEC["fps"]):
                process.stdin.write(video_frame(index/SPEC["fps"], base, sprites, popup).tobytes())
            process.stdin.close()
            if process.wait() != 0:
                raise RuntimeError("FFmpeg did not complete the export")
        finally:
            if process.poll() is None:
                process.terminate()
                process.wait()
    for filename, at in [("cover.jpg", .9), ("search-preview.jpg", 3)]:
        subprocess.run(["ffmpeg", "-v", "error", "-y", "-ss", str(at), "-i", str(VIDEO),
                        "-frames:v", "1", "-q:v", "2", str(HERE / filename)], check=True)
    print(f"Rendered {VIDEO}")


if __name__ == "__main__":
    run()
