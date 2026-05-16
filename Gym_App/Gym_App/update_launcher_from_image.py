#!/usr/bin/env python3
"""
Apply your own logo to Android launcher icons.

1. Save your image as PNG or JPG under Resources/drawable/ (e.g. Jeanetix_logo.png).
2. Run from this directory (Gym_App/Gym_App):

   python3 update_launcher_from_image.py

   python3 update_launcher_from_image.py Resources/drawable/other.png

Requires: pip install pillow

Also writes Resources/drawable/welcome_hero.png (splash on MainActivity) from the same file.
  Resources/drawable/jeanetix_launcher_source.png
  Resources/drawable/Jeanetix_logo.png
  Resources/drawable/jeanetix_logo.png
"""

from __future__ import annotations

import argparse
import sys
from pathlib import Path

try:
    from PIL import Image
except ImportError:
    print("Install Pillow: pip install pillow", file=sys.stderr)
    sys.exit(1)

SCRIPT_DIR = Path(__file__).resolve().parent
DRAWABLE = SCRIPT_DIR / "Resources" / "drawable"
RES_ROOT = SCRIPT_DIR / "Resources"


def _default_logo_path() -> Path:
    """First existing candidate in drawable (Option A: jeanetix_launcher_source.png)."""
    candidates = [
        DRAWABLE / "jeanetix_launcher_source.png",
        DRAWABLE / "Jeanetix_logo.png",
        DRAWABLE / "jeanetix_logo.png",
        DRAWABLE / "jeanetix_launcher_foreground.png",
        DRAWABLE / "jeanetix_launcher_source.jpg",
        DRAWABLE / "Jeanetix_logo.jpg",
        DRAWABLE / "jeanetix_logo.jpg",
    ]
    for p in candidates:
        if p.is_file():
            return p
    return candidates[0]


# Android adaptive icon layer sizes per density (108dp base)
DENSITIES = {
    "mdpi": 108,
    "hdpi": 162,
    "xhdpi": 216,
    "xxhdpi": 324,
    "xxxhdpi": 432,
}


def _sample_background_rgb(im: Image.Image) -> tuple[int, int, int]:
    """Median color of edge pixels (expects RGB)."""
    w, h = im.size
    strip = max(2, min(w, h) // 35)
    px: list[tuple[int, int, int]] = []
    for x in range(w):
        for y in range(strip):
            px.append(im.getpixel((x, y))[:3])
        for y in range(h - strip, h):
            px.append(im.getpixel((x, y))[:3])
    for y in range(h):
        for x in range(strip):
            px.append(im.getpixel((x, y))[:3])
        for x in range(w - strip, w):
            px.append(im.getpixel((x, y))[:3])

    rs = sorted(p[0] for p in px)
    gs = sorted(p[1] for p in px)
    bs = sorted(p[2] for p in px)
    mid = len(px) // 2
    return (rs[mid], gs[mid], bs[mid])


def _dist(a: tuple[int, int, int], b: tuple[int, int, int]) -> float:
    return ((a[0] - b[0]) ** 2 + (a[1] - b[1]) ** 2 + (a[2] - b[2]) ** 2) ** 0.5


def _apply_chroma_key(
    rgba: Image.Image,
    bg_rgb: tuple[int, int, int],
    threshold: float = 48.0,
    soften: float = 36.0,
) -> Image.Image:
    """Make pixels close to bg_rgb transparent (edge-aware soften band)."""
    im = rgba.convert("RGBA")
    px = im.load()
    w, h = im.size
    for y in range(h):
        for x in range(w):
            r, g, b, a = px[x, y]
            if a == 0:
                continue
            d = _dist((r, g, b), bg_rgb)
            if d <= threshold:
                px[x, y] = (r, g, b, 0)
            elif d <= threshold + soften:
                # Partial alpha for anti-alias fringe
                t = (d - threshold) / soften
                na = int(max(0, min(255, round(a * t))))
                px[x, y] = (r, g, b, na)
    return im


def _center_square_crop(im: Image.Image) -> Image.Image:
    w, h = im.size
    side = min(w, h)
    left = (w - side) // 2
    top = (h - side) // 2
    return im.crop((left, top, left + side, top + side))


def _hex_color(rgb: tuple[int, int, int]) -> str:
    return f"#{rgb[0]:02X}{rgb[1]:02X}{rgb[2]:02X}"


def _resize_max_width(im: Image.Image, max_w: int) -> Image.Image:
    w, h = im.size
    if w <= max_w:
        return im
    new_h = max(1, int(round(h * max_w / w)))
    return im.resize((max_w, new_h), Image.Resampling.LANCZOS)


def _save_welcome_hero(hero_rgba: Image.Image, max_width: int = 1400) -> None:
    out = _resize_max_width(hero_rgba, max_width)
    path = DRAWABLE / "welcome_hero.png"
    out.save(path, "PNG", optimize=True)
    print(f"Wrote welcome splash: {path} ({out.size[0]}x{out.size[1]})")


def _write_ic_launcher_background_xml(hex_color: str) -> None:
    path = RES_ROOT / "values" / "ic_launcher_background.xml"
    path.write_text(
        """<?xml version="1.0" encoding="utf-8"?>
<resources>
  <color name="ic_launcher_background">"""
        + hex_color
        + """</color>
</resources>
""",
        encoding="utf-8",
    )


def main() -> None:
    parser = argparse.ArgumentParser(description="Build launcher mipmap PNGs from one source image.")
    parser.add_argument(
        "image",
        nargs="?",
        default=None,
        help="Logo file (default: jeanetix_launcher_source.png in Resources/drawable/)",
    )
    parser.add_argument(
        "--welcome-only",
        action="store_true",
        help="Only write Resources/drawable/welcome_hero.png (skip launcher mipmaps and ic_launcher_background).",
    )
    parser.add_argument(
        "--no-chroma",
        action="store_true",
        help="Keep original background in splash/launcher (no transparency from edge color).",
    )
    args = parser.parse_args()
    src = Path(args.image).expanduser().resolve() if args.image else _default_logo_path()
    if not src.is_file():
        print(f"File not found: {src}", file=sys.stderr)
        print(
            "Option A: save your logo as:\n"
            f"  {DRAWABLE / 'jeanetix_launcher_source.png'}\n"
            "Or pass the path: python3 update_launcher_from_image.py Resources/drawable/Jeanetix_logo.png",
            file=sys.stderr,
        )
        sys.exit(1)

    full_rgb = Image.open(src).convert("RGB")
    bg_rgb = _sample_background_rgb(full_rgb)
    square_rgb = _center_square_crop(full_rgb)

    if args.no_chroma:
        fore_src = _center_square_crop(Image.open(src).convert("RGBA"))
    else:
        fore_src = _apply_chroma_key(square_rgb.convert("RGBA"), bg_rgb)

    # Splash / welcome hero: full artwork (not square), transparent where background was
    full_rgba = full_rgb.convert("RGBA")
    if args.no_chroma:
        hero_rgba = full_rgba
    else:
        hero_rgba = _apply_chroma_key(full_rgba, bg_rgb)
    _save_welcome_hero(hero_rgba)

    if args.welcome_only:
        print("Done (--welcome-only: launcher mipmaps unchanged). Rebuild and reinstall the APK.")
        return

    back_color = (*bg_rgb, 255)
    hex_bg = _hex_color(bg_rgb)
    _write_ic_launcher_background_xml(hex_bg)
    print(f"Background sample: {hex_bg} (written to values/ic_launcher_background.xml)")

    for density, size in DENSITIES.items():
        mipmap_dir = RES_ROOT / f"mipmap-{density}"
        mipmap_dir.mkdir(parents=True, exist_ok=True)

        back = Image.new("RGBA", (size, size), back_color)
        fore = fore_src.resize((size, size), Image.Resampling.LANCZOS)

        back_path = mipmap_dir / "ic_launcher_adaptive_back.png"
        fore_path = mipmap_dir / "ic_launcher_adaptive_fore.png"
        legacy_path = mipmap_dir / "ic_launcher.png"

        back.save(back_path, "PNG", optimize=True)
        fore.save(fore_path, "PNG", optimize=True)
        combined = Image.alpha_composite(back, fore)
        combined.save(legacy_path, "PNG", optimize=True)
        print(f"Wrote {density} ({size}x{size})")

    print("Done. Rebuild the app (dotnet build / Visual Studio) and reinstall the APK.")


if __name__ == "__main__":
    main()
