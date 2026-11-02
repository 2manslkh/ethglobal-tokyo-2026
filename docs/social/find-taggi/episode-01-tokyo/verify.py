#!/usr/bin/env python3
"""Validate the actual encoded Reel, not just its render settings."""

import array
import json
import math
from pathlib import Path
import subprocess
import sys

from render import SPEC, target_center

HERE = Path(__file__).resolve().parent
VIDEO = HERE / "find-taggi-tokyo.mp4"


def ffmpeg(*args):
    return subprocess.check_output(["ffmpeg", "-v", "error", *map(str, args)])


def frame(at):
    return ffmpeg("-ss", at, "-i", VIDEO, "-frames:v", 1,
                  "-f", "rawvideo", "-pix_fmt", "rgb24", "pipe:1")


def check():
    metadata = json.loads(subprocess.check_output([
        "ffprobe", "-v", "error", "-count_frames", "-show_streams",
        "-show_format", "-of", "json", str(VIDEO)]))
    video = next(s for s in metadata["streams"] if s["codec_type"] == "video")
    audio = next(s for s in metadata["streams"] if s["codec_type"] == "audio")
    assert (video["width"], video["height"]) == (1080, 1920)
    assert video["codec_name"] == "h264" and video["pix_fmt"] == "yuv420p"
    assert video["color_space"] == video["color_transfer"] == video["color_primaries"] == "bt709"
    assert video["r_frame_rate"] == "30/1" and int(video["nb_read_frames"]) == 600
    assert abs(float(metadata["format"]["duration"]) - 20) < .01
    assert audio["codec_name"] == "aac" and audio["channels"] == 2
    assert int(audio["sample_rate"]) == 48000
    print("PASS: 1080x1920, H.264/yuv420p, 30 fps, 600 frames, 20.000 s, AAC stereo/48 kHz")

    # Compare the scene at both boundaries and within the 12-second search window.
    x, y, w, h = SPEC["scene_rect"]
    small_frames = [ffmpeg("-ss", at, "-i", VIDEO, "-frames:v", 1,
                          "-vf", f"crop={w}:{h}:{x}:{y},scale=240:-2",
                          "-f", "rawvideo", "-pix_fmt", "rgb24", "pipe:1")
                    for at in (2, 5, 8, 11, 13.966667)]
    errors = [sum(abs(a-b) for a, b in zip(small_frames[0], f)) / len(f)
              for f in small_frames[1:]]
    assert max(errors) < 1.0, f"Search image changed: {errors}"
    print(f"PASS: stationary search from 2.000 to 13.967 s (maximum mean pixel difference {max(errors):.4f}/255)")

    baseline = frame(13.5)
    for at, expected in ((14.6, 1), (15.6, 2), (16.6, 3), (17.966667, 3)):
        current = frame(at)
        observed = 0
        for target in SPEC["targets"]:
            cx, cy = target_center(target)
            assert 250 < cy-72 and cy+72 < 1570 and cx+72 < 930
            changes = []
            for i in range(100):
                angle = i * math.tau / 100
                px, py = round(cx+67*math.cos(angle)), round(cy+67*math.sin(angle))
                index = (py*1080+px)*3
                changes.append(sum(abs(current[index+c]-baseline[index+c]) for c in range(3))/3)
            observed += sum(d > 25 for d in changes) > 70
        assert observed == expected, f"At {at}s expected {expected} circles, found {observed}"
        print(f"PASS: {observed} accurately positioned reveal circles at {at:.3f} s")

    pcm = array.array("f")
    pcm.frombytes(ffmpeg("-i", VIDEO, "-t", 20, "-map", "0:a:0", "-ac", 1,
                         "-ar", 48000, "-f", "f32le", "pipe:1"))
    if sys.byteorder != "little":
        pcm.byteswap()
    peak = max(abs(sample) for sample in pcm)
    assert .03 < peak < .5, f"Unexpected audio peak: {peak}"
    for at in (.2, 11, 12, 13, 14.03, 15.03, 16.03, 18.18):
        section = pcm[round(at*48000):round((at+.1)*48000)]
        assert max(abs(s) for s in section) > .01, f"Missing cue at {at}s"
    assert max(abs(s) for s in pcm[48000:10*48000]) < .001
    print(f"PASS: all 8 sound cues present; no clipping (decoded mono peak {20*math.log10(peak):.2f} dBFS); quiet search bed")
    ffmpeg("-i", VIDEO, "-f", "null", "-")
    print("PASS: complete video/audio decode without errors")


if __name__ == "__main__":
    check()
