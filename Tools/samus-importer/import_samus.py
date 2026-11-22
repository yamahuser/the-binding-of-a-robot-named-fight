"""Map the supplied Super Metroid sheet to OpenARNF's animation frames."""

import argparse
from functools import lru_cache
from pathlib import Path
import re
import subprocess
from tempfile import TemporaryDirectory
import uuid

from PIL import Image, ImageChops, ImageDraw, ImageFont


CELL = 48
STAND_HEIGHT = 41
BACKGROUND = (192, 128, 246)
PLAYER = Path("Assets/Resources/Sprites/PlayerBase.png")
MORPH = Path("Assets/Sprites/Player/SpiderForm.png")
PLAYER_OUT = Path("Assets/Resources/Samus/PlayerAtlas.png")
MORPH_OUT = PLAYER_OUT.with_name("SpiderAtlas.png")
PATCH = Path(__file__).with_name("samus-renderer.patch")
UPGRADE_PATCH = Path(__file__).with_name("samus-options-upgrade.patch")


def row(x, y, frames):
    return [(x + frame * 96, y) for frame in frames]


def poses():
    result = {}
    for side in (0, 1):
        direction = ("Right", "Left")[side]

        def motion(name, x, y, frames):
            result[name + direction] = row(x, y + side * 128, frames)

        motion("Default", 16, 176, [0, 2])
        motion("AimUp", 16, 432, [1])
        motion("AngleUp", 16, 688, [0])
        motion("AngleDown", 16, 944, [0])
        motion("Run", 16, 1200, range(10))
        result["Run" + direction + "Shoot"] = row(16, 1456 + side * 128, range(10))
        result["Run" + direction + "ShootUp"] = row(16, 1968 + side * 128, range(10))
        result["Run" + direction + "ShootDown"] = row(16, 2224 + side * 128, range(10))
        motion("Crouching", 1056, 1776, [0])
        motion("Crouched", 1056, 368, [0, 2])
        result["CrouchingAngleUp" + direction] = row(2096, 2608 + side * 128, [0])
        result["CrouchingAngleDown" + direction] = row(3408, 1584 + side * 128, [0])
        result["CrouchingAimUp" + direction] = row(2096, 112 + side * 192, [0])
        result["CrouchedAngleUp" + direction] = row(2096, 3888 + side * 128, [0])
        result["CrouchedAimUp" + direction] = row(2096, 3888 + side * 128, [1])
        result["CrouchedAngleDown" + direction] = row(2096, 2864 + side * 128, [0])
        result["Jump" + direction] = row(1056, 3056 + side * 128, [0]) + row(1056, 3312 + side * 128, [0, 1])
        motion("JumpShoot", 16, 2480, [0])
        motion("JumpAimUp", 16, 2736, [1])
        motion("JumpAngleUp", 2096, 1584, [0])
        motion("JumpAngleDown", 2096, 1840, [0])
        motion("Fall", 1056, 624, [0, 1, 2, 2])
        motion("Land", 2752, 176, [0, 1])
        motion("SpinJump", 16, 3248, range(1, 9))
        result["Dash" + direction] = row(2752, 3536 + side * 160, [0])
        result["Slide" + direction] = row(1056, (3696, 3568)[side], [1])
        result["OnWall" + direction] = row(2752, (2352, 2480)[side], [0])
        for index, frame in enumerate([([0, 1], 3248), ([2], 3248), ([0], 3504), ([1], 3504), ([3], 3504)]):
            result[f"Fatigue{direction}{index}"] = row(3408, frame[1] + side * 128, frame[0])
    result["SpinDashCharge"] = row(16, 3984, [0, 2, 4, 6])
    result["SpinDash"] = row(4048, 1712, [1, 3, 5, 7])
    result["DiveKick"] = row(16, 2992, [0])
    result["JumpAimDown"] = row(16, 3120, [0]) + row(16, 2992, [0])
    return result


def morph_poses():
    ball = row(16, 3984, range(8))
    enter = row(1056, 368, [0]) + row(1056, 2032, [0, 1]) + ball[:3]
    return {
        "SpiderDefault": ball[:2], "SpiderWalk": ball,
        "SpiderMorphIn": enter, "SpiderMorphOut": enter[::-1],
        "SpiderLand": ball[1:3], "SpiderFall": ball[:3][::-1], "SpiderJump": ball[1:4],
    }


class Sheet:
    def __init__(self, path):
        with Image.open(path) as png:
            self.image = png.convert("RGB")
        if self.image.size != (6496, 4384):
            raise ValueError("Expected the 6496×4384 Super Metroid Samus Aran sheet, asset 182270.")

    @lru_cache(maxsize=None)
    def frame(self, x, y):
        if any(self.image.getpixel(point) != (0, 0, 0) for point in ((x, y), (x + 83, y), (x, y + 83), (x + 83, y + 83))):
            raise ValueError(f"Missing sprite frame border at {x},{y}.")
        frame = self.image.crop((x + 2, y + 2, x + 82, y + 82))
        difference = ImageChops.difference(frame, Image.new("RGB", frame.size, BACKGROUND))
        channels = difference.split()
        mask = ImageChops.lighter(ImageChops.lighter(channels[0], channels[1]), channels[2]).point(lambda p: 255 if p else 0)
        frame.putalpha(mask)
        bounds = mask.getbbox()
        if bounds is None or not (0 < bounds[0] < bounds[2] < 80 and 0 < bounds[1] < bounds[3] < 80):
            raise ValueError(f"Could not isolate the sprite at {x},{y}.")
        return frame.crop(bounds), bounds


def host_clips(project, texture, animations, prefix):
    meta = (project / texture.with_suffix(".png.meta")).read_text(encoding="utf8")
    guid = re.search(r"^guid: (\w+)", meta, re.MULTILINE)[1]
    frame_ids = {int(key): int(index) for key, index in re.findall(r"213: (\d+)\s+second: " + prefix + r"(\d+)", meta)}
    keys = re.compile(r"^\s+value: \{fileID: (\d+), guid: " + guid + r", type: 3\}", re.MULTILINE)
    clips = {}
    for clip in (project / animations).glob("*.anim"):
        indices = list(dict.fromkeys(frame_ids[int(key)] for key in keys.findall(clip.read_text(encoding="utf8"))))
        if indices:
            clips[clip.stem] = indices
    return clips


def fit(frame, bounds, visible_bottom, scale, standing):
    ratio = STAND_HEIGHT / frame.height if standing else scale
    ratio = min(ratio, CELL / frame.width, CELL / frame.height)
    width, height = max(1, round(frame.width * ratio)), max(1, round(frame.height * ratio))
    left = round(CELL / 2 + (bounds[0] - 40) * ratio)
    left = max(0, min(left, CELL - width))
    top = max(0, min(visible_bottom - height, CELL - height))
    out = Image.new("RGBA", (CELL, CELL))
    out.alpha_composite(frame.resize((width, height), Image.Resampling.NEAREST), (left, top))
    return out


def cell_xy(index, columns):
    return (index % columns) * CELL, (index // columns) * CELL


def build(project, image_path, donor, clips, pose_map):
    with Image.open(project / image_path) as png:
        original = png.convert("RGBA")
    atlas = Image.new("RGBA", original.size)
    scale = STAND_HEIGHT / donor.frame(16, 176)[0].height
    populated = set()
    for clip, indices in sorted(clips.items(), key=lambda entry: entry[0].startswith("AimUp")):
        frames = pose_map[clip]
        for step, index in enumerate(indices):
            if index in populated:
                continue
            frame, bounds = donor.frame(*frames[round(step * (len(frames) - 1) / max(1, len(indices) - 1))])
            x, y = cell_xy(index, original.width // CELL)
            bottom = original.crop((x, y, x + CELL, y + CELL)).getchannel("A").getbbox()[3]
            fitted = fit(frame, bounds, bottom, scale, clip.startswith("Default"))
            # Player.ChangeFacing mirrors the renderer. Store authored left poses in local space.
            is_left = "Left" in clip or (clip == "JumpAimDown" and step == 0)
            if is_left:
                fitted = fitted.transpose(Image.Transpose.FLIP_LEFT_RIGHT)
            atlas.alpha_composite(fitted, (x, y))
            populated.add(index)
    return atlas, populated


def preview(atlas, morph):
    zoom = 2
    tile = CELL * zoom
    rows = [("Standing and aim", [0, 2, 5, 7, 9, 11, 12, 13]),
            ("Run", list(range(20, 30))), ("Run and aim", list(range(60, 70))),
            ("Jump and crouch", [100, 101, 102, 109, 111, 14, 15, 115, 135]),
            ("Spin jump", list(range(140, 148))), ("Morph Ball", list(range(10)))]
    canvas = Image.new("RGB", (24 + 10 * tile, 15 + len(rows) * (tile + 28)), (29, 33, 43))
    draw = ImageDraw.Draw(canvas)
    font = ImageFont.load_default(size=16)
    for i, (label, indices) in enumerate(rows):
        top = 15 + i * (tile + 28)
        draw.text((12, top), label, font=font, fill=(239, 241, 245))
        sheet = morph if label == "Morph Ball" else atlas
        for j, index in enumerate(indices):
            x, y = cell_xy(index, sheet.width // CELL)
            frame = sheet.crop((x, y, x + CELL, y + CELL)).resize((tile, tile), Image.Resampling.NEAREST)
            if label == "Standing and aim" and index in (2, 7, 11, 13):
                frame = frame.transpose(Image.Transpose.FLIP_LEFT_RIGHT)
            left = 12 + j * tile
            draw.rectangle((left, top + 22, left + tile - 2, top + tile + 20), fill=(40, 46, 58))
            canvas.paste(frame, (left, top + 22), frame)
    return canvas


def install(project, atlases):
    command = ["git", "-C", str(project), "apply"]
    already_applied = subprocess.run(command + ["--reverse", "--check", str(PATCH)], capture_output=True).returncode == 0
    patch = PATCH
    if not already_applied:
        if subprocess.run(command + ["--check", str(PATCH)], capture_output=True).returncode != 0:
            patch = UPGRADE_PATCH
        subprocess.run(command + ["--check", str(patch)], check=True)
    template = (project / "Assets/Resources/PlayerParts/BaseTorso.png.meta").read_text(encoding="utf-8")
    targets = [(project / path, atlas) for path, atlas in atlases]
    destination = targets[0][0].parent
    destination.mkdir(parents=True, exist_ok=True)
    with TemporaryDirectory(dir=destination) as staging:
        for target, atlas in targets:
            atlas.save(Path(staging) / target.name)
        if not already_applied:
            subprocess.run(command + [str(patch)], check=True)
        for target, _ in targets:
            meta = target.with_suffix(".png.meta")
            if not meta.exists():
                meta.write_text(re.sub(r"^guid: \w+", "guid: " + uuid.uuid4().hex, template, count=1, flags=re.MULTILINE), encoding="utf-8")
            (Path(staging) / target.name).replace(target)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--openarnf", required=True, type=Path)
    parser.add_argument("--samus-sheet", required=True, type=Path)
    parser.add_argument("--preview", type=Path)
    parser.add_argument("--apply", action="store_true")
    args = parser.parse_args()
    project = args.openarnf.resolve(strict=True)
    donor = Sheet(args.samus_sheet)
    clips = host_clips(project, PLAYER, "Assets/Sprites/Player", "PlayerBase_")
    morph_clips = host_clips(project, MORPH, "Assets/Sprites/Player/SpiderForm", "SpiderForm_")
    atlas, mapped = build(project, PLAYER, donor, clips, poses())
    ball, mapped_ball = build(project, MORPH, donor, morph_clips, morph_poses())
    if len(mapped) != 187 or len(mapped_ball) != 22:
        raise ValueError(f"Found {len(mapped)} regular and {len(mapped_ball)} Morph Ball frames in this project.")
    for index in range(4):
        x, y = cell_xy(index, atlas.width // CELL)
        bounds = atlas.crop((x, y, x + CELL, y + CELL)).getchannel("A").getbbox()
        if bounds[1::2] != (CELL - STAND_HEIGHT, CELL):
            raise ValueError(f"Incorrect standing size for frame {index}.")
    if args.preview:
        preview(atlas, ball).save(args.preview)
    if args.apply:
        install(project, [(PLAYER_OUT, atlas), (MORPH_OUT, ball)])
    status = "Installed" if args.apply else "Prepared"
    print(f"{status} {len(mapped)} Samus frames and {len(mapped_ball)} Morph Ball frames. Standing height: {STAND_HEIGHT} pixels.")


if __name__ == "__main__":
    main()
