"""Definitions of the 18 tileable textures. Each function gets its own RNG and
returns a dict with float maps; gen_textures.py encodes them to PNG."""
import numpy as np
from psxlib import (N, XC, YC, snoise, worley, smooth, sstep, hexc, fill, mix, ramp,
                    draw_line, streak_down, hpipe, hpipe_T, band_x, band_y, disc, wrapd,
                    norm01)

RUST = [(0.0, "#1e1610"), (0.35, "#46301f"), (0.6, "#6e4a32"), (0.82, "#8c6a4e"), (1.0, "#a48c72")]


def rust_layer(rng, scale=1.0):
    a = snoise(rng, beta=2.2)
    b = snoise(rng, beta=1.2, fmin=8)
    t = np.clip(0.75 * a + 0.35 * b - 0.05, 0, 1) * scale
    return ramp(t, RUST), t


def dirt(rng, alb, amount=0.25, beta=2.4, thr=0.45):
    g = snoise(rng, beta=beta)
    m = sstep(thr, thr + 0.35, g) * amount
    sp = snoise(rng, beta=0.4, fmin=20) > 0.72
    alb = mix(alb, alb * 0.55, m)
    alb = np.where(sp[..., None], alb * 0.82, alb)
    return alb, m


def chips(rng, coverage=0.5, fine=0.5):
    """Paint-chip mask: low freq noise gated by finer noise."""
    a = snoise(rng, beta=2.0)
    b = snoise(rng, beta=1.0, fmin=6)
    v = a * (1 - fine) + b * fine
    return v > np.quantile(v, 1 - coverage)  # exact coverage fraction


def edge_of(mask):
    m = mask.astype(np.uint8)
    nb = (np.roll(m, 1, 0) + np.roll(m, -1, 0) + np.roll(m, 1, 1) + np.roll(m, -1, 1))
    return mask & (nb < 4)


# ------------------------------------------------------------------ PIPES
def pipes_wall_steel(rng):
    n1 = snoise(rng, beta=2.0); n2 = snoise(rng, beta=0.8, fmin=10)
    wall = mix(fill("#5e5d57"), fill("#7a776d"), 0.6 * n1 + 0.4 * n2)
    h = 0.6 * n2 + 0.4 * n1
    rough = 0.88 + 0.1 * n2
    alb = wall
    # streaks under brackets / pipes
    src = np.zeros((N, N))
    pipes = [(26, 11, "#8b9095", 0.5), (70, 17, "#3c5a44", 0.62), (108, 6, "#80583e", 0.38)]
    for yc, r, c, ro in pipes:
        rr = np.full((1, N), float(r))
        cpl = band_x(52 + yc, 4)[0:1]
        rr = rr + 2.5 * cpl
        m, prof, d, s = hpipe(yc, rr)
        hp = 2.0 + rr * prof
        tone = snoise(rng, beta=1.6, sx=6.0)
        col = mix(fill(c) * 0.9, fill(c) * 1.1, tone)
        if c == "#3c5a44":
            ch = chips(rng, 0.15) & m
            col = np.where(ch[..., None], mix(fill("#77797a"), rust_layer(rng)[0], 0.6), col)
            hp = hp - 0.4 * ch
        if c == "#80583e":
            pat = snoise(rng, beta=1.5) > 0.74
            col = np.where(pat[..., None], fill("#5a7a6a"), col)
        alb = np.where(m[..., None], col, alb)
        h = np.where(m, np.maximum(h, hp), h)
        rough = np.where(m, ro + 0.15 * tone, rough)
        src = np.maximum(src, (band_y(yc + r, 1) & (snoise(rng, beta=0.5, fmin=12) > 0.7)) * 0.7)
        # brackets
        for bx in (20, 84):
            bm = band_x(bx, 3) & (np.abs(wrapd(YC, yc)) < r + 1.5)
            alb = np.where(bm[..., None], fill("#4c4f50") * (0.9 + 0.2 * tone[..., None]), alb)
            h = np.where(bm, 2.0 + (r + 1.5) * np.sqrt(np.clip(1 - (wrapd(YC, yc) / (r + 1.5)) ** 2, 0, 1)), h)
            rough = np.where(bm, 0.7, rough)
            for sgn in (-1, 1):
                bolt, bp = disc(bx, yc + sgn * (r + 4), 2.2)
                alb = np.where(bolt[..., None], fill("#3a3a36"), alb)
                h = np.where(bolt, 1.5 + 1.5 * bp, h)
            src = np.maximum(src, band_x(bx, 2) & band_y(yc + r + 3, 1.5))
    st = streak_down(src.astype(float), snoise(rng, beta=0.5, fmin=8, sx=0.2), iters=40)
    wallm = h < 1.2
    alb = np.where(wallm[..., None], mix(alb, fill("#5a4030"), np.clip(st * 0.8, 0, 0.8)), alb)
    alb, _ = dirt(rng, alb, 0.3)
    return dict(albedo=alb, height=h, rough=rough, nstr=0.9, colors=32, dither=12)


def _hstrip_pipes(rng, specs):
    """specs: list of dict(yc, r, z, couplings=[(x, w, dr)], ribs=0)."""
    H = np.full((N, N), -1e9); ID = np.full((N, N), -1)
    D = np.zeros((N, N)); S = np.zeros((N, N)); P = np.zeros((N, N)); R = np.ones((N, N))
    for i, sp in enumerate(specs):
        rr = np.full((1, N), float(sp["r"]))
        for (cx, w, dr) in sp.get("couplings", []):
            rr = rr + dr * band_x(cx, w / 2)[0:1]
        if sp.get("ribs"):
            rr = rr - sp["ribdepth"] * (0.5 + 0.5 * np.cos(2 * np.pi * sp["ribs"] * XC[0:1] / N))
        m, prof, d, s = hpipe(sp["yc"], rr)
        hz = sp.get("z", 0) + rr * prof
        take = m & (hz > H)
        H = np.where(take, hz, H); ID = np.where(take, i, ID)
        D = np.where(take, d, D); S = np.where(take, s, S); P = np.where(take, prof, P)
        R = np.where(take, np.broadcast_to(rr, (N, N)), R)
    return H, ID, D, S, P, R


def pipe_copper_strip(rng):
    specs = [dict(yc=44, r=17, couplings=[(16, 10, 2.5), (80, 10, 2.5)]),
             dict(yc=94, r=10, couplings=[(48, 7, 2.0), (112, 7, 2.0)])]
    H, ID, D, S, P, R = _hstrip_pipes(rng, specs)
    alpha = ID >= 0
    tone = snoise(rng, beta=1.4, sx=5.0)
    cop = ramp(0.25 + 0.6 * tone, [(0, "#4a3022"), (0.5, "#86583c"), (0.8, "#a8785a"), (1, "#c09c80")])
    oxid = snoise(rng, beta=1.8)
    cop = mix(cop, fill("#3e2a20"), sstep(0.5, 0.8, oxid) * 0.7)
    ver = (snoise(rng, beta=1.3, fmin=3) * (0.6 + 0.4 * oxid)) > 0.62
    alb = np.where(ver[..., None], mix(fill("#4f7a6c"), fill("#7ea294"), snoise(rng, beta=0.6, fmin=10)), cop)
    # solder rings at coupling edges
    ring = np.zeros((N, N), bool)
    for cx in (16, 80):
        ring |= (np.abs(np.abs(wrapd(XC, cx)) - 5.5) < 1.0) & (ID == 0)
    for cx in (48, 112):
        ring |= (np.abs(np.abs(wrapd(XC, cx)) - 4.0) < 1.0) & (ID == 1)
    alb = np.where(ring[..., None], fill("#a9a49a"), alb)
    rough = np.where(ver, 0.85, 0.35 + 0.3 * oxid)
    alb, _ = dirt(rng, alb, 0.2)
    H = np.where(alpha, H, 0)
    return dict(albedo=alb, height=H, rough=rough, alpha=alpha, nstr=0.8, colors=32, dither=12)


def pipe_rusty_strip(rng):
    specs = [dict(yc=64, r=38, couplings=[(0, 10, 6)])]
    H, ID, D, S, P, R = _hstrip_pipes(rng, specs)
    alpha = ID >= 0
    rc, rt = rust_layer(rng)
    paint = mix(fill("#4e5c50"), fill("#66745e"), snoise(rng, beta=1.5, sx=3))
    keep = chips(rng, 0.38, fine=0.35)
    alb = np.where(keep[..., None], paint, rc)
    e = edge_of(~keep) & (snoise(rng, beta=0.5, fmin=10) > 0.5)
    alb = np.where(e[..., None], fill("#2e2018"), alb)
    hh = H + 0.5 * keep - 0.6 * (snoise(rng, beta=0.6, fmin=14) > 0.7)
    # bolts on the flange (top and bottom rim seen from the side)
    for bx in (-3, 3):
        for yy in (64 - 40, 64 + 40):
            bm, bp = disc(bx % N, yy, 2.4)
            bm &= alpha
            alb = np.where(bm[..., None], fill("#4a3022"), alb)
            hh = np.where(bm, hh + 2 * bp, hh)
    # seam weld line
    weld = band_x(64, 1.2) & alpha
    alb = np.where(weld[..., None], fill("#3a2a20"), alb)
    hh = np.where(weld, hh + 1.0, hh)
    st = streak_down(((rt > 0.75) & ~keep & alpha).astype(float) * 0.5, snoise(rng, beta=0.5, fmin=8, sx=0.2),
                     base=0.8, spread=0.12, iters=14)
    alb = mix(alb, fill("#4e3420"), np.clip(st, 0, 0.45) * alpha)
    rough = np.where(keep, 0.7, 0.95)
    alb, _ = dirt(rng, alb, 0.3)
    hh = np.where(alpha, hh, 0)
    return dict(albedo=alb, height=hh, rough=rough, alpha=alpha, nstr=0.7, colors=32, dither=14)


def pipe_corrugated_strip(rng):
    specs = [dict(yc=30, r=13, ribs=21, ribdepth=0.0, z=0),
             dict(yc=86, r=27, ribs=16, ribdepth=0.0, z=0)]
    H, ID, D, S, P, R = _hstrip_pipes(rng, specs)
    alpha = ID >= 0
    rib1 = 0.5 + 0.5 * np.cos(2 * np.pi * 21 * XC / N)
    rib2 = 0.5 + 0.5 * np.cos(2 * np.pi * 16 * XC / N)
    rib = np.where(ID == 0, rib1, rib2)
    hh = H + np.where(ID == 0, 1.5, 2.5) * rib * P
    tone = snoise(rng, beta=1.6, sx=3)
    yel = mix(fill("#a8861c"), fill("#d0aa30"), tone)
    blk = mix(fill("#1c1d1f"), fill("#34363a"), tone)
    alb = np.where((ID == 0)[..., None], yel, blk)
    alb = mix(alb, alb * 0.6, (1 - rib) * 0.5)  # dust collects in valleys (material, not light)
    dust = snoise(rng, beta=1.8)
    alb = mix(alb, fill("#6a6458"), sstep(0.55, 0.9, dust) * (1 - rib) * 0.8)
    # printed stripe on the black hose
    stripe = (ID == 1) & (np.abs(D + 6) < 1.2)
    alb = np.where(stripe[..., None], fill("#b0a040"), alb)
    rough = np.where(ID == 0, 0.55, 0.65) + 0.25 * (1 - rib)
    hh = np.where(alpha, hh, 0)
    return dict(albedo=alb, height=hh, rough=rough, alpha=alpha, nstr=0.9, colors=24, dither=10)


def pipes_painted_vertical(rng):
    n1 = snoise(rng, beta=2.0)
    by = ((YC + 16) % 32) < 1.0
    bx = ((XC + 32 * (((YC + 16) // 32) % 2)) % 64) < 1.0
    wall = mix(fill("#2e3530"), fill("#454c44"), n1)
    alb = np.where((by | bx)[..., None], fill("#202420"), wall)
    h = 0.6 * n1 - 0.8 * (by | bx)
    rough = 0.9 + 0.05 * n1
    pipes = [(18, 9, "#c8a024"), (54, 15, "#4a6a3a"), (92, 11, "#8a2a22"), (119, 5, "#7c8084")]
    for i, (xc, r, c) in enumerate(pipes):
        m, prof, d, s = hpipe_T(xc, r)
        tone = snoise(rng, beta=1.6, sy=5.0)
        col = mix(fill(c) * 0.88, fill(c) * 1.08, tone)
        ch = chips(rng, 0.12 if i < 3 else 0.06) & m
        rc, _ = rust_layer(rng)
        col = np.where(ch[..., None], rc, col)
        col = np.where((edge_of(ch) & m)[..., None], fill("#8c8c88"), col)
        if i == 0:  # black hazard band
            hb = band_y(64, 7) & ((((XC + YC) // 4) % 2) == 0)
            col = np.where(hb[..., None], fill("#1a1a18"), col)
        if i == 1:  # stencil arrow pointing up
            lx, ly = wrapd(XC, xc), wrapd(YC, 44)
            arrow = ((ly > -8) & (ly < 0) & (np.abs(lx) < (ly + 8) * 0.6)) | ((ly >= 0) & (ly < 9) & (np.abs(lx) < 2))
            col = np.where(arrow[..., None], fill("#d8d4c4"), col)
        hp = 2 + r * prof - 0.4 * ch
        alb = np.where(m[..., None], col, alb)
        h = np.where(m, hp, h)
        rough = np.where(m, np.where(ch, 0.9, 0.55), rough)
    for yc in (24, 88):
        cm = band_y(yc, 2.5)
        for xc, r, _ in pipes:
            mm = cm & (np.abs(wrapd(XC, xc)) < r + 1.5)
            alb = np.where(mm[..., None], fill("#3a3c3c"), alb)
            h = np.where(mm, 2 + (r + 1.5) * np.sqrt(np.clip(1 - (wrapd(XC, xc) / (r + 1.5)) ** 2, 0, 1)), h)
            rough = np.where(mm, 0.6, rough)
    alb, _ = dirt(rng, alb, 0.35)
    return dict(albedo=alb, height=h, rough=rough, nstr=0.8, colors=32, dither=12)


# ------------------------------------------------------------------ CABLES
def _cables(rng, specs, vertical=False):
    H = np.full((N, N), -1e9); ID = np.full((N, N), -1); P = np.zeros((N, N)); D = np.zeros((N, N))
    A = XC if not vertical else YC
    B = YC if not vertical else XC
    for i, (c0, r, k, amp, ph, z) in enumerate(specs):
        cen = c0 + amp * np.sin(2 * np.pi * k * A / N + ph)
        d = wrapd(B, cen)
        prof = np.sqrt(np.clip(1 - (d / r) ** 2, 0, 1))
        m = np.abs(d) < r
        hz = z + r * prof
        take = m & (hz > H)
        H = np.where(take, hz, H); ID = np.where(take, i, ID); P = np.where(take, prof, P)
        D = np.where(take, d, D)
    return H, ID, P, D


def cables_bundle_strip(rng):
    specs = [(40, 5, 1, 4, 0.0, 1), (50, 6, 2, 3, 1.0, 2), (60, 4, 1, 5, 2.0, 1.5), (68, 7, 1, 3, 3.5, 2.5),
             (78, 4.5, 2, 4, 4.2, 1.2), (88, 5.5, 1, 4, 5.1, 2.0), (56, 3, 3, 9, 0.7, 3.0), (96, 3.5, 1, 3, 2.2, 0.8)]
    cols = ["#1c1c1e", "#2a2a2c", "#8a2420", "#151517", "#1e3a7a", "#3a3c40", "#c09020", "#2c5a2a"]
    H, ID, P, D = _cables(rng, specs)
    alpha = ID >= 0
    tone = snoise(rng, beta=1.2, sx=4)
    alb = np.zeros((N, N, 3))
    for i, c in enumerate(cols):
        alb = np.where((ID == i)[..., None], mix(fill(c) * 0.85, fill(c) * 1.2, tone), alb)
    # print marks on a few cables
    mk = (((XC // 2) % 8) < 3) & (np.abs(D + 1) < 0.8) & np.isin(ID, [1, 3, 5])
    alb = np.where(mk[..., None], fill("#7a7a74"), alb)
    hh = H.copy()
    for tx in (40, 104):
        tie = band_x(tx, 1.6) & alpha
        alb = np.where(tie[..., None], fill("#cfcabc"), alb)
        hh = np.where(tie, hh + 1.2, hh)
        head = band_x(tx + 3, 2) & band_y(36, 3)
        alb = np.where(head[..., None], fill("#bdb8aa"), alb)
        hh = np.where(head, 7, hh)
        alpha = alpha | head
    rough = np.where(np.isin(ID, [6]), 0.4, 0.6) + 0.15 * tone
    hh = np.where(alpha, hh, 0)
    alb, _ = dirt(rng, alb, 0.15)
    return dict(albedo=alb, height=hh, rough=rough, alpha=alpha, nstr=0.9, colors=32, dither=10)


def cable_tray(rng):
    n1 = snoise(rng, beta=1.8)
    alb = mix(fill("#7d8286"), fill("#9aa0a4"), n1)
    sp, _, cid = worley(rng, 40)
    alb = alb * (0.94 + 0.08 * (cid % 5)[..., None] / 4)  # zinc spangle
    h = np.zeros((N, N)) + 0.3 * n1
    lu = (XC + 8 * ((YC // 8) % 2)) % 16
    lv = YC % 8
    slot = (np.abs(lu - 8) < 5) & (np.abs(lv - 4) < 1.4) & (np.abs(wrapd(YC, 0)) > 12)
    alb = np.where(slot[..., None], fill("#141414"), alb)
    h = np.where(slot, -2, h)
    rail = np.abs(wrapd(YC, 0)) < 8
    lip = np.abs(wrapd(YC, 0)) < 5
    alb = np.where(rail[..., None], mix(fill("#8a9094"), fill("#a8aeb2"), n1), alb)
    h = np.where(rail, 3 + 1.5 * lip, h)
    holes = rail & (np.hypot(wrapd(XC % 32, 16), wrapd(YC, 0)) < 2.2)
    alb = np.where(holes[..., None], fill("#101010"), alb)
    h = np.where(holes, -1, h)
    specs = [(28, 6, 1, 2, 0.3, 0), (44, 4, 1, 3, 2.0, 0), (60, 7, 1, 2, 4.0, 0), (76, 4, 2, 2, 1.0, 0),
             (90, 5.5, 1, 3, 5.0, 0), (106, 4, 1, 2, 3.0, 0)]
    cols = ["#1d1d1f", "#5a1a16", "#2a2a2c", "#1a2a5a", "#202022", "#2f2f30"]
    CH, ID, P, D = _cables(rng, specs)
    cm = ID >= 0
    tone = snoise(rng, beta=1.2, sx=4)
    for i, c in enumerate(cols):
        alb = np.where((ID == i)[..., None], mix(fill(c) * 0.85, fill(c) * 1.2, tone), alb)
    h = np.where(cm, np.maximum(h, CH + 0.5), h)
    for tx in (16, 80):
        tie = band_x(tx, 1.5) & cm
        alb = np.where(tie[..., None], fill("#d0ccc0"), alb)
        h = np.where(tie, h + 1, h)
    rough = np.where(cm, 0.62, 0.5 + 0.2 * n1)
    alb, _ = dirt(rng, alb, 0.35)
    return dict(albedo=alb, height=h, rough=rough, nstr=0.8, colors=32, dither=12)


def cable_braided_strip(rng):
    specs = [dict(yc=40, r=18), dict(yc=80, r=8), dict(yc=102, r=8)]
    H, ID, D, S, P, R = _hstrip_pipes(rng, specs)
    alpha = ID >= 0
    weave = np.sin(2 * np.pi * (XC + S) / 8) * np.sin(2 * np.pi * (XC - S) / 8)
    tone = snoise(rng, beta=1.0, sx=3)
    braid = mix(fill("#6e7276"), fill("#b0b4b6"), 0.5 + 0.5 * weave)
    braid = mix(braid, fill("#4a3a2a"), sstep(0.6, 0.9, snoise(rng, beta=1.8)) * 0.6)
    orange = mix(fill("#b0501a"), fill("#d06a24"), tone)
    black = mix(fill("#18181a"), fill("#2a2a2e"), tone)
    alb = np.where((ID == 0)[..., None], braid, np.where((ID == 1)[..., None], orange, black))
    # tiny print band
    bits = rng.random((3, N // 2)) > 0.45
    bitmap = np.zeros((N, N), bool)
    for j, cid in enumerate((1, 2)):
        rowsel = (ID == cid) & (np.abs(D + 0.5) < 1.5)
        cols_ = np.repeat(bits[j], 2)[None, :] & (((XC // 2) % 16) < 11)
        bitmap |= rowsel & cols_ & ((np.floor(YC) % 2) == 0)
    alb = np.where(bitmap[..., None], fill("#e0dccc") * np.where((ID == 2)[..., None], 0.9, 0.2), alb)
    hh = H + np.where(ID == 0, 0.8 * weave, 0)
    rough = np.where(ID == 0, 0.45 - 0.15 * weave, 0.6)
    hh = np.where(alpha, hh, 0)
    alb, _ = dirt(rng, alb, 0.15)
    return dict(albedo=alb, height=hh, rough=rough, alpha=alpha, nstr=0.9, colors=32, dither=10)


# ------------------------------------------------------------------ BRUSHED METAL
def _brushed(rng, sx=14.0):
    a = snoise(rng, beta=1.0, fmin=4, sx=sx)
    b = snoise(rng, beta=0.6, fmin=20, sx=sx * 1.5)
    return 0.6 * a + 0.4 * b


def metal_brushed_panels(rng):
    px = (XC // 64).astype(int) % 2; py = (YC // 64).astype(int) % 2
    a = _brushed(rng); b = _brushed(rng).T
    br = np.where(((px + py) % 2)[...], a, b)
    tint = np.array([0.96, 1.02, 1.0, 0.98])[(px + 2 * py)]
    alb = mix(fill("#9da3a8"), fill("#c4c9cd"), br) * tint[..., None]
    lx, ly = XC % 64, YC % 64
    groove = (lx < 1.0) | (ly < 1.0)
    groove2 = ((lx >= 1) & (lx < 2)) | ((ly >= 1) & (ly < 2))
    alb = np.where(groove[..., None], fill("#2a2c2e"), alb)
    alb = np.where(groove2[..., None], alb * 0.75, alb)
    h = 0.25 * br - 1.5 * groove - 0.6 * groove2
    rough = 0.38 + 0.15 * br + 0.4 * groove
    for cx in (6, 58):
        for cy in (6, 58):
            sm = np.hypot((lx - cx), (ly - cy)) < 2.5
            sp = np.sqrt(np.clip(1 - (np.hypot(lx - cx, ly - cy) / 2.5) ** 2, 0, 1))
            slot = sm & (np.abs(ly - cy) < 0.6)
            alb = np.where(sm[..., None], fill("#7c8186"), alb)
            alb = np.where(slot[..., None], fill("#303234"), alb)
            h = np.where(sm, 0.8 * sp - 0.5 * slot, h)
    alb, _ = dirt(rng, alb, 0.2, thr=0.55)
    return dict(albedo=alb, height=h, rough=rough, nstr=1.0, colors=24, dither=8)


def metal_brushed_dark(rng):
    br = _brushed(rng, sx=10)
    low = snoise(rng, beta=2.4)
    alb = mix(fill("#2e333a"), fill("#4a5059"), 0.7 * br + 0.3 * low)
    sc = np.zeros((N, N))
    for _ in range(46):
        x0, y0 = rng.random() * N, rng.random() * N
        L = rng.uniform(4, 34); ang = rng.uniform(0, np.pi)
        draw_line(sc, x0, y0, x0 + L * np.cos(ang), y0 + L * np.sin(ang), rng.uniform(0.5, 1))
    alb = mix(alb, fill("#8c939c"), sc[..., None] * 0.9)
    wear = sstep(0.62, 0.85, snoise(rng, beta=1.6))
    alb = mix(alb, fill("#6a7078"), wear * 0.5)
    rough = 0.4 + 0.15 * br - 0.12 * sc + 0.1 * low
    h = 0.3 * br - 0.6 * sc
    # props / weapons metal: lighter weathering than the wall and floor sets
    wx = dict(dirt=0.3, stains=6, drips=6, scratches=10, edge=0.3)
    return dict(albedo=alb, height=h, rough=rough, nstr=1.0, colors=24, dither=8, weather=wx)


def metal_diamond_plate(rng):
    lu = XC % 16 - 8; lv = YC % 16 - 8
    par = ((XC // 16 + YC // 16) % 2).astype(bool)
    a = (lu + lv) / np.sqrt(2); b = (lu - lv) / np.sqrt(2)
    aa = np.where(par, a, b); bb = np.where(par, b, a)
    e = (aa / 6.0) ** 2 + (bb / 1.9) ** 2
    raised = e < 1
    base = mix(fill("#7a8084"), fill("#949a9e"), snoise(rng, beta=1.8))
    top = mix(fill("#a8aeb2"), fill("#c6cbce"), snoise(rng, beta=1.0, fmin=4))
    wear = snoise(rng, beta=2.0)
    alb = np.where(raised[..., None], mix(base, top, sstep(0.3, 0.7, wear)), base)
    dm = sstep(0.5, 0.8, snoise(rng, beta=2.2)) * (~raised)
    alb = mix(alb, fill("#3a342c"), dm * 0.7)
    rc, rt = rust_layer(rng)
    rs = (snoise(rng, beta=1.6) > 0.75) & ~raised
    alb = np.where(rs[..., None], mix(alb, rc, 0.7), alb)
    h = 1.6 * np.sqrt(np.clip(1 - e, 0, 1)) + 0.15 * snoise(rng, beta=1.0, fmin=8)
    rough = np.where(raised, 0.35 + 0.2 * (1 - wear), 0.7) + 0.2 * dm
    return dict(albedo=alb, height=h, rough=rough, nstr=1.0, colors=24, dither=10)


# ------------------------------------------------------------------ RUST
def metal_rust_heavy(rng):
    a = snoise(rng, beta=2.4); b = snoise(rng, beta=1.4, fmin=4); c = snoise(rng, beta=0.5, fmin=16)
    rt = np.clip((0.55 * a + 0.3 * b + 0.15 * c - 0.5) * 1.8 + 0.5, 0, 1)
    rc = ramp(rt, [(0.0, "#130d09"), (0.25, "#33221a"), (0.45, "#563a28"), (0.62, "#734e34"),
                   (0.8, "#8e6a4c"), (1.0, "#ac9274")])
    dark = sstep(0.55, 0.8, snoise(rng, beta=2.0))
    rc = mix(rc, rc * 0.45, dark * 0.8)
    f1, f2, _ = worley(rng, 70)
    pits = (f1 < 1.6) & (snoise(rng, beta=1.0) > 0.45)
    flake_n = snoise(rng, beta=2.0)
    levels = np.floor(flake_n * 4) / 4
    edges = edge_of(np.floor(flake_n * 4) % 2 == 0)
    bright = snoise(rng, beta=0.8, fmin=10)
    alb = mix(rc, ramp(bright, RUST) * 1.05, 0.2)
    alb = np.where(pits[..., None], fill("#1e0e06"), alb)
    alb = np.where(edges[..., None], alb * 0.7, alb)
    orange = sstep(0.7, 0.9, bright) * (levels > 0.4)
    alb = mix(alb, fill("#94683e"), orange * 0.5)
    h = 2.0 * levels + 0.5 * bright - 1.0 * pits
    rough = 0.85 + 0.12 * rt
    return dict(albedo=alb, height=h, rough=rough, nstr=1.0, colors=32, dither=14)


def metal_painted_rust_blue(rng):
    paint = mix(fill("#2a5482"), fill("#3a6898"), snoise(rng, beta=1.8))
    paint = mix(paint, fill("#6a8aa4"), sstep(0.6, 0.95, snoise(rng, beta=2.4)) * 0.5)  # faded
    ch = chips(rng, 0.22, fine=0.4)
    rc, rt = rust_layer(rng)
    alb = np.where(ch[..., None], rc, paint)
    e = edge_of(ch)
    alb = np.where(e[..., None], fill("#8c9296"), alb)
    seam = np.abs(wrapd(YC, 64)) < 1.0
    rivet = np.zeros((N, N), bool); rp = np.zeros((N, N))
    for x in range(8, N, 16):
        for yy in (59, 69):
            m, p = disc(x, yy % N, 2.0); rivet |= m; rp = np.maximum(rp, p)
    src = (ch & (snoise(rng, beta=0.5, fmin=10) > 0.75)).astype(float) * 0.7 + rivet * 0.6
    st = streak_down(src, snoise(rng, beta=0.5, fmin=8, sx=0.15), base=0.8, spread=0.17, iters=40)
    alb = mix(alb, fill("#6a4a30"), np.clip(0.9 * st - ch, 0, 0.7))
    alb = np.where(seam[..., None], fill("#1c2430"), alb)
    alb = np.where(rivet[..., None], mix(fill("#2e5078"), fill("#6a4c34"), (rp < 0.6).astype(float)), alb)
    h = 0.6 * (~ch) + 0.3 * rt * ch - 1.2 * seam + 1.4 * rp
    rough = np.where(ch, 0.92, 0.6) + 0.05 * rt
    alb, _ = dirt(rng, alb, 0.25)
    return dict(albedo=alb, height=h, rough=rough, nstr=1.0, colors=32, dither=12)


def metal_corrugated_rust(rng):
    wave = np.cos(2 * np.pi * 8 * XC / N)
    _, _, cid = worley(rng, 30)
    galv = mix(fill("#878d90"), fill("#a2a8aa"), snoise(rng, beta=1.6))
    galv = galv * (0.95 + 0.06 * (cid % 4)[..., None] / 3)
    valley = sstep(0.2, 1.0, -wave)
    src = (snoise(rng, beta=0.6, fmin=8) > 0.72).astype(float) * (0.4 + 0.6 * valley)
    st = streak_down(src, snoise(rng, beta=0.4, fmin=6, sx=0.15), base=0.9, spread=0.09, iters=70)
    rc, rt = rust_layer(rng)
    patch = sstep(0.62, 0.8, snoise(rng, beta=2.0) + 0.15 * valley)
    alb = mix(galv, rc, np.clip(0.8 * st + patch, 0, 1))
    alb = mix(alb, alb * 0.75, valley * 0.35)
    h = 2.5 * wave + 0.3 * rt * patch
    rough = 0.5 + 0.4 * np.clip(st + patch, 0, 1)
    alb, _ = dirt(rng, alb, 0.2)
    return dict(albedo=alb, height=h, rough=rough, nstr=0.9, colors=32, dither=12)


def metal_rivet_plates(rng):
    row = (YC // 64).astype(int) % 2
    lx = (XC + 32 * (row % 2)) % 64; ly = YC % 64
    pid = ((XC + 32 * (row % 2)) // 64).astype(int) % 2 + 2 * row
    tint = np.array([1.0, 0.94, 0.97, 1.03])[pid]
    base = mix(fill("#3e4a3c"), fill("#566250"), snoise(rng, beta=1.8)) * tint[..., None]
    wear = sstep(0.62, 0.85, snoise(rng, beta=1.5))
    base = mix(base, fill("#7a7e76"), wear * 0.6)
    seam = (lx < 1) | (ly < 1)
    lip = ((lx >= 1) & (lx < 3)) | ((ly >= 1) & (ly < 3))
    rivet = np.zeros((N, N), bool); rp = np.zeros((N, N))
    for t in range(4, 64, 8):
        for (cx, cy) in ((t, 5), (5, t)):
            d = np.hypot(lx - cx, ly - cy)
            m = d < 2.0; rivet |= m; rp = np.maximum(rp, np.sqrt(np.clip(1 - (d / 2.0) ** 2, 0, 1)))
    rc, rt = rust_layer(rng)
    halo = smooth(rivet.astype(float) + seam * 0.8, 1.2) * (0.6 + snoise(rng, beta=1.0, fmin=6))
    st = streak_down(np.clip(halo, 0, 1) * 0.8, snoise(rng, beta=0.5, fmin=8, sx=0.15), iters=36)
    rustm = np.clip(np.maximum(halo * 1.4, st * 0.8), 0, 1)
    alb = mix(base, rc, rustm)
    alb = np.where(seam[..., None], fill("#141814"), alb)
    alb = np.where(rivet[..., None], mix(fill("#4a5246"), fill("#6a4c34"), (rp < 0.5).astype(float)), alb)
    h = 1.0 - 1.5 * seam + 0.4 * lip + 1.3 * rp
    rough = 0.65 + 0.3 * rustm
    alb, _ = dirt(rng, alb, 0.25)
    return dict(albedo=alb, height=h, rough=rough, nstr=1.0, colors=32, dither=12)


# ------------------------------------------------------------------ GRATES
def grate_floor_bars(rng):
    bear = (YC % 16) < 3
    cross = (XC % 32) < 2
    alpha = bear | cross
    galv = mix(fill("#80868a"), fill("#a4aaae"), snoise(rng, beta=1.8))
    serr = (XC % 4) < 2
    alb = np.where((bear & serr)[..., None], galv * 1.15, galv)
    alb = np.where(cross[..., None], galv * 0.85, alb)
    rc, _ = rust_layer(rng)
    joint = bear & cross
    rj = smooth(joint.astype(float), 1.5) * 3 * (0.5 + snoise(rng, beta=1.0, fmin=6))
    rp = sstep(0.6, 0.85, snoise(rng, beta=2.0))
    alb = mix(alb, rc, np.clip(rj + 0.8 * rp, 0, 0.9))
    h = np.where(bear, 2.0 + 0.4 * serr, 0) + np.where(cross & ~bear, 1.4, 0)
    rough = 0.6 + 0.25 * np.clip(rj, 0, 1)
    alb, _ = dirt(rng, alb, 0.2)
    return dict(albedo=alb, height=h, rough=rough, alpha=alpha, nstr=1.0, colors=24, dither=10)


def grate_mesh_expanded(rng):
    row = (YC // 16).astype(int)
    u = (XC + 16 * (row % 2)) % 32 - 16
    v = YC % 16 - 8
    e = np.abs(u) / 14.5 + np.abs(v) / 7.0
    hole = e < 1
    alpha = ~hole
    paint = mix(fill("#6e7478"), fill("#8e9498"), snoise(rng, beta=1.8))
    strand_t = np.clip((e - 1) / 0.35, 0, 1)
    wear = sstep(0.55, 0.85, snoise(rng, beta=1.4)) * (strand_t < 0.6)
    alb = mix(paint, fill("#b4b8ba"), wear * 0.8)
    rc, _ = rust_layer(rng)
    alb = mix(alb, rc, sstep(0.7, 0.9, snoise(rng, beta=2.0)) * 0.7)
    h = 1.0 + 0.8 * np.sign(v) * (strand_t < 1) * 0.5
    rough = 0.55 - 0.2 * wear
    return dict(albedo=alb, height=h, rough=rough, alpha=alpha, nstr=1.0, colors=24, dither=10)


def grate_vent_slats(rng):
    v = YC % 16
    slat = v < 12
    prof = np.where(slat, v / 11.0, 0)
    frame = (XC % 64) < 4
    paint = mix(fill("#8a8678"), fill("#a29e8e"), snoise(rng, beta=1.8))
    alb = np.where(slat[..., None], paint, fill("#0e0e0e"))
    dust = (v < 3) & slat
    alb = np.where(dust[..., None], mix(paint, fill("#c0b8a0"), 0.5), alb)
    alb = np.where(frame[..., None], paint * 0.9, alb)
    rc, _ = rust_layer(rng)
    st = streak_down(((frame | (v == 11)) & (snoise(rng, beta=0.5, fmin=10) > 0.75)).astype(float) * 0.7,
                     snoise(rng, beta=0.5, fmin=8, sx=0.15), iters=20)
    alb = mix(alb, rc * 0.9, np.clip(st, 0, 0.7) * (slat | frame))
    h = np.where(slat, 3.0 * prof, -2.0)
    h = np.where(frame, 4.0, h)
    screws = np.zeros((N, N), bool)
    for yy in range(8, N, 32):
        m, p = disc(2, yy, 1.4); screws |= m
        h = np.where(m, 4.6, h)
    alb = np.where(screws[..., None], fill("#4a4840"), alb)
    rough = np.where(slat | frame, 0.7, 1.0)
    alb, _ = dirt(rng, alb, 0.25)
    return dict(albedo=alb, height=h, rough=rough, nstr=0.8, colors=24, dither=10)


TEXTURES = [
    ("pipes_wall_steel", pipes_wall_steel),
    ("pipe_copper_strip", pipe_copper_strip),
    ("pipe_rusty_strip", pipe_rusty_strip),
    ("pipe_corrugated_strip", pipe_corrugated_strip),
    ("pipes_painted_vertical", pipes_painted_vertical),
    ("cables_bundle_strip", cables_bundle_strip),
    ("cable_tray", cable_tray),
    ("cable_braided_strip", cable_braided_strip),
    ("metal_brushed_panels", metal_brushed_panels),
    ("metal_brushed_dark", metal_brushed_dark),
    ("metal_diamond_plate", metal_diamond_plate),
    ("metal_rust_heavy", metal_rust_heavy),
    ("metal_painted_rust_blue", metal_painted_rust_blue),
    ("metal_corrugated_rust", metal_corrugated_rust),
    ("metal_rivet_plates", metal_rivet_plates),
    ("grate_floor_bars", grate_floor_bars),
    ("grate_mesh_expanded", grate_mesh_expanded),
    ("grate_vent_slats", grate_vent_slats),
]
