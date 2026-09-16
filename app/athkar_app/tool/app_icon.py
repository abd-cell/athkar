"""Draws the launcher icon — the mark from `lib/widgets/athkar_logo.dart`.

The mark is *painted* in the app, not shipped as an asset, so the launcher
icons are the one place it has to exist as pixels. Generating them from this
script rather than exporting them by hand keeps the two definitions in step:
the geometry below is the same geometry as `_MarkPainter`, in the same
proportions, and the colours are the same tokens.

    python tool/app_icon.py

Writes, relative to `app/athkar_app/`:
  android/app/src/main/res/mipmap-*/ic_launcher.png      legacy launcher
  android/app/src/main/res/mipmap-*/ic_launcher_fore.png adaptive foreground
  ios/Runner/Assets.xcassets/AppIcon.appiconset/*.png    every listed size
  web/icons/*.png, web/favicon.png
"""

from __future__ import annotations

import json
import math
import pathlib

from PIL import Image, ImageDraw, ImageFont

ROOT = pathlib.Path(__file__).resolve().parent.parent

BRAND = (47, 88, 56, 255)      # AthkarColors.brand  #2F5838
ON_BRAND = (251, 246, 238, 255)  # tokens.onBrand    #FBF6EE

# Traditional Arabic: the naskh face Windows ships. Amiri is fetched at runtime
# by google_fonts and is not on disk, and an icon glyph does not need to be the
# identical cut — it needs the same naskh colour.
GLYPH_FONT = pathlib.Path("C:/Windows/Fonts/trado.ttf")

# The geometry of `_MarkPainter`, as fractions of the mark's box.
HEAD_DEGREES = -68.0
SWEEP_DEGREES = 316.0
STROKE = 0.085
BEAD = STROKE * 0.98

SUPERSAMPLE = 8


def draw_mark(size: int, colour: tuple[int, int, int, int]) -> Image.Image:
    """The bare mark — ring, bead and «ذ» — on transparency."""
    s = size * SUPERSAMPLE
    img = Image.new("RGBA", (s, s), (0, 0, 0, 0))
    draw = ImageDraw.Draw(img)

    stroke = s * STROKE
    bead = s * BEAD
    radius = s / 2 - bead
    centre = s / 2

    # Pillow strokes *inward* from the bounding box, so the box is grown by
    # half a stroke to put the centreline on `radius` — where the caps and the
    # bead are placed. Without this the bead sits proud of the ring.
    outer = radius + stroke / 2
    draw.arc((centre - outer, centre - outer, centre + outer, centre + outer),
             HEAD_DEGREES, HEAD_DEGREES + SWEEP_DEGREES,
             fill=colour, width=round(stroke))

    # Pillow's arc has butt ends; the painter's are round. Cap both by hand.
    for degrees in (HEAD_DEGREES, HEAD_DEGREES + SWEEP_DEGREES):
        _dot(draw, _on_circle(centre, radius, degrees), stroke / 2, colour)

    _dot(draw, _on_circle(centre, radius, HEAD_DEGREES), bead, colour)

    img.alpha_composite(_glyph(s, colour))
    return img.resize((size, size), Image.LANCZOS)


def _glyph(s: int, colour) -> Image.Image:
    """«ذ», measured by its own ink and centred in the ring.

    Anchoring on the em box would hang the letter off the baseline and leave it
    sitting low inside the circle — the face's ascent is mostly empty above a
    ذ, and the icon is small enough for that to read as a mistake.
    """
    scratch = Image.new("RGBA", (s, s), (0, 0, 0, 0))
    font = ImageFont.truetype(str(GLYPH_FONT), round(s * 0.7))
    ImageDraw.Draw(scratch).text((s / 2, s / 2), "ذ", font=font,
                                 fill=colour, anchor="mm")

    ink = scratch.crop(scratch.getbbox())
    height = s * 0.40
    width = round(ink.width * height / ink.height)
    ink = ink.resize((max(width, 1), round(height)), Image.LANCZOS)

    placed = Image.new("RGBA", (s, s), (0, 0, 0, 0))
    placed.alpha_composite(ink, ((s - ink.width) // 2, (s - ink.height) // 2))
    return placed


def _on_circle(centre: float, radius: float, degrees: float) -> tuple[float, float]:
    angle = math.radians(degrees)
    return centre + math.cos(angle) * radius, centre + math.sin(angle) * radius


def _dot(draw: ImageDraw.ImageDraw, at: tuple[float, float], r: float, colour) -> None:
    draw.ellipse((at[0] - r, at[1] - r, at[0] + r, at[1] + r), fill=colour)


def tile(size: int, *, radius: float | None, background=BRAND, inset=0.74) -> Image.Image:
    """The mark on a brand tile. `radius` of None gives a square — the shape
    iOS and the adaptive-icon mask apply themselves."""
    s = size * SUPERSAMPLE
    img = Image.new("RGBA", (s, s), (0, 0, 0, 0))
    draw = ImageDraw.Draw(img)

    if radius is None:
        draw.rectangle((0, 0, s, s), fill=background)
    else:
        draw.rounded_rectangle((0, 0, s - 1, s - 1), radius=s * radius, fill=background)

    img = img.resize((size, size), Image.LANCZOS)

    mark_size = round(size * inset)
    mark = draw_mark(mark_size, ON_BRAND)
    offset = (size - mark_size) // 2
    img.alpha_composite(mark, (offset, offset))
    return img


def write(img: Image.Image, path: pathlib.Path) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    img.save(path)
    print(f"  {path.relative_to(ROOT)}  {img.width}×{img.height}")


def main() -> None:
    print("android")
    # The legacy icon carries its own rounded square; the adaptive foreground
    # sits on the inner 66/108 of the canvas, where no launcher mask crops it.
    for folder, px in {
        "mdpi": 48, "hdpi": 72, "xhdpi": 96, "xxhdpi": 144, "xxxhdpi": 192,
    }.items():
        out = ROOT / "android/app/src/main/res" / f"mipmap-{folder}"
        write(tile(px, radius=0.22), out / "ic_launcher.png")

        foreground = Image.new("RGBA", (px * 2, px * 2), (0, 0, 0, 0))
        mark = draw_mark(round(px * 2 * 0.52), ON_BRAND)
        pad = (foreground.width - mark.width) // 2
        foreground.alpha_composite(mark, (pad, pad))
        write(foreground, out / "ic_launcher_fore.png")

    print("ios")
    appicon = ROOT / "ios/Runner/Assets.xcassets/AppIcon.appiconset"
    entries = json.loads((appicon / "Contents.json").read_text())["images"]
    for entry in entries:
        side = float(entry["size"].split("x")[0]) * float(entry["scale"].rstrip("x"))
        # iOS rounds the corners itself and forbids transparency.
        write(tile(round(side), radius=None), appicon / entry["filename"])

    print("web")
    for name, px in {"Icon-192": 192, "Icon-512": 512}.items():
        write(tile(px, radius=0.22), ROOT / "web/icons" / f"{name}.png")
    # A maskable icon is cropped to a circle by the launcher, so the mark sits
    # smaller inside a full-bleed tile.
    for name, px in {"Icon-maskable-192": 192, "Icon-maskable-512": 512}.items():
        write(tile(px, radius=None, inset=0.52), ROOT / "web/icons" / f"{name}.png")
    write(tile(64, radius=0.22), ROOT / "web/favicon.png")


if __name__ == "__main__":
    main()
