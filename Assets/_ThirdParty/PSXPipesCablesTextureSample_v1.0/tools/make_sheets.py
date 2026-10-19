#!/usr/bin/env python3
"""Contact sheet + 2x2 tiling sheet from the generated PNGs.

    python3 tools/make_sheets.py [--font some.ttf]
Writes previews/contact_sheet.png and previews/tiling_2x2.png.
"""
import os, sys, argparse
import numpy as np
from PIL import Image, ImageDraw, ImageFont

HERE = os.path.dirname(os.path.abspath(__file__))
KIT = os.path.abspath(os.path.join(HERE, ".."))
sys.path.insert(0, HERE)
from textures_def import TEXTURES
from decals_def import DECALS

BG = (22, 23, 25)
FG = (220, 218, 205)
DIM = (130, 130, 122)


def font(path, size):
    if path and os.path.exists(path):
        return ImageFont.truetype(path, size)
    return ImageFont.load_default(size)


def checker(w, h, a=(70, 70, 72), b=(96, 96, 98), s=8):
    y, x = np.mgrid[0:h, 0:w]
    m = ((x // s + y // s) % 2).astype(bool)
    out = np.where(m[..., None], np.array(b), np.array(a)).astype(np.uint8)
    return Image.fromarray(out)


def on_checker(im):
    bg = checker(*im.size).convert("RGBA")
    return Image.alpha_composite(bg, im.convert("RGBA")).convert("RGB")


def contact(fpath):
    f_t, f_l, f_s = font(fpath, 24), font(fpath, 12), font(fpath, 12)
    cw, gap = 256, 16
    ch = 256 + 128 + 26
    cols = 6
    W = cols * cw + (cols + 1) * gap
    top = 64
    rows = (len(TEXTURES) + cols - 1) // cols
    dec_top = top + rows * (ch + gap) + 36
    dw = (W - gap * 9) // 8
    H = dec_top + dw + 40
    sheet = Image.new("RGB", (W, H), BG)
    d = ImageDraw.Draw(sheet)
    d.text((gap, 16), "PSX Pipes & Cables Texture Sample v1.0", font=f_t, fill=FG)
    d.text((gap, 44), "18 seamless 128x128 textures (albedo / normal GL / roughness)  +  8 decals with alpha. "
                      "Shown 2x, nearest neighbour.", font=f_s, fill=DIM)
    for i, (n, _) in enumerate(TEXTURES):
        r, c = divmod(i, cols)
        x = gap + c * (cw + gap)
        y = top + r * (ch + gap)
        base = os.path.join(KIT, "textures", n, n)
        alb = Image.open(base + "_albedo.png")
        if alb.mode == "RGBA":
            alb = on_checker(alb.resize((256, 256), Image.NEAREST))
        else:
            alb = alb.resize((256, 256), Image.NEAREST)
        sheet.paste(alb, (x, y))
        sheet.paste(Image.open(base + "_normal_gl.png").convert("RGB"), (x, y + 256))
        sheet.paste(Image.open(base + "_roughness.png").convert("RGB"), (x + 128, y + 256))
        d.text((x, y + 256 + 128 + 6), f"{i + 1:02d}  {n}", font=f_l, fill=FG)
    d.text((gap, dec_top - 28), "Decals (128x128, straight alpha, edge colour bled)", font=f_l, fill=FG)
    for i, (n, _) in enumerate(DECALS):
        x = gap + i * (dw + gap)
        im = Image.open(os.path.join(KIT, "decals", n + ".png")).resize((dw, dw), Image.NEAREST)
        plate = Image.new("RGBA", (dw, dw), (112, 112, 108, 255))
        sheet.paste(Image.alpha_composite(plate, im).convert("RGB"), (x, dec_top))
        d.text((x, dec_top + dw + 6), n.replace("decal_", ""), font=f_s, fill=FG)
    return sheet


def tiling():
    cols = 5
    tiles = []
    for n, _ in TEXTURES:
        a = Image.open(os.path.join(KIT, "textures", n, n + "_albedo.png"))
        t = Image.new(a.mode, (256, 256))
        for ox in (0, 128):
            for oy in (0, 128):
                t.paste(a, (ox, oy))
        tiles.append(on_checker(t) if a.mode == "RGBA" else t.convert("RGB"))
    W = cols * 256
    H = ((len(tiles) + cols - 1) // cols) * 256
    sheet = Image.new("RGB", (W, H))
    for i, t in enumerate(tiles):
        r, c = divmod(i, cols)
        sheet.paste(t, (c * 256, r * 256))
    return sheet


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--font", default=None)
    a = ap.parse_args()
    out = os.path.join(KIT, "previews")
    os.makedirs(out, exist_ok=True)
    contact(a.font).save(os.path.join(out, "contact_sheet.png"), optimize=True)
    tiling().save(os.path.join(out, "tiling_2x2.png"), optimize=True)
    print("previews written")


if __name__ == "__main__":
    main()
