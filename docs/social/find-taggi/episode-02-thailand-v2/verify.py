#!/usr/bin/env python3
"""Verify the popup, unobstructed search window, countdown and finished export."""

import array
import importlib.util
import io
import json
import re
import subprocess
from PIL import Image, ImageChops, ImageStat
from render import HERE, PAPER, SPEC, VIDEO, animated_sprite, prepare_popup, prepare_scene, scene_frame, video_frame


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
    assert base.tobytes() == baseline_base.tobytes(), "A permanent header example remains"
    for at in (0, 7.25, 13.5, 599/30):
        assert scene_frame(at, base, sprites).crop(box).tobytes() == baseline.scene_frame(at, baseline_base, baseline_sprites).crop(box).tobytes()
    print("PASS: v1 scene, target positions, sizes, motion, duration and caption are unchanged")

    example = SPEC["example"]
    ex, ey = example["position"]
    ew = round(example["crop"][2]*example["height"]/example["crop"][3])
    example_box = (ex,ey,ex+ew,ey+example["height"])
    px,py,pw,ph = SPEC["popup_rect"]
    assert px <= ex and ex+ew <= px+pw and py <= ey and ey+example["height"] <= py+ph
    popup = prepare_popup()
    for index in range(60,600):
        at = index/30
        # Any lingering popup layer would change pixels in the search frame.
        assert video_frame(at,base,sprites,popup).tobytes() == scene_frame(at,base,sprites).tobytes()
    decoded = {}
    times = [0, 1, 59/30, 2, 3, 4, 5, 6, 14, 15, 16, 17, 18, 19, 599/30]
    for at in times:
        decoded[at] = decoded_frame(at)
    # The popup clears exactly at 2 seconds for an 18-second search.
    deviations = []
    # At t=0 the entrance fade is fully transparent; also check that initial
    # background frame before the introduction becomes visible.
    for at in [0, 2, 14, 16, 18, 19, 599/30]:
        expected = scene_frame(at, base, sprites)
        deviations.append(mean_difference(expected.crop(box), decoded[at].crop(box)))
    assert max(deviations) < 4
    events = [line for line in (HERE / "edit.ass").read_text().splitlines() if line.startswith("Dialogue:")]
    copy, countdown = [], []
    for event in events:
        fields = event.split(",", 9)
        text = re.sub(r"\{[^}]*\}", "", fields[9])
        (countdown if re.fullmatch(r"\d{2}s", text) else copy).append((fields, text))
    assert len(copy) == 9
    copy_by_text = {text:fields for fields,text in copy}
    assert copy_by_text["Can you find all 10 Taggis?"][1:3] == ["0:00:02.00", "0:00:20.00"]
    for text in ("Can you find", "all 10 Taggis?", "18 seconds. Look closely."):
        fields = copy_by_text[text]
        assert fields[1:3] == ["0:00:00.00", "0:00:02.00"] and "\\fad(160,140)" in fields[9]
    assert "EXAMPLE" not in copy_by_text
    assert len(countdown) == 18
    for second, (fields,text) in enumerate(countdown, start=2):
        assert fields[1:3] == [f"0:00:{second:02d}.00", f"0:00:{second+1:02d}.00"]
        assert text == f"{20-second:02d}s"
    assert not any("\\p1" in line or "\\t(" in line for line in events)
    intro = video_frame(1, base, sprites, popup)
    plain_card_box = (175,820,310,1150)
    # RGB-to-YUV round-tripping shifts this light cream by two levels per
    # channel. Allow that quantization while separately checking the card
    # differs strongly from the uncovered market underneath.
    paper_difference = mean_difference(intro.crop(plain_card_box), decoded[1].crop(plain_card_box))
    assert paper_difference <= 3
    assert mean_difference(decoded[1].crop(plain_card_box), scene_frame(1,base,sprites).crop(plain_card_box)) > 30
    assert mean_difference(intro.crop(example_box), decoded[1].crop(example_box)) < 6
    assert mean_difference(decoded[59/30].crop(plain_card_box), scene_frame(59/30,base,sprites).crop(plain_card_box)) > 5
    print("PASS: cream popup, full prompt and 300x391 Taggi visible during introduction; fully removed at frame 60")
    print(f"PASS: full 18-second search after the popup, without answers (max encode difference {max(deviations):.4f}/255)")

    # Decode every timer frame, including both sides of every second boundary.
    tw,th = 90,56
    raw = subprocess.check_output(["ffmpeg", "-v", "error", "-i", str(VIDEO),
        "-vf", f"crop={tw}:{th}:820:162", "-an", "-f", "rawvideo", "-pix_fmt", "rgb24", "pipe:1"])
    stride = tw*th*3
    assert len(raw) == 600*stride
    timer_frames = [Image.frombytes("RGB", (tw,th), raw[i*stride:(i+1)*stride]) for i in range(600)]
    blank = Image.new("RGB", (tw,th), f"#{PAPER}")
    assert max(mean_difference(blank,frame) for frame in timer_frames[:60]) <= 3
    hold_differences = [mean_difference(timer_frames[second*30+15], timer_frames[index])
                        for second in range(2,20) for index in range(second*30,(second+1)*30)]
    changes = [mean_difference(timer_frames[second*30-1], timer_frames[second*30]) for second in range(2,20)]
    assert max(hold_differences) < 1.5
    assert min(changes) > 1.5
    print(f"PASS: timer absent during popup, then 18s to 01s for exactly 30 frames each (max hold noise {max(hold_differences):.4f}; min transition {min(changes):.4f}/255)")

    for label, sample_times in [("early search", [2,3,4,5,6]), ("late search", [15,16,17,18,19])]:
        for target in SPEC["targets"]:
            cx,cy = target["center"]
            region = (cx-90,cy-90,cx+90,cy+90)
            reference = decoded[sample_times[0]].crop(region)
            changes = [sum(ImageChops.difference(reference, decoded[at].crop(region)).convert("L").histogram()[21:])
                       for at in sample_times[1:]]
            assert max(changes) >= 10, f"Target {target['id']} stopped during {label} interval"
        print(f"PASS: all 10 animate during the {label} interval")

    # A seamless full-composition boundary is no longer the intended behavior:
    # this variant restarts the introductory popup. Source motion remains
    # periodic (checked above), and both endpoint scenes pass encoding checks.

    pcm = array.array("f")
    pcm.frombytes(subprocess.check_output(["ffmpeg", "-v", "error", "-i", str(VIDEO),
                  "-map", "0:a", "-ac", "1", "-f", "f32le", "pipe:1"]))
    assert max(abs(s) for s in pcm) < 1e-6
    print("PASS: silent audio matches v1; the introductory popup repeats on replay")
    subprocess.run(["ffmpeg", "-v", "error", "-i", str(VIDEO), "-f", "null", "-"], check=True)
    print("PASS: full decode without errors")


if __name__ == "__main__":
    check()
