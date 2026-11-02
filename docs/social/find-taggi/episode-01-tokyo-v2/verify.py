#!/usr/bin/env python3
"""Check motion, tiny-target visibility, reveal markers and the actual export."""

import array
import io
import json
import math
import subprocess
import sys

from PIL import Image, ImageChops
from render import HERE, SPEC, VIDEO, center_at, prepare_scene


def frame(at):
    data = subprocess.check_output(["ffmpeg", "-v", "error", "-ss", str(at),
                                    "-i", str(VIDEO), "-frames:v", "1", "-f",
                                    "image2pipe", "-c:v", "png", "pipe:1"])
    return Image.open(io.BytesIO(data)).convert("RGB")


def check():
    result = json.loads(subprocess.check_output([
        "ffprobe", "-v", "error", "-count_frames", "-show_streams", "-show_format",
        "-of", "json", str(VIDEO)]))
    video = next(s for s in result["streams"] if s["codec_type"] == "video")
    audio = next(s for s in result["streams"] if s["codec_type"] == "audio")
    assert (video["width"], video["height"], video["codec_name"], video["pix_fmt"]) == (1080, 1920, "h264", "yuv420p")
    assert video["r_frame_rate"] == "30/1" and int(video["nb_read_frames"]) == 600
    assert video["color_space"] == video["color_transfer"] == video["color_primaries"] == "bt709"
    assert abs(float(result["format"]["duration"]) - 20) < .01
    assert audio["codec_name"] == "aac" and audio["channels"] == 2
    print("PASS: 20.000 s, 600 frames, 1080x1920, 30 fps, H.264/BT.709, stereo AAC")

    times = (2.2, 2.8, 3.6, 4.7, 6.1)
    frames = [frame(at) for at in times]
    exclusions = []
    assert len(SPEC["targets"]) == 10
    for target in SPEC["targets"]:
        cx, cy = target["center"]
        radius = max(10, target["height"]+8)
        box = (cx-radius, cy-radius, cx+radius, cy+radius)
        exclusions.append(box)
        changes = []
        for later in frames[1:]:
            diff = ImageChops.difference(frames[0].crop(box), later.crop(box)).convert("L")
            changes.append(sum(diff.histogram()[21:]))
        assert max(changes) >= 2, f"No visible animation for target {target['id']}: {changes}"
        print(f"PASS: target {target['id']:02d} animates ({max(changes)} pixels change by >20/255)")
    # Background stays stationary outside the independently animated regions.
    differences = ImageChops.difference(frames[0], frames[-1]).convert("L")
    samples = []
    for y in range(250, 1640, 5):
        for x in range(70, 1000, 5):
            if not any(l <= x <= r and t <= y <= b for l, t, r, b in exclusions):
                samples.append(differences.getpixel((x, y)))
    mean = sum(samples)/len(samples)
    assert mean < 1.5, f"Background movement detected: {mean}"
    print(f"PASS: background stationary (mean sampled difference {mean:.4f}/255)")

    _, sprites = prepare_scene()
    clean = frame(18.5)
    for target, sprite in zip(SPEC["targets"][-2:], sprites[-2:]):
        assert sprite.size == (4, 4) and target["angle"] == target["breathe"] == 0
        cx, cy = center_at(target, times[0])
        box = (int(cx)-2, int(cy)-2, int(cx)+2, int(cy)+2)
        diff = ImageChops.difference(frames[0].crop(box), clean.crop(box)).convert("L")
        count = sum(diff.histogram()[16:])
        assert count >= 2, f"Micro target {target['id']} did not survive encoding"
        print(f"PASS: target {target['id']} has a 4x4 source canvas and remains visible after encoding ({count}/16 pixels)")

    reveal, before = frame(17.5), frame(13.5)
    for target in SPEC["targets"]:
        cx, cy = center_at(target, 14)
        radius = max(18, target["height"]/2+14)
        thickness = 5 if radius > 30 else 3
        assert cx-radius > 60 and cx+radius < 930 and cy-radius > 250 and cy+radius < 1570
        differences = []
        for i in range(100):
            angle = i*math.tau/100
            pixel = (round(cx+(radius-thickness/2)*math.cos(angle)),
                     round(cy+(radius-thickness/2)*math.sin(angle)))
            differences.append(sum(abs(a-b) for a,b in zip(reveal.getpixel(pixel), before.getpixel(pixel)))/3)
        assert sum(d > 15 for d in differences) >= 40, f"Reveal ring missing for target {target['id']}"
    print("PASS: all 10 reveal rings are present and inside target-safe margins")

    pcm = array.array("f")
    pcm.frombytes(subprocess.check_output([
        "ffmpeg", "-v", "error", "-i", str(VIDEO), "-t", "20", "-map", "0:a:0",
        "-ac", "1", "-ar", "48000", "-f", "f32le", "pipe:1"]))
    if sys.byteorder != "little":
        pcm.byteswap()
    peak = max(abs(s) for s in pcm)
    assert .03 < peak < .5
    print(f"PASS: audio present without clipping (decoded mono peak {20*math.log10(peak):.2f} dBFS)")
    subprocess.run(["ffmpeg", "-v", "error", "-i", str(VIDEO), "-f", "null", "-"], check=True)
    print("PASS: complete video/audio decode")


if __name__ == "__main__":
    check()
