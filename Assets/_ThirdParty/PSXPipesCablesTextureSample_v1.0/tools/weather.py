"""Shared weathering pass applied to every texture before palette reduction.

Adds irregular imperfections: fine mottling, per-cell tone variation, uneven
dirt, stains / splotches, drip streaks, edge wear, scratches and cavity dirt.
Everything is built from periodic noise or wrap-around drawing, so the result
stays seamless; everything comes from the per-texture RNG, so it is
deterministic. Only colour / roughness change: no light direction is baked in.
"""
import numpy as np
from psxlib import (N, XC, YC, snoise, smooth, sstep, hexc, mix, draw_line, streak_down)

DEFAULTS = dict(
    mottle=0.16,      # fine brightness noise
    cells=32,         # cell size for tone variation (must divide 128)
    cell_amt=0.07,    # +- brightness per cell
    dirt=0.55,        # uneven dirt strength
    dirt_col="#3b3226",
    stains=10,        # stain coverage in percent (irregular noise shapes)
    stain_amt=0.5,
    drips=14,         # number of drip sources
    drip_amt=0.45,
    edge=0.5,        # edge wear (lightening of raised edges)
    scratches=22,
    scratch_amt=0.35,
    cavity=0.40,      # dirt in recesses
    desat=0.0,        # 0..1 global desaturation
)


def _lum(a):
    return a[..., 0] * 0.299 + a[..., 1] * 0.587 + a[..., 2] * 0.114


def weather(rng, alb, h, rough, alpha=None, **over):
    P = dict(DEFAULTS, **over)
    m = np.ones((N, N)) if alpha is None else alpha.astype(float)
    rough = rough.astype(float).copy()

    # 1. fine mottling
    hi = snoise(rng, beta=0.3, fmin=24)
    mid = snoise(rng, beta=1.2, fmin=6)
    alb = alb * (1 + P["mottle"] * ((hi - 0.5) * 1.6 + (mid - 0.5)))[..., None]

    # 2. per-cell tone variation with wobbly (noise-warped) cell borders
    c = P["cells"]
    nc = N // c
    wx = (snoise(rng, beta=2.0) - 0.5) * c * 0.5
    wy = (snoise(rng, beta=2.0) - 0.5) * c * 0.5
    ox, oy = rng.integers(c, size=2)         # random phase: cell borders never line up with the tile edge
    cid = (np.floor((XC + wx + ox) / c) % nc + nc * (np.floor((YC + wy + oy) / c) % nc)).astype(int)
    gains = 1 + P["cell_amt"] * rng.uniform(-1, 1, nc * nc)
    warm = 0.03 * rng.uniform(-1, 1, nc * nc)
    alb = alb * gains[cid][..., None]
    alb[..., 0] *= 1 + warm[cid]
    alb[..., 2] *= 1 - warm[cid]

    # 3. uneven dirt (multi-scale, thresholded so it comes in patches)
    g = 0.65 * snoise(rng, beta=2.3) + 0.35 * snoise(rng, beta=1.0, fmin=8)
    dm = sstep(0.42, 0.82, g) * P["dirt"] * m
    target = alb * 0.5 + hexc(P["dirt_col"]) * 0.22
    alb = mix(alb, target, dm)
    rough += 0.25 * dm

    # 4. stains and splotches: irregular shapes from thresholded band-limited
    #    noise (no round blobs), dark stains with a darker tide line, plus a
    #    few light dusty / chalky splotches
    st = np.zeros((N, N)); sp = np.zeros((N, N))
    if P["stains"]:
        cover = P["stains"] / 100.0                      # stains = rough % coverage
        shape = 0.6 * snoise(rng, beta=1.6, fmin=2) + 0.4 * snoise(rng, beta=0.8, fmin=6)
        thr = np.quantile(shape, 1 - cover)
        blob = sstep(thr - 0.01, thr + 0.03, shape)
        tide = np.clip(blob - sstep(thr + 0.03, thr + 0.07, shape), 0, 1)
        st = np.clip(blob * 0.55 + tide * 0.6, 0, 1) * m
        lshape = 0.6 * snoise(rng, beta=1.6, fmin=2) + 0.4 * snoise(rng, beta=0.8, fmin=6)
        lthr = np.quantile(lshape, 1 - cover * 0.5)
        sp = sstep(lthr, lthr + 0.04, lshape) * (snoise(rng, beta=0.4, fmin=12) > 0.4) * m
        alb = mix(alb, alb * 0.55 + hexc("#2a2016") * 0.1, st * P["stain_amt"])
        lt = _lum(alb)[..., None]
        alb = mix(alb, lt * 0.6 + hexc("#8a8270") * 0.45, sp * P["stain_amt"] * 0.7)
        rough += 0.1 * sp - 0.08 * st

    # 5. drip streaks running down (+row)
    if P["drips"]:
        src = np.zeros((N, N))
        for _ in range(P["drips"]):
            x, y = int(rng.integers(N)), int(rng.integers(N))
            w = int(rng.integers(1, 3))
            src[y, x:x + w] = rng.uniform(0.5, 1.0)
            src[y, (x + w) % N] = 0.3
        drip = streak_down(src, snoise(rng, beta=0.5, fmin=8, sx=0.15), base=0.9, spread=0.09, iters=60)
        drip = np.clip(drip, 0, 1) * m
        alb = mix(alb, alb * 0.6 + hexc(P["dirt_col"]) * 0.15, drip * P["drip_amt"])
        rough += 0.1 * drip

    # 6. edge wear: raised edges / convex texels get lighter and smoother
    if P["edge"]:
        conv = h - smooth(h, 1.5)
        e = sstep(0.8, 2.2, conv / (conv.std() + 1e-6)) * m
        e = e * (snoise(rng, beta=0.6, fmin=10) > 0.35)
        lt = _lum(alb)[..., None]
        worn = np.clip(lt * 1.35 + 0.06, 0, 1) * np.array([1.0, 1.0, 1.02])
        alb = mix(alb, worn, e * P["edge"])
        rough -= 0.15 * e * P["edge"]

    # 7. scratches (crisp texel lines, wrap-around)
    if P["scratches"]:
        sc = np.zeros((N, N)); dk = np.zeros((N, N))
        for i in range(P["scratches"]):
            x0, y0 = rng.random() * N, rng.random() * N
            L = rng.uniform(3, 18); a = rng.uniform(0, np.pi)
            draw_line(sc if rng.random() < 0.75 else dk, x0, y0, x0 + L * np.cos(a), y0 + L * np.sin(a), 1)
        sc *= m; dk *= m
        alb = mix(alb, np.clip(alb * 1.3 + 0.07, 0, 1), sc * P["scratch_amt"])
        alb = mix(alb, alb * 0.6, dk * P["scratch_amt"])
        rough -= 0.1 * sc

    # 8. dirt in recesses
    cc = h - smooth(h, 2.0)
    cav = np.clip(-cc / (2.5 * cc.std() + 1e-6), 0, 1) * m
    alb = alb * (1 - P["cavity"] * cav)[..., None]
    rough += 0.1 * cav

    # 9. optional global desaturation
    if P["desat"]:
        lt = _lum(alb)[..., None]
        alb = mix(alb, np.broadcast_to(lt, alb.shape), P["desat"])
    return np.clip(alb, 0, 1), np.clip(rough, 0, 1)
