#!/usr/bin/env python3
"""Regenerate every texture and decal of the PSX Pipes & Cables Texture Sample.

    python3 tools/gen_textures.py --out .            # writes textures/ and decals/
    python3 tools/gen_textures.py --out /tmp/regen   # e.g. for a byte-compare

Requires Python 3 + numpy + scipy + Pillow. Same seed + same library versions
-> byte-identical PNGs (verified with numpy 2.5.2 / scipy 1.18.1 / Pillow 12.3.0).
"""
import argparse, os, sys
import numpy as np
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from psxlib import (N, rng_for, snoise, smooth, psx_quantize, quantize_gray, height_to_normal, bleed_rgb,
                    save_png, BAYER)
from textures_def import TEXTURES
from weather import weather
from decals_def import DECALS, BORDER

MASTER_SEED = 20261018


def build_texture(name, fn, seed):
    d = fn(rng_for(name, seed))
    alpha = d.get("alpha")
    d["albedo"], d["rough"] = weather(rng_for(name + ":weather", seed), d["albedo"], d["height"],
                                      d["rough"], alpha, **d.get("weather", {}))
    alb = psx_quantize(d["albedo"], d.get("colors", 32), d.get("dither", 12), mask=alpha)
    h = d["height"].astype(np.float64)
    ngl = height_to_normal(h, d.get("nstr", 1.0), gl=True)
    ndx = ngl.copy(); ndx[..., 1] = 255 - ndx[..., 1]
    rough = quantize_gray(d["rough"], 16)
    if alpha is not None:
        alb = bleed_rgb(alb, alpha, wrap=True)
        alb = np.dstack([alb, np.where(alpha, 255, 0).astype(np.uint8)])
        flat = np.array([128, 128, 255], np.uint8)
        # flat normals in cut-out texels; the bordering bevel stays on the opaque side
        ngl = np.where(alpha[..., None], ngl, flat)
        ndx = np.where(alpha[..., None], ndx, flat)
    return dict(albedo=alb, normal_gl=ngl, normal_dx=ndx, roughness=rough)


def build_decal(name, fn, seed):
    rgb, a, rough = fn(rng_for(name, seed))
    a = np.clip(a, 0, 1) * BORDER
    a8 = np.clip(np.round(np.round(a * 7) / 7 * 255), 0, 255).astype(np.uint8)  # 8 alpha steps
    opaque = a8 > 0
    col = psx_quantize(rgb, 24, 10, mask=opaque if opaque.any() else None)
    col = bleed_rgb(col, opaque, wrap=False)
    return dict(albedo=np.dstack([col, a8]))


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--out", default=os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
    ap.add_argument("--seed", type=int, default=MASTER_SEED)
    ap.add_argument("--only", default=None, help="comma separated names")
    args = ap.parse_args()
    only = set(args.only.split(",")) if args.only else None
    for name, fn in TEXTURES:
        if only and name not in only:
            continue
        maps = build_texture(name, fn, args.seed)
        od = os.path.join(args.out, "textures", name)
        os.makedirs(od, exist_ok=True)
        for k, arr in maps.items():
            save_png(os.path.join(od, f"{name}_{k}.png"), arr)
        print("texture", name)
    od = os.path.join(args.out, "decals")
    os.makedirs(od, exist_ok=True)
    for name, fn in DECALS:
        if only and name not in only:
            continue
        maps = build_decal(name, fn, args.seed)
        save_png(os.path.join(od, f"{name}.png"), maps["albedo"])
        print("decal", name)


if __name__ == "__main__":
    main()
