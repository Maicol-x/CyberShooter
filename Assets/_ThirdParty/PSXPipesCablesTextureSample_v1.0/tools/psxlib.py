"""Shared helpers for the PSX Pipes & Cables Texture Sample generator.

Everything here is plain numpy / scipy / Pillow math. All noise is built to be
periodic on a 128x128 torus so every texture tiles without seams.
"""
import hashlib
import os
import numpy as np
from PIL import Image
from scipy import ndimage

N = 128
_Y, _X = np.mgrid[0:N, 0:N].astype(np.float64)
# PSX_COORD_OFFSET="i,j" shifts the coordinate grid by (i*N, j*N) texels. A
# correctly periodic generator must give the same images (used by
# tools/periodicity_test.py to catch pattern periods that do not divide 128).
_OX, _OY = (int(v) * N for v in os.environ.get("PSX_COORD_OFFSET", "0,0").split(","))
XC = _X + 0.5 + _OX  # pixel-centre coordinates
YC = _Y + 0.5 + _OY

BAYER4 = (np.array([[0, 8, 2, 10],
                    [12, 4, 14, 6],
                    [3, 11, 1, 9],
                    [15, 7, 13, 5]], dtype=np.float64) + 0.5) / 16.0 - 0.5
BAYER = np.tile(BAYER4, (N // 4, N // 4))


def rng_for(name, seed):
    """Independent, stable RNG per texture (name + master seed)."""
    h = int(hashlib.sha256(f"{seed}:{name}".encode()).hexdigest()[:16], 16)
    return np.random.default_rng(h)


def norm01(a):
    lo, hi = a.min(), a.max()
    return (a - lo) / (hi - lo) if hi > lo else np.zeros_like(a)


def wrapd(a, c, n=N):
    """Signed shortest distance on a ring of length n."""
    return (a - c + n / 2.0) % n - n / 2.0


def snoise(rng, beta=2.0, fmin=1.0, fmax=None, sx=1.0, sy=1.0, n=N):
    """Periodic spectral (1/f^beta) noise in 0..1. sx/sy > 1 squash that axis'
    frequencies, giving streaks along that axis (used for brushed metal)."""
    w = rng.standard_normal((n, n))
    F = np.fft.fft2(w)
    k1 = np.fft.fftfreq(n) * n
    KX, KY = np.meshgrid(k1, k1)
    k = np.sqrt((KX * sx) ** 2 + (KY * sy) ** 2)
    amp = np.zeros_like(k)
    nz = k > 0
    amp[nz] = k[nz] ** (-beta / 2.0)
    if fmin:
        amp[k < fmin] = 0
    if fmax:
        amp[k > fmax] = 0
    return norm01(np.real(np.fft.ifft2(F * amp)))


def worley(rng, npts, n=N, jitter_pts=None):
    """Periodic Worley noise: returns (F1, F2, nearest-id)."""
    pts = rng.random((npts, 2)) * n if jitter_pts is None else jitter_pts
    f1 = np.full((n, n), 1e9)
    f2 = np.full((n, n), 1e9)
    idx = np.zeros((n, n), dtype=np.int64)
    for i, (px, py) in enumerate(pts):
        d = np.hypot(wrapd(XC, px), wrapd(YC, py))
        closer = d < f1
        f2 = np.where(closer, f1, np.minimum(f2, d))
        idx = np.where(closer, i, idx)
        f1 = np.where(closer, d, f1)
    return f1, f2, idx


def smooth(a, s):
    return ndimage.gaussian_filter(a, s, mode="wrap")


def sstep(e0, e1, x):
    t = np.clip((x - e0) / (e1 - e0), 0, 1)
    return t * t * (3 - 2 * t)


def hexc(h):
    h = h.lstrip("#")
    return np.array([int(h[i:i + 2], 16) / 255.0 for i in (0, 2, 4)])


def fill(c):
    return np.broadcast_to(hexc(c), (N, N, 3)).copy()


def mix(a, b, t):
    t = np.asarray(t)
    if t.ndim == 2:
        t = t[..., None]
    return a * (1 - t) + b * t


def ramp(t, stops):
    """Colour ramp: stops = [(pos, '#hex'), ...] sorted by pos."""
    t = np.clip(t, 0, 1)
    pos = np.array([p for p, _ in stops])
    cols = np.array([hexc(c) for _, c in stops])
    out = np.empty(t.shape + (3,))
    for ch in range(3):
        out[..., ch] = np.interp(t, pos, cols[:, ch])
    return out


def draw_line(mask, x0, y0, x1, y1, val=1.0, width=1, n=N):
    """Crisp texel line with wrap-around (no anti-aliasing, PSX style)."""
    L = max(abs(x1 - x0), abs(y1 - y0))
    steps = int(L * 2) + 2
    xs = np.linspace(x0, x1, steps)
    ys = np.linspace(y0, y1, steps)
    for ox in range(width):
        for oy in range(width):
            mask[(np.floor(ys).astype(int) + oy) % n,
                 (np.floor(xs).astype(int) + ox) % n] = val
    return mask


def streak_down(src, length_noise, base=0.86, spread=0.12, iters=48):
    """Drip / leak streaks running downward (+row) from a source mask. Periodic."""
    decay = base + spread * length_noise  # per-pixel decay
    acc = src.copy()
    for _ in range(iters):
        acc = np.maximum(acc, np.roll(acc, 1, axis=0) * decay)
    return acc


def hpipe(yc, r):
    """Horizontal cylinder. r may be a scalar or a (1,N) per-column array.
    Returns mask, height profile 0..1, signed offset d, arc coordinate s."""
    d = wrapd(YC, yc)
    rr = np.broadcast_to(np.asarray(r, dtype=float), (N, N))
    t = np.clip(d / rr, -1, 1)
    prof = np.sqrt(np.clip(1 - t * t, 0, 1))
    mask = np.abs(d) < rr
    s = np.arcsin(t) * rr
    return mask, prof, d, s


def vpipe(xc, r):
    m, p, d, s = hpipe_T(xc, r)
    return m, p, d, s


def hpipe_T(xc, r):
    d = wrapd(XC, xc)
    rr = np.broadcast_to(np.asarray(r, dtype=float), (N, N))
    t = np.clip(d / rr, -1, 1)
    prof = np.sqrt(np.clip(1 - t * t, 0, 1))
    mask = np.abs(d) < rr
    s = np.arcsin(t) * rr
    return mask, prof, d, s


def band_x(xc, half):
    return np.abs(wrapd(XC, xc)) < half


def band_y(yc, half):
    return np.abs(wrapd(YC, yc)) < half


def disc(xc, yc, r):
    d = np.hypot(wrapd(XC, xc), wrapd(YC, yc))
    return d < r, np.sqrt(np.clip(1 - (d / r) ** 2, 0, 1))


# ---------------------------------------------------------------- encoding
def _to5bit(v):
    v5 = np.round(np.asarray(v, dtype=np.float64) / 255.0 * 31.0).astype(int)
    return (v5 << 3) | (v5 >> 2)


def psx_quantize(rgb, colors=32, dither=14.0, mask=None):
    """Limited-palette (CLUT-like) quantisation with a 15-bit palette and an
    ordered 4x4 Bayer dither. The dither matrix tiles 32x into 128 px, so the
    result stays seamless. Returns uint8 RGB."""
    u = np.clip(rgb * 255.0, 0, 255)
    sel = np.ones(u.shape[:2], bool) if mask is None else mask
    pix = np.round(u[sel]).astype(np.uint8).reshape(1, -1, 3)
    q = Image.fromarray(pix).quantize(colors=colors, method=Image.Quantize.MEDIANCUT,
                                             dither=Image.Dither.NONE)
    used = sorted(set(np.asarray(q).ravel().tolist()))
    pal = np.array(q.getpalette()[:3 * 256]).reshape(-1, 3)[used]
    pal = np.unique(_to5bit(pal), axis=0).astype(np.float64)
    t = u + BAYER[..., None] * dither
    d = ((t[:, :, None, :] - pal[None, None, :, :]) ** 2).sum(-1)
    out = pal[np.argmin(d, axis=-1)]
    return out.astype(np.uint8)


def quantize_gray(a, levels=16, dither=0.6):
    v = np.clip(a, 0, 1) * (levels - 1) + BAYER * dither
    v = np.clip(np.round(v), 0, levels - 1) / (levels - 1)
    return np.round(v * 255).astype(np.uint8)


def height_to_normal(h, strength=1.0, gl=True):
    """Height (pixel units) -> tangent-space normal, wrap-around differences.
    OpenGL convention (+Y up, green = up) when gl=True; DirectX flips green."""
    dx = (np.roll(h, -1, axis=1) - np.roll(h, 1, axis=1)) * 0.5 * strength
    drow = (np.roll(h, -1, axis=0) - np.roll(h, 1, axis=0)) * 0.5 * strength
    nx, ny, nz = -dx, drow, np.ones_like(h)  # ny: +Y up in texture space
    if not gl:
        ny = -ny
    l = np.sqrt(nx * nx + ny * ny + nz * nz)
    n = np.stack([nx / l, ny / l, nz / l], -1)
    return np.round((n * 0.5 + 0.5) * 255).astype(np.uint8)


def bleed_rgb(rgb_u8, opaque, wrap=True):
    """Copy the nearest opaque texel colour into transparent texels so no
    filtering / mip level can pull in a dark or white halo."""
    if opaque.all() or not opaque.any():
        return rgb_u8
    if wrap:
        big = np.tile(~opaque, (3, 3))
        _, (iy, ix) = ndimage.distance_transform_edt(big, return_indices=True)
        n0, n1 = opaque.shape
        iy = iy[n0:2 * n0, n1:2 * n1] % n0
        ix = ix[n0:2 * n0, n1:2 * n1] % n1
    else:
        _, (iy, ix) = ndimage.distance_transform_edt(~opaque, return_indices=True)
    return rgb_u8[iy, ix]


def save_png(path, arr):
    Image.fromarray(arr).save(path, format="PNG", optimize=False, compress_level=9)
