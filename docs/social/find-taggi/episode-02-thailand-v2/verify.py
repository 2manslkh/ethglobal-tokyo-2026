#!/usr/bin/env python3
"""Verify the A/B control, all timer intervals, example and finished export."""

import array
import importlib.util
import io
import json
import re
import subprocess
from PIL import Image, ImageChops, ImageDraw, ImageStat
from render import HERE, SPEC, VIDEO, animated_sprite, prepare_scene, scene_frame


def decoded_frame(at):
    data = subprocess.check_output(["ffmpeg", "-v", "error", "-ss", str(at), "-i", str(VIDEO),
                                    "-frames:v", "1", "-f", "image2pipe", "-c:v", "png", "pipe:1"])
    return Image.open(io.BytesIO(data)).convert("RGB")


def mean_difference(first, second):
    return ImageStat.Stat(ImageChops.difference(first, second).convert("L")).mean[0]


def check():
    metadata = json.loads(subprocess.check_output(["ffprobe", "-v", "error", "-count_frames",
                          "-show_streams", "-show_format", "-of", "json", str(VIDEO)]))
    video = next(s for s in metadata["streams"] if s["codec_type"] == "video")
    audio = next(s for s in metadata["streams"] if s["codec_type"] == "audio")
    assert (video["width"], video["height"], video["codec_name"], video["pix_fmt"]) == (1080, 1920, "h264", "yuv420p")
    assert video["r_frame_rate"] == "30/1" and int(video["nb_read_frames"]) == 600
    assert video["color_space"] == video["color_transfer"] == video["color_primaries"] == "bt709"
    assert abs(float(metadata["format"]["duration"]) - 20) < .01
    assert audio["codec_name"] == "aac" and audio["channels"] == 2
    print("PASS: 20.000 s, 600 frames, 1080x1920, 30 fps, H.264/BT.709 and stereo AAC")

    base, sprites = prepare_scene()
    assert len(sprites) == 10
    print("Sprite sizes:", ", ".join(f"{i+1}: {s.width}x{s.height}" for i,s in enumerate(sprites)))
    for target, original in zip(SPEC["targets"], sprites):
        assert min(original.size) >= 24
        for index in range(600):
            sprite, (x, y) = animated_sprite(target, original, index/30)
            assert min(sprite.size) >= 24
            assert 60 <= x and x+sprite.width < 930 and y >= 250 and y+sprite.height < 1570
    print("PASS: all 10 remain at least 24 px on both sprite dimensions for all 600 frames, within safe margins")

    first_source = scene_frame(0, base, sprites)
    assert first_source.tobytes() == scene_frame(20, base, sprites).tobytes()
    print("PASS: source animation at 20 seconds is pixel-identical to time zero")

    x,y,w,h = SPEC["scene_rect"]
    box = (x,y,x+w,y+h)
    baseline_spec = importlib.util.spec_from_file_location("thailand_v1", HERE.parent / "episode-02-thailand/render.py")
    baseline = importlib.util.module_from_spec(baseline_spec)
    baseline_spec.loader.exec_module(baseline)
    for key in ("width", "height", "fps", "duration", "source_size", "source_crop", "scene_rect", "targets", "min_target_side"):
        assert SPEC[key] == baseline.SPEC[key], f"A/B control changed: {key}"
    assert (HERE / SPEC["background"]).resolve() == baseline.HERE / "clean-background.png"
    assert (HERE / "CAPTION.txt").read_bytes() == (baseline.HERE / "CAPTION.txt").read_bytes()
    baseline_base, baseline_sprites = baseline.prepare_scene()
    for at in (0, 7.25, 13.5, 599/30):
        assert scene_frame(at, base, sprites).crop(box).tobytes() == baseline.scene_frame(at, baseline_base, baseline_sprites).crop(box).tobytes()
    print("PASS: v1 scene, target positions, sizes, motion, duration and caption are unchanged")

    example = SPEC["example"]
    ex, ey = example["position"]
    ew = round(example["crop"][2]*example["height"]/example["crop"][3])
    example_box = (ex,ey,ex+ew,ey+example["height"])
    assert ey+example["height"] < y and ex+ew < 930
    assert ImageChops.difference(base.crop(example_box).convert("RGB"), baseline_base.crop(example_box).convert("RGB")).getbbox()
    decoded = {}
    times = [0, 1, 2, 3, 4, 14, 15, 16, 17, 18, 19, 599/30]
    for at in times:
        decoded[at] = decoded_frame(at)
    # The countdown and example never replace or cover the puzzle.
    deviations = []
    for at in [0, 14, 16, 18, 19, 599/30]:
        expected = scene_frame(at, base, sprites)
        deviations.append(mean_difference(expected.crop(box), decoded[at].crop(box)))
    assert max(deviations) < 4
    events = [line for line in (HERE / "edit.ass").read_text().splitlines() if line.startswith("Dialogue:")]
    fixed, countdown = [], []
    for event in events:
        fields = event.split(",", 9)
        text = re.sub(r"\{[^}]*\}", "", fields[9])
        (countdown if text.isdigit() else fixed).append((fields, text))
    assert len(fixed) == 7 and all(fields[1:3] == ["0:00:00.00", "0:00:20.00"] for fields,_ in fixed)
    assert {text for _,text in fixed} >= {"Can you find all 10 Taggis?", "EXAMPLE", "SECONDS LEFT"}
    assert len(countdown) == 20
    for second, (fields,text) in enumerate(countdown):
        assert fields[1:3] == [f"0:00:{second:02d}.00", f"0:00:{second+1:02d}.00"]
        assert text == f"{20-second:02d}"
    assert not any("\\p1" in line or "\\fad" in line or "\\t(" in line for line in events)
    assert mean_difference(base.crop(example_box).convert("RGB"), decoded[0].crop(example_box)) < 6
    assert mean_difference(decoded[0].crop(example_box), decoded[599/30].crop(example_box)) < 1.5
    print("PASS: labelled 92x120 Taggi example is visible outside the ten-target puzzle")
    print(f"PASS: uninterrupted search, requested copy and no answers (max encode difference {max(deviations):.4f}/255)")

    # Decode every timer frame, including both sides of every second boundary.
    tw,th = 140,108
    raw = subprocess.check_output(["ffmpeg", "-v", "error", "-i", str(VIDEO),
        "-vf", f"crop={tw}:{th}:553:45", "-an", "-f", "rawvideo", "-pix_fmt", "rgb24", "pipe:1"])
    stride = tw*th*3
    assert len(raw) == 600*stride
    timer_frames = [Image.frombytes("RGB", (tw,th), raw[i*stride:(i+1)*stride]) for i in range(600)]
    hold_differences = [mean_difference(timer_frames[second*30+15], timer_frames[index])
                        for second in range(20) for index in range(second*30,(second+1)*30)]
    changes = [mean_difference(timer_frames[second*30-1], timer_frames[second*30]) for second in range(1,20)]
    assert max(hold_differences) < 1.5
    assert min(changes) > 1.5
    print(f"PASS: 20 to 01 countdown, exactly 30 frames per number; all 19 transitions visible (max hold noise {max(hold_differences):.4f}; min transition {min(changes):.4f}/255)")

    for label, sample_times in [("early", [0,1,2,3,4]), ("late", [15,16,17,18,19])]:
        for target in SPEC["targets"]:
            cx,cy = target["center"]
            region = (cx-90,cy-90,cx+90,cy+90)
            reference = decoded[sample_times[0]].crop(region)
            changes = [sum(ImageChops.difference(reference, decoded[at].crop(region)).convert("L").histogram()[21:])
                       for at in sample_times[1:]]
            assert max(changes) >= 10, f"Target {target['id']} stopped during {label} interval"
        print(f"PASS: all 10 animate during the {label} interval")

    end_source = scene_frame(599/30, base, sprites)
    expected_seam = mean_difference(end_source.crop(box), first_source.crop(box))
    actual_seam = mean_difference(decoded[599/30].crop(box), decoded[0].crop(box))
    # CRF encoding changes static texture slightly between the initial I-frame
    # and later frames. Measure that separately, rather than treating it as motion.
    mask = Image.new("L", (1080,1920), 0)
    drawing = ImageDraw.Draw(mask)
    drawing.rectangle(box, fill=255)
    for target, sprite in zip(SPEC["targets"], sprites):
        cx,cy = target["center"]
        radius = max(sprite.size)+12
        drawing.rectangle((cx-radius,cy-radius,cx+radius,cy+radius), fill=0)
    difference = ImageChops.difference(decoded[599/30], decoded[0]).convert("L")
    codec_noise = ImageStat.Stat(difference, mask=mask).mean[0]
    assert codec_noise < 1.5
    assert actual_seam < expected_seam+codec_noise+.25
    print(f"PASS: encoded loop boundary within the normal motion step plus measured compression variation ({actual_seam:.4f}/255; source step {expected_seam:.4f}; static texture {codec_noise:.4f})")

    pcm = array.array("f")
    pcm.frombytes(subprocess.check_output(["ffmpeg", "-v", "error", "-i", str(VIDEO),
                  "-map", "0:a", "-ac", "1", "-f", "f32le", "pipe:1"]))
    assert max(abs(s) for s in pcm) < 1e-6
    print("PASS: silent audio matches v1; timer resets visually on replay")
    subprocess.run(["ffmpeg", "-v", "error", "-i", str(VIDEO), "-f", "null", "-"], check=True)
    print("PASS: full decode without errors")


if __name__ == "__main__":
    check()
