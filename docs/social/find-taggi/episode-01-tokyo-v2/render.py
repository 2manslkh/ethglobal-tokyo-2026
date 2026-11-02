#!/usr/bin/env python3
"""Render ten separately animated Taggis, including two exact 4x4 sprites."""

import importlib.util
import io
import json
import math
import os
from pathlib import Path
import subprocess
import tempfile
from PIL import Image, ImageDraw
from xml.sax.saxutils import escape


HERE = Path(__file__).resolve().parent
PILOT = HERE.parent / "episode-01-tokyo"
module_spec = importlib.util.spec_from_file_location("pilot_render", PILOT / "render.py")
pilot = importlib.util.module_from_spec(module_spec)
module_spec.loader.exec_module(pilot)
SPEC = json.loads((HERE / "episode.json").read_text())
VIDEO = HERE / "find-taggi-tokyo-v2.mp4"
INK, PAPER, YELLOW = pilot.INK, pilot.PAPER, pilot.YELLOW


def dimensions(target):
    height = target["height"]
    width = target.get("width", round(height * target["crop"][2] / target["crop"][3]))
    margin = 0 if height == 4 else math.ceil(height * .14) + 3
    return width, height, width + 2*margin, height + 2*margin, margin


def center_at(target, at):
    phase = math.tau * min(12, max(0, at-2)) / target["period"] + target["phase"]
    _, _, width, height, _ = dimensions(target)
    x, y = target["center"]
    return (math.floor(x-width/2+target["dx"]*math.sin(phase)) + width/2,
            math.floor(y-height/2+target["dy"]*math.sin(phase+1.1)) + height/2)


def make_ass():
    # Reuse the approved font/style definitions, then author the new timeline.
    header = pilot.make_ass().split("Dialogue:", 1)[0]
    events = []

    def event(start, end, style, tags, content, layer=1):
        events.append(f"Dialogue: {layer},{pilot.timestamp(start)},{pilot.timestamp(end)},{style},,0,0,0,,{{{tags}}}{content}")

    def text(start, end, content, x, y, size, style="Body", align=7, extra="", layer=4):
        event(start, end, style, f"\\an{align}\\pos({x},{y})\\fs{size}\\1c{pilot.ass_color(INK)}{extra}", content, layer)

    def card(start, end, x, y, w, h):
        shape = pilot.rounded_box(w, h)
        event(start, end, "Body", f"\\an7\\pos({x+5},{y+8})\\p1\\bord0\\1c{pilot.ass_color(INK)}\\1a&HCB&\\fad(160,140)", shape, 2)
        event(start, end, "Body", f"\\an7\\pos({x},{y})\\p1\\bord1.5\\3c{pilot.ass_color(INK)}\\1c{pilot.ass_color(PAPER)}\\fad(160,140)", shape, 3)

    text(0, 20, "Find Taggi", 76, 55, 88, "Display")
    text(0, 20, "TOKYO  /  HARD", 900, 112, 25, align=9)
    text(0, 2, "Ten little discoveries on the move.", 78, 174, 32)
    text(2, 14, "Find all 10. Watch closely.", 78, 174, 32)
    text(14, 18, "All ten hiding spots.", 78, 174, 32)
    text(18, 20, "Little discoveries worth keeping.", 78, 174, 32)
    text(2, 14, "Some are only 4 pixels tiny.", 540, 1718, 30, align=8)
    text(0, 16, "tagtag", 540, 1780, 49, "Display", align=8)
    for elapsed in range(12):
        text(2+elapsed, 3+elapsed, f"{12-elapsed:02d}s", 900, 169, 38,
             align=9, extra="\\fnInstrument Sans SemiBold")

    card(0, 2, 150, 510, 780, 820)
    text(0, 2, "Can you find", 540, 555, 79, "Display", 8,
         "\\fad(160,140)\\fscx96\\fscy96\\t(0,300,\\fscx100\\fscy100)")
    text(0, 2, "all 10 Taggis?", 540, 650, 98, "Display", 8, "\\fad(160,140)")
    text(0, 2, "12 seconds. Watch for movement.", 540, 1215, 30, align=8, extra="\\fad(160,140)")
    text(0, 2, "Some are only 4 pixels tiny.", 540, 1265, 27, align=8, extra="\\fad(160,140)")

    for i, target in enumerate(SPEC["targets"]):
        x, y = center_at(target, 14)
        radius = max(18, target["height"]/2+14)
        start = 14 + i*.18
        event(start, 18, "Body", f"\\an5\\pos({x:.2f},{y:.2f})\\p1\\bord1.1\\3c{pilot.ass_color(INK)}\\1c{pilot.ass_color(YELLOW)}\\fad(70,0)\\fscx80\\fscy80\\t(0,220,\\fscx100\\fscy100)", pilot.ring_path(radius, 5 if radius>30 else 3), 5)
        text(start, 18, str(i+1), round(x+radius+4), round(y-radius-8), 24,
             extra=f"\\fnInstrument Sans SemiBold\\bord2\\3c{pilot.ass_color(PAPER)}", layer=6)
        text(start, start+.18 if i<9 else 18, f"{i+1} / 10", 900, 169, 38,
             align=9, extra="\\fnInstrument Sans SemiBold")
    text(16, 18, "The two 4-pixel Taggis, magnified", 540, 1075, 26, align=8)
    text(16, 18, "#9  /  4 x 4 px", 392, 1290, 24, align=8)
    text(16, 18, "#10  /  4 x 4 px", 690, 1290, 24, align=8)

    card(18, 20, 150, 640, 780, 570)
    text(18, 20, "How many did", 540, 691, 87, "Display", 8, "\\fad(160,140)")
    text(18, 20, "you find?", 540, 802, 100, "Display", 8, "\\fad(160,140)")
    text(18, 20, "Tell us your score out of 10.", 540, 972, 32, align=8, extra="\\fad(160,140)")
    text(18, 20, "tagtag", 540, 1073, 58, "Display", 8, "\\fad(160,140)")
    return header + "\n".join(events) + "\n"


def prepare_scene():
    x, y, w, h = SPEC["source_crop"]
    dx, dy, dw, dh = SPEC["scene_rect"]
    encoded = subprocess.check_output([
        "ffmpeg", "-v", "error", "-i", str(HERE / "clean-background.png"),
        "-vf", f"crop={w}:{h}:{x}:{y},scale={dw}:{dh}:flags=lanczos,pad=1080:1920:{dx}:{dy}:color=0x{PAPER},setsar=1",
        "-frames:v", "1", "-f", "image2pipe", "-c:v", "png", "pipe:1"])
    base = Image.open(io.BytesIO(encoded)).convert("RGBA")
    sprites = []
    for target in SPEC["targets"]:
        x, y, w, h = target["crop"]
        width, height, *_ = dimensions(target)
        with Image.open(pilot.PRESETS / f"taggi-{target['preset']}.png") as source:
            sprites.append(source.convert("RGBA").crop((x, y, x+w, y+h)).resize(
                (width, height), Image.Resampling.LANCZOS))
    return base, sprites


def scene_frame(at, base, sprites):
    frame = base.copy()
    if 2 <= at < 18:
        for target, original in zip(SPEC["targets"], sprites):
            phase = math.tau * min(12, max(0, at-2)) / target["period"] + target["phase"]
            sprite = original
            if target["breathe"]:
                height = round(original.height * (1+target["breathe"]*math.sin(phase)))
                sprite = sprite.resize((original.width, height), Image.Resampling.BICUBIC)
            if target["angle"]:
                sprite = sprite.rotate(-math.degrees(target["angle"]*math.sin(phase)),
                                       resample=Image.Resampling.BICUBIC, expand=True)
            cx, cy = target["center"]
            x = math.floor(cx+target["dx"]*math.sin(phase)-sprite.width/2)
            y = math.floor(cy+target["dy"]*math.sin(phase+1.1)-sprite.height/2)
            frame.alpha_composite(sprite, (x, y))
    if 16 <= at < 18:
        zooms = []
        for target in SPEC["targets"][-2:]:
            cx, cy = center_at(target, 14)
            x, y = round(cx)-8, round(cy)-8
            zooms.append(frame.crop((x, y, x+16, y+16)).resize((144, 144), Image.Resampling.NEAREST))
        ImageDraw.Draw(frame).rectangle((250, 1060, 830, 1340), fill="#"+PAPER, outline="#"+INK, width=1)
        frame.alpha_composite(zooms[0], (320, 1130))
        frame.alpha_composite(zooms[1], (618, 1130))
    return frame.convert("RGB")


def video_filters():
    return (
        f"[0:v]ass=filename='{pilot.filter_path(HERE / 'edit.ass')}':fontsdir='{pilot.filter_path(pilot.FONTS)}'[lettered];"
        "[1:v]crop=930:1180:160:40,scale=300:-2:flags=lanczos,format=rgba,"
        "fade=t=in:st=0:d=0.16:alpha=1,fade=t=out:st=1.8:d=0.2:alpha=1[intro];"
        "[lettered][intro]overlay=x=390:y=805:enable='lt(t,2)':format=auto,"
        "scale=out_color_matrix=bt709:out_range=tv,format=yuv420p,"
        "setparams=range=limited:color_primaries=bt709:color_trc=bt709:colorspace=bt709[v]"
    )


def run():
    (HERE / "edit.ass").write_text(make_ass())
    pilot.make_audio(HERE / "sound-effects.wav")
    with tempfile.TemporaryDirectory(prefix="find-taggi-v2-") as scratch:
        scratch = Path(scratch)
        config = scratch / "fonts.conf"
        config.write_text(f'<?xml version="1.0"?><!DOCTYPE fontconfig SYSTEM "urn:fontconfig:fonts.dtd"><fontconfig><dir>{escape(str(pilot.FONTS))}</dir><cachedir>{escape(str(scratch / "font-cache"))}</cachedir></fontconfig>')
        graph = scratch / "filters.txt"
        graph.write_text(video_filters())
        env = dict(os.environ, FONTCONFIG_FILE=str(config))
        base, sprites = prepare_scene()
        command = ["ffmpeg", "-hide_banner", "-y", "-filter_complex_threads", "2",
                   "-f", "rawvideo", "-pixel_format", "rgb24", "-video_size", "1080x1920",
                   "-framerate", "30", "-i", "pipe:0", "-loop", "1", "-framerate", "30",
                   "-i", str(pilot.PRESETS / "taggi-5.png"),
                   "-i", str(HERE / "sound-effects.wav"), "-filter_complex_script", str(graph),
                   "-map", "[v]", "-map", "2:a", "-t", "20", "-r", "30",
                   "-c:v", "libx264", "-preset", "medium", "-crf", "12", "-pix_fmt", "yuv420p",
                   "-color_primaries", "bt709", "-color_trc", "bt709", "-colorspace", "bt709",
                   "-c:a", "aac", "-b:a", "160k", "-ar", "48000", "-ac", "2",
                   "-movflags", "+faststart", "-metadata", "title=Find Taggi — Tokyo / Animated draft 2", str(VIDEO)]
        process = subprocess.Popen(command, stdin=subprocess.PIPE, env=env)
        try:
            for frame_index in range(600):
                process.stdin.write(scene_frame(frame_index/30, base, sprites).tobytes())
            process.stdin.close()
            if process.wait() != 0:
                raise RuntimeError("FFmpeg did not complete the export")
        finally:
            if process.poll() is None:
                process.terminate()
                process.wait()
    for name, at in [("cover.jpg", .9), ("search-preview.jpg", 3.5), ("answer-key.jpg", 17.5)]:
        subprocess.run(["ffmpeg", "-v", "error", "-y", "-ss", str(at), "-i", str(VIDEO),
                        "-frames:v", "1", "-q:v", "2", str(HERE / name)], check=True)
    print(f"Rendered {VIDEO}")


if __name__ == "__main__":
    run()
