#!/usr/bin/env python3
"""Build the FarmSense Nexus media set with Pillow and DejaVu fonts.

Run: python3 tools/build_nexus_media.py
Adapted from Compare/tools/build_nexus_media.py, retaining its image helpers,
EVZ palette, condensed typography, orange frames and soft card shadows.
Source screenshots are cropped without retouching their in-game text.
"""
import os
from pathlib import Path
from PIL import Image, ImageDraw, ImageFilter, ImageFont

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / "media" / "nexus"
SOURCES = ROOT / "media" / "sources"

FONT_BOLD = os.environ.get("NEXUS_FONT_BOLD", "/usr/share/fonts/TTF/DejaVuSansCondensed-Bold.ttf")
FONT_REGULAR = os.environ.get("NEXUS_FONT_REGULAR", "/usr/share/fonts/TTF/DejaVuSans.ttf")
WHITE = (244, 246, 244, 255)
MUTED = (177, 184, 180, 255)
ORANGE = (232, 137, 42, 255)
GREEN = (36, 225, 67, 255)
RED = (244, 48, 45, 255)
DARK = (10, 14, 14, 255)


def font(size: int, bold: bool = False) -> ImageFont.FreeTypeFont:
    path = FONT_BOLD if bold else FONT_REGULAR
    if not Path(path).is_file():
        candidates = [
            "/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf" if bold
            else "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf",
            "/usr/share/fonts/TTF/DejaVuSans.ttf",
        ]
        path = next((p for p in candidates if Path(p).is_file()), "")
        if not path:
            raise SystemExit(
                f"FEHLER: Schriftart nicht gefunden.\n"
                f"Per NEXUS_FONT_BOLD / NEXUS_FONT_REGULAR übergeben oder DejaVu installieren."
            )
    return ImageFont.truetype(path, size)


def cover(img: Image.Image, size: tuple[int, int]) -> Image.Image:
    iw, ih = img.size
    tw, th = size
    scale = max(tw / iw, th / ih)
    resized = img.resize((round(iw * scale), round(ih * scale)), Image.Resampling.LANCZOS)
    left = (resized.width - tw) // 2
    top = (resized.height - th) // 2
    return resized.crop((left, top, left + tw, top + th))


def contain(img: Image.Image, size: tuple[int, int]) -> Image.Image:
    iw, ih = img.size
    tw, th = size
    scale = min(tw / iw, th / ih)
    return img.resize((max(1, round(iw * scale)), max(1, round(ih * scale))), Image.Resampling.LANCZOS)


def background(img: Image.Image, size: tuple[int, int]) -> Image.Image:
    bg = cover(img, size).filter(ImageFilter.GaussianBlur(30)).convert("RGBA")
    bg.alpha_composite(Image.new("RGBA", size, (4, 8, 8, 218)))
    draw = ImageDraw.Draw(bg, "RGBA")
    for y in range(size[1]):
        alpha = round(40 + 90 * y / max(size[1] - 1, 1))
        draw.line((0, y, size[0], y), fill=(0, 0, 0, alpha))
    draw.polygon([(0, 0), (size[0] * 0.68, 0), (size[0] * 0.40, size[1]), (0, size[1])], fill=(5, 9, 9, 90))
    return bg


def paste_card(canvas: Image.Image, img: Image.Image, xy: tuple[int, int], max_size: tuple[int, int],
               accent=ORANGE, border: int = 3, shadow: int = 22) -> tuple[int, int, int, int]:
    card = contain(img.convert("RGBA"), max_size)
    x, y = xy
    shadow_img = Image.new("RGBA", (card.width + shadow * 2, card.height + shadow * 2), (0, 0, 0, 0))
    sd = ImageDraw.Draw(shadow_img, "RGBA")
    sd.rounded_rectangle((shadow, shadow, shadow + card.width, shadow + card.height), radius=8, fill=(0, 0, 0, 205))
    shadow_img = shadow_img.filter(ImageFilter.GaussianBlur(shadow // 2))
    canvas.alpha_composite(shadow_img, (x - shadow, y - shadow))
    frame = Image.new("RGBA", (card.width + border * 2, card.height + border * 2), accent)
    frame.alpha_composite(card, (border, border))
    canvas.alpha_composite(frame, (x - border, y - border))
    return x, y, card.width, card.height


FILES = [
    "farmsense-thumbnail-1920x1080.jpg",
    "farmsense-header-1300x372.jpg",
    "farmsense-gallery-01-1920x1080.jpg",
    "farmsense-gallery-02-1920x1080.jpg",
]


def save(canvas: Image.Image, filename: str) -> None:
    path = OUT / filename
    canvas.convert("RGB").save(path, quality=94, optimize=True, progressive=True)
    if path.stat().st_size >= 8 * 1024 * 1024:
        raise SystemExit(f"File exceeds Nexus size limit: {path}")


def text(draw, xy, label, size, fill=WHITE, bold=False):
    """Use top-left text anchors for predictable spacing and ASCII captions."""
    label.encode("ascii")
    draw.text(xy, label, font=font(size, bold), fill=fill, anchor="lt")


def chip(draw, xy, label, size=22):
    label.encode("ascii")
    x, y = xy
    width = round(draw.textlength(label, font=font(size, True))) + 42
    height = size + 24
    draw.rounded_rectangle((x, y, x + width, y + height), radius=16,
                           fill=(15, 20, 20, 245), outline=ORANGE, width=2)
    text(draw, (x + 21, y + 12), label, size, bold=True)
    return width, height


def edge_rules(draw, width, height):
    # Echo the EVZ reference's orange rails and quiet charcoal panels.
    draw.rectangle((0, 0, width - 1, 7), fill=ORANGE)
    draw.rectangle((0, height - 8, width - 1, height - 1), fill=ORANGE)


def build_thumbnail(images):
    canvas = background(images[0], (1920, 1080))
    draw = ImageDraw.Draw(canvas, "RGBA")
    edge_rules(draw, *canvas.size)
    title = "FARMSENSE"
    text(draw, (104, 80), title, 132, bold=True)
    end = 104 + round(draw.textlength(title, font=font(132, True)))
    draw.rounded_rectangle((end + 20, 104, end + 122, 127), radius=10, fill=ORANGE)
    text(draw, (110, 234), "CROP INFO + HARVEST WITH E", 42, ORANGE, True)
    text(draw, (110, 292),
         "See what your crops are doing - and harvest them with E", 28, MUTED)
    labels = ["STAGE 2/3", "TIME LEFT", "HARVEST WITH E"]
    widths = [round(draw.textlength(label, font=font(22, True))) + 42 for label in labels]
    cx = 1816 - sum(widths) - 28
    for label, width in zip(labels, widths):
        chip(draw, (cx, 94), label)
        cx += width + 14
    draw.line((110, 354, 1810, 354), fill=ORANGE, width=2)
    # Both 500x260 source crops contain the plant name, stage, time and bar.
    # paste_card uses LANCZOS at 1.74x, preserving the source aspect ratio.
    captions = ("2/3 - 11 % - RIPE IN 56 MIN", "1/3 - 2 % - RIPE IN 2 H 3 MIN")
    for source, x, caption in zip(images, (80, 990), captions):
        crop = source.crop((704, 410, 1204, 670))
        _, _, width, height = paste_card(canvas, crop, (x, 545), (870, 453),
                                         border=3, shadow=24)
        caption_width = draw.textlength(caption, font=font(30, True))
        text(draw, (x + (width - caption_width) / 2, 545 + height + 24),
             caption, 30, WHITE, True)
    save(canvas, FILES[0])


def build_header(images):
    canvas = background(images[0], (1300, 372))
    draw = ImageDraw.Draw(canvas, "RGBA")
    draw.rectangle((0, 0, 445, 371), fill=DARK)
    draw.rectangle((439, 28, 442, 343), fill=ORANGE)
    # The info field is almost native-size; no HUD or title-overlay collision.
    crop = images[0].crop((510, 370, 1370, 710))
    crop = cover(crop, (806, 318))
    paste_card(canvas, crop, (470, 27), (806, 318), border=3, shadow=20)
    chip(draw, (1010, 287), "GROWTH + TIME LEFT", size=18)
    edge_rules(draw, *canvas.size)
    save(canvas, FILES[1])


def build_gallery(images):
    specs = [
        (images[0], "GROWTH AND TIME LEFT AT A GLANCE",
         "Stage, percentage and remaining time for every crop - planted or wild", FILES[2]),
        (images[1], "WILD CROPS IN THE PINE FOREST",
         "Corn, potatoes, blueberries and pumpkins now grow wild - and grow back after harvest", FILES[3]),
    ]
    for source, title, subtitle, filename in specs:
        canvas = background(source, (1920, 1080))
        draw = ImageDraw.Draw(canvas, "RGBA")
        edge_rules(draw, *canvas.size)
        text(draw, (110, 80), title, 72, bold=True)
        draw.rectangle((112, 172, 560, 177), fill=ORANGE)
        text(draw, (112, 208), subtitle, 30, MUTED)
        # 1660x700 crop displays the central field at its original pixel size.
        crop = source.crop((120, 200, 1780, 900))
        paste_card(canvas, crop, (130, 320), (1660, 700), border=3, shadow=30)
        save(canvas, filename)


def build_contact_sheet():
    sheet = Image.new("RGBA", (1540, 980), (18, 22, 22, 255))
    draw = ImageDraw.Draw(sheet)
    edge_rules(draw, *sheet.size)
    positions = [(30, 45), (790, 45), (30, 500), (790, 500)]
    for name, (x, y) in zip(FILES, positions):
        with Image.open(OUT / name) as image:
            thumb = contain(image.convert("RGBA"), (720, 405))
        text(draw, (x, y), name, 18, bold=True)
        sheet.alpha_composite(thumb, (x, y + 34))
    save(sheet, "farmsense-nexus-media-preview.jpg")


def main():
    images = []
    for name in ("screenshot-mushroom-farm.png", "screenshot-wild-yucca.png"):
        with Image.open(SOURCES / name) as source:
            if source.width < 1780 or source.height < 970:
                raise SystemExit(f"Source too small for the intended crop: {name}")
            images.append(source.convert("RGBA"))
    OUT.mkdir(parents=True, exist_ok=True)
    build_thumbnail(images)
    build_header(images)
    build_gallery(images)
    build_contact_sheet()
    for name in FILES + ["farmsense-nexus-media-preview.jpg"]:
        path = OUT / name
        with Image.open(path) as image:
            print(f"{name}: {image.width}x{image.height}, {path.stat().st_size:,} bytes")


if __name__ == "__main__":
    main()
