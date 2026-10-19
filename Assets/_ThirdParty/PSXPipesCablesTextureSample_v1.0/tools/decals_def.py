"""8 decals (128x128, straight alpha). Not tileable: alpha is forced to 0 at
the border. Each returns rgb (float), alpha (float 0..1), rough (float)."""
import numpy as np
from psxlib import N, XC, YC, snoise, smooth, sstep, fill, mix, ramp, draw_line, streak_down
from textures_def import RUST

CX = XC - N / 2
CY = YC - N / 2
R = np.hypot(CX, CY)
BORDER = sstep(0, 6, np.minimum.reduce([XC, YC, N - XC, N - YC]))  # 0 at edges


def blob(rng, radius, wobble=0.35, beta=1.8):
    n = snoise(rng, beta=beta)
    return R / radius + wobble * (n - 0.5) * 2


def decal_oil_stain(rng):
    b = blob(rng, 44, 0.4, beta=2.8)
    a = sstep(1.05, 0.6, b) * 0.85
    ring = sstep(0.18, 0.0, np.abs(b - 0.92)) * 0.3
    sat = (snoise(rng, beta=0.6, fmin=14) > 0.8) & (b < 1.3) & (b > 0.9)
    a = np.clip(a + ring + 0.5 * sat, 0, 0.95)
    rgb = mix(fill("#14100a"), fill("#2a2016"), snoise(rng, beta=1.4))
    rgb = mix(rgb, fill("#0a0806"), ring / 0.3)
    return rgb, a, np.full((N, N), 0.25)


def decal_crack(rng):
    m = np.zeros((N, N))
    def walk(x, y, ang, length, w, depth):
        for _ in range(int(length)):
            ang += rng.normal(0, 0.35)
            nx, ny = x + 2 * np.cos(ang), y + 2 * np.sin(ang)
            if not (6 < nx < N - 6 and 6 < ny < N - 6):
                return
            draw_line(m, x, y, nx, ny, 1, width=w, n=N)
            x, y = nx, ny
            if depth < 3 and rng.random() < 0.09:
                walk(x, y, ang + rng.choice([-1, 1]) * rng.uniform(0.5, 1.2), length * 0.5, 1, depth + 1)
    base = rng.uniform(0, 2 * np.pi)
    for k in range(4):
        walk(64, 64, base + k * np.pi / 2 + rng.normal(0, 0.3), 32, 2 if k < 2 else 1, 0)
    chip = smooth(m, 1.0) > 0.18
    a = np.where(m > 0, 1.0, np.where(chip, 0.55, 0.0))
    rgb = np.where((m > 0)[..., None], fill("#0c0b0a"), fill("#3a3632"))
    return rgb, a, np.full((N, N), 0.9)


def decal_grime(rng):
    n = snoise(rng, beta=1.6)
    f = snoise(rng, beta=0.7, fmin=10)
    b = R / 56 + 0.6 * (n - 0.5)
    a = sstep(1.0, 0.4, b) * (0.45 + 0.5 * f)
    rgb = mix(fill("#2a2418"), fill("#4a3e2a"), f)
    return rgb, np.clip(a, 0, 0.9), np.full((N, N), 0.95)


def decal_rust_leak(rng):
    src = np.zeros((N, N))
    hole = np.hypot(CX, YC - 18) < 4.5
    for x in np.arange(34, 94, 2.0):
        if rng.random() < 0.75:
            w = 1 + int(rng.random() < 0.4)
            src[16 + int(rng.integers(-2, 4)), int(x):int(x) + w] = rng.uniform(0.5, 1.0) * (1 - abs(x - 64) / 40)
    src = np.maximum(src, smooth(hole.astype(float), 2.5) * 1.5)
    ln = snoise(rng, beta=0.4, fmin=6, sx=0.15)
    st = streak_down(np.clip(src, 0, 1), ln, base=0.95, spread=0.045, iters=110)
    st = np.where(YC < 12, 0, st)
    a = np.clip(st * 1.1, 0, 1) * sstep(126, 90, YC)
    t = np.clip(0.3 + 0.6 * st + 0.2 * snoise(rng, beta=1.0), 0, 1)
    rgb = ramp(t, RUST)
    rgb = np.where(hole[..., None], fill("#140a04"), rgb)
    a = np.where(hole, 1.0, a)
    return rgb, a, np.full((N, N), 0.9)


def decal_hazard_stripes(rng):
    band = np.abs(CY) < 22
    stripe = (((XC + YC) // 12) % 2) == 0
    rgb = np.where(stripe[..., None], fill("#d4a414"), fill("#141414"))
    wear = snoise(rng, beta=1.4)
    fine = snoise(rng, beta=0.6, fmin=12)
    keep = (0.7 * wear + 0.3 * fine) > 0.32
    edge_ragged = np.abs(CY) < 22 - 3 * (fine > 0.6)
    rgb = mix(rgb, rgb * 0.7, sstep(0.5, 0.8, snoise(rng, beta=2.0)) * 0.6)
    a = (band & keep & edge_ragged & (np.abs(CX) < 60)).astype(float)
    return rgb, a, np.full((N, N), 0.55)


def decal_puddle(rng):
    b = blob(rng, 50, 0.45, beta=3.0)
    a = sstep(1.0, 0.85, b) * 0.75
    edge = sstep(0.12, 0.0, np.abs(b - 0.97))
    a = np.clip(a + 0.25 * edge, 0, 0.85)
    rgb = mix(fill("#10141a"), fill("#1c232a"), snoise(rng, beta=1.0))
    rgb = mix(rgb, fill("#3a3226"), edge)
    return rgb, a, np.full((N, N), 0.05)


def decal_scorch(rng):
    b = blob(rng, 50, 0.5)
    rays = np.cos(np.arctan2(CY, CX) * 11 + 3 * snoise(rng, beta=1.5)) * 0.08
    b = b + rays
    a = sstep(1.05, 0.25, b)
    rgb = ramp(np.clip(1 - b, 0, 1), [(0, "#3a2a1a"), (0.3, "#1a1410"), (0.6, "#080706"), (1, "#050404")])
    spk = (snoise(rng, beta=0.4, fmin=16) > 0.78) & (b < 1.4)
    a = np.where(spk, np.maximum(a, 0.8), a)
    return rgb, np.clip(a, 0, 0.97), np.full((N, N), 0.95)


def decal_debris(rng):
    a = np.zeros((N, N)); rgb = fill("#4a4438")
    cols = ["#5a5448", "#3a352c", "#6a6458", "#2a2620", "#7a6a50", "#4a3a2a"]
    for i in range(70):
        r = rng.gamma(1.6, 1.3) + 0.6
        ang = rng.uniform(0, 2 * np.pi); dist = abs(rng.normal(0, 26))
        x, y = 64 + dist * np.cos(ang), 64 + dist * np.sin(ang)
        if not (8 < x < N - 8 and 8 < y < N - 8):
            continue
        th = np.arctan2(YC - y, XC - x)
        rr = r * (0.85 + 0.1 * np.cos(rng.integers(2, 4) * th + rng.uniform(0, 6))
                  + 0.06 * np.cos(5 * th + rng.uniform(0, 6)))
        m = np.hypot(XC - x, YC - y) < rr
        rgb = np.where(m[..., None], fill(cols[i % len(cols)]) * rng.uniform(0.8, 1.15), rgb)
        a = np.where(m, 1.0, a)
    dust = sstep(1.0, 0.3, R / 50 + 0.5 * (snoise(rng, beta=1.4) - 0.5)) * 0.45
    a = np.maximum(a, dust * (snoise(rng, beta=0.5, fmin=14) > 0.45))
    return rgb, a, np.full((N, N), 0.9)


DECALS = [
    ("decal_oil_stain", decal_oil_stain),
    ("decal_crack", decal_crack),
    ("decal_grime", decal_grime),
    ("decal_rust_leak", decal_rust_leak),
    ("decal_hazard_stripes", decal_hazard_stripes),
    ("decal_puddle", decal_puddle),
    ("decal_scorch", decal_scorch),
    ("decal_debris", decal_debris),
]
