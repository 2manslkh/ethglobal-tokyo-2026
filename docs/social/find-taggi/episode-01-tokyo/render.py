#!/usr/bin/env python3
"""Render the approved pilot with Python's standard library and FFmpeg 7+."""

import array
import json
import math
import os
from pathlib import Path
import subprocess
import sys
import tempfile
import wave
from xml.sax.saxutils import escape


HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[3]
FONTS = ROOT / "Assets/Resources/Tagtag/Fonts"
PRESETS = ROOT / "Assets/Resources/Tagtag/Presets"
SPEC = json.loads((HERE / "episode.json").read_text())
INK = "34332F"
PAPER = "FFF6E8"
YELLOW = "F6CE55"


def ass_color(rgb):
    return "&H" + rgb[4:6] + rgb[2:4] + rgb[0:2] + "&"


def timestamp(seconds):
    centiseconds = round(seconds * 100)
    return f"{centiseconds // 360000}:{centiseconds // 6000 % 60:02d}:{centiseconds // 100 % 60:02d}.{centiseconds % 100:02d}"


def target_center(target):
    left, top, right, bottom = target["source_box"]
    crop_x, crop_y, crop_w, crop_h = SPEC["source_crop"]
    x, y, w, h = SPEC["scene_rect"]
    return (x + ((left + right) / 2 - crop_x) * w / crop_w,
            y + ((top + bottom) / 2 - crop_y) * h / crop_h)


def ring_path(radius, thickness):
    """Opposite winding directions leave a transparent hole in the ring."""
    paths = []
    for r, direction in [(radius, 1), (radius - thickness, -1)]:
        points = [(radius + r * math.cos(direction * i * math.tau / 120),
                   radius + r * math.sin(direction * i * math.tau / 120))
                  for i in range(121)]
        paths.append("m " + " l ".join(f"{x:.2f} {y:.2f}" for x, y in points))
    return " ".join(paths)


def rounded_box(width, height, radius=22):
    k = radius * 0.55228475
    r, w, h = radius, width, height
    return (f"m {r} 0 l {w-r} 0 b {w-r+k} 0 {w} {r-k} {w} {r} "
            f"l {w} {h-r} b {w} {h-r+k} {w-r+k} {h} {w-r} {h} "
            f"l {r} {h} b {r-k} {h} 0 {h-r+k} 0 {h-r} "
            f"l 0 {r} b 0 {r-k} {r-k} 0 {r} 0")


def make_ass():
    header = f"""[Script Info]
ScriptType: v4.00+
PlayResX: 1080
PlayResY: 1920
WrapStyle: 2
ScaledBorderAndShadow: yes

[V4+ Styles]
Format: Name, Fontname, Fontsize, PrimaryColour, SecondaryColour, OutlineColour, BackColour, Bold, Italic, Underline, StrikeOut, ScaleX, ScaleY, Spacing, Angle, BorderStyle, Outline, Shadow, Alignment, MarginL, MarginR, MarginV, Encoding
Style: Display,Shadows Into Light,86,&H0034332F,&H0034332F,&H0034332F,&H00000000,0,0,0,0,100,100,0,0,1,0,0,7,0,0,0,1
Style: Body,Instrument Sans,34,&H0034332F,&H0034332F,&H0034332F,&H00000000,0,0,0,0,100,100,0,0,1,0,0,7,0,0,0,1

[Events]
Format: Layer, Start, End, Style, Name, MarginL, MarginR, MarginV, Effect, Text
"""
    events = []

    def event(start, end, style, tags, content, layer=1):
        events.append(f"Dialogue: {layer},{timestamp(start)},{timestamp(end)},{style},,0,0,0,,{{{tags}}}{content}")

    def text(start, end, content, x, y, size, style="Body", align=7, extra="", layer=4):
        event(start, end, style, f"\\an{align}\\pos({x},{y})\\fs{size}\\1c{ass_color(INK)}{extra}", content, layer)

    def card(start, end, x, y, w, h):
        shape = rounded_box(w, h)
        event(start, end, "Body", f"\\an7\\pos({x+5},{y+8})\\p1\\bord0\\1c{ass_color(INK)}\\1a&HCB&\\fad(160,140)", shape, 2)
        event(start, end, "Body", f"\\an7\\pos({x},{y})\\p1\\bord1.5\\3c{ass_color(INK)}\\1c{ass_color(PAPER)}\\fad(160,140)", shape, 3)

    text(0, 20, "Find Taggi", 76, 55, 88, "Display")
    text(0, 20, "01  /  TOKYO", 900, 112, 25, align=9)
    text(0, 2, "A little discovery in Tokyo.", 78, 174, 32)
    text(2, 14, "Find all 3. Look closely.", 78, 174, 32)
    text(14, 18, "There they are.", 78, 174, 32)
    text(18, 20, "A little discovery in Tokyo.", 78, 174, 32)
    # Quiet footer; all essential instructions remain above interface clearance.
    text(2, 18, "Little discoveries worth keeping.", 540, 1718, 29, align=8)
    text(0, 18, "tagtag", 540, 1780, 49, "Display", align=8)
    for elapsed in range(12):
        text(2 + elapsed, 3 + elapsed, f"{12-elapsed:02d}s", 900, 169, 38, align=9, extra="\\fnInstrument Sans SemiBold")

    card(0, 2, 150, 510, 780, 800)
    text(0, 2, "Can you find", 540, 555, 79, "Display", 8, "\\fad(160,140)\\fscx96\\fscy96\\t(0,300,\\fscx100\\fscy100)")
    text(0, 2, "all 3 Taggis?", 540, 650, 104, "Display", 8, "\\fad(160,140)")
    text(0, 2, "12 seconds. Look closely.", 540, 1220, 32, align=8, extra="\\fad(160,140)")

    for i, target in enumerate(SPEC["targets"], 1):
        x, y = target_center(target)
        start = target["reveal_at"]
        event(start, 18, "Body", f"\\an5\\pos({x:.2f},{y:.2f})\\p1\\bord1.4\\3c{ass_color(INK)}\\1c{ass_color(YELLOW)}\\fad(90,0)\\fscx78\\fscy78\\t(0,280,\\fscx105\\fscy105)\\t(280,430,\\fscx100\\fscy100)", ring_path(72, 9), 5)
        text(start, start+1 if i < 3 else 18, f"{i} / 3", 900, 169, 38, align=9, extra="\\fnInstrument Sans SemiBold")

    card(18, 20, 150, 640, 780, 570)
    text(18, 20, "How many did", 540, 691, 87, "Display", 8, "\\fad(160,140)")
    text(18, 20, "you find?", 540, 802, 100, "Display", 8, "\\fad(160,140)")
    text(18, 20, "Tell us below.", 540, 972, 35, align=8, extra="\\fad(160,140)")
    text(18, 20, "tagtag", 540, 1073, 58, "Display", 8, "\\fad(160,140)")
    return header + "\n".join(events) + "\n"


def make_audio(destination):
    rate = 48000
    samples = array.array("f", [0]) * (rate * SPEC["duration"])

    def tone(start, length, frequency, amplitude, decay, sweep=0):
        offset = round(start * rate)
        for i in range(round(length * rate)):
            t = i / rate
            attack = min(1, t / .008)
            release = min(1, (length-t) / .025)
            envelope = attack * release * math.exp(-decay * t)
            phase = math.tau * (frequency*t + sweep*t*t/2)
            samples[offset+i] += amplitude * envelope * (math.sin(phase) + .18*math.sin(phase*2))

    tone(.20, .24, 460, .13, 15, -400)
    for start in (11, 12, 13):
        tone(start, .09, 720, .075, 42)
    for start, frequency in ((14, 660), (15, 784), (16, 988)):
        tone(start+.03, .65, frequency, .13, 5)
        tone(start+.08, .48, frequency*1.5, .045, 7)
    tone(18.18, .8, 523.25, .09, 4)
    tone(18.27, .7, 659.25, .075, 4)
    pcm = array.array("h", (round(max(-1, min(1, s))*32767) for s in samples))
    if sys.byteorder != "little":
        pcm.byteswap()
    with wave.open(str(destination), "wb") as output:
        output.setnchannels(1)
        output.setsampwidth(2)
        output.setframerate(rate)
        output.writeframes(pcm.tobytes())


def filter_path(path):
    return str(path).replace("\\", "\\\\").replace(":", "\\:").replace("'", "'\\''")


def run():
    for path in [HERE / "source-illustration.png", FONTS / "ShadowsIntoLight.ttf", PRESETS / "taggi-5.png"]:
        if not path.is_file():
            raise SystemExit(f"Missing required asset: {path}")
    with tempfile.TemporaryDirectory(prefix="find-taggi-") as scratch:
        scratch = Path(scratch)
        (HERE / "edit.ass").write_text(make_ass())
        make_audio(HERE / "sound-effects.wav")
        config = scratch / "fonts.conf"
        config.write_text(f'<?xml version="1.0"?><!DOCTYPE fontconfig SYSTEM "urn:fontconfig:fonts.dtd"><fontconfig><dir>{escape(str(FONTS))}</dir><cachedir>{escape(str(scratch / "font-cache"))}</cachedir></fontconfig>')
        env = dict(os.environ, FONTCONFIG_FILE=str(config))
        x, y, w, h = SPEC["source_crop"]
        dx, dy, dw, dh = SPEC["scene_rect"]
        filters = (
            f"[0:v]crop={w}:{h}:{x}:{y},scale={dw}:{dh}:flags=lanczos,"
            f"pad=1080:1920:{dx}:{dy}:color=0x{PAPER},setsar=1,"
            f"ass=filename='{filter_path(HERE / 'edit.ass')}':fontsdir='{filter_path(FONTS)}'[lettered];"
            "[1:v]crop=930:1180:160:40,scale=300:-2:flags=lanczos,format=rgba,"
            "fade=t=in:st=0:d=0.16:alpha=1,fade=t=out:st=1.8:d=0.2:alpha=1[mascot];"
            "[lettered][mascot]overlay=x=390:y=805:enable='lt(t,2)':format=auto,"
            "scale=out_color_matrix=bt709:out_range=tv,format=yuv420p,"
            "setparams=range=limited:color_primaries=bt709:color_trc=bt709:colorspace=bt709[v]"
        )
        graph = scratch / "filters.txt"
        graph.write_text(filters)
        command = [
            "ffmpeg", "-hide_banner", "-y", "-filter_complex_threads", "2",
            "-loop", "1", "-framerate", "30", "-i", str(HERE / "source-illustration.png"),
            "-loop", "1", "-framerate", "30", "-i", str(PRESETS / "taggi-5.png"),
            "-i", str(HERE / "sound-effects.wav"), "-filter_complex_script", str(graph),
            "-map", "[v]", "-map", "2:a", "-t", "20", "-r", "30",
            "-c:v", "libx264", "-preset", "medium", "-crf", "18", "-pix_fmt", "yuv420p",
            "-color_primaries", "bt709", "-color_trc", "bt709", "-colorspace", "bt709",
            "-c:a", "aac", "-b:a", "160k", "-ar", "48000", "-ac", "2",
            "-movflags", "+faststart", "-metadata", "title=Find Taggi — Tokyo / Episode 01",
            str(HERE / "find-taggi-tokyo.mp4"),
        ]
        subprocess.run(command, check=True, env=env)
        for name, at in [("cover.jpg", .9), ("search-preview.jpg", 3.5), ("answer-key.jpg", 17.5)]:
            subprocess.run(["ffmpeg", "-v", "error", "-y", "-ss", str(at), "-i",
                            str(HERE / "find-taggi-tokyo.mp4"), "-frames:v", "1",
                            "-q:v", "2", str(HERE / name)], check=True)
    print(f"Rendered {HERE / 'find-taggi-tokyo.mp4'}")


if __name__ == "__main__":
    run()
