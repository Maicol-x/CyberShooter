#!/usr/bin/env python3
"""Automated seam (tileability) check.

A texture tiles when the wrap-around transition (last column -> first column,
last row -> first row) looks like an ordinary interior transition. For each
texture SET (albedo + normal + roughness stacked into one multi-channel image,
or a single PNG) and for each axis this tool computes:

  A_j = mean |p[j+1] - p[j]|    per channel, for every interior line pair j
  S_j = |mean (p[j+1] - p[j])|  per channel (systematic offset, e.g. uneven lighting)

and the same two values for the wrap pair. Verdict per axis:

  FAIL  the wrap value exceeds TOL x the largest interior value of the same
        channel / statistic (ratio > TOL). No interior transition is that
        strong, so the wrap line is a real seam.
  WARN  the wrap value is a strong robust outlier among interior values
        (z = (wrap - median) / (1.4826 * MAD) > Z_MAX for some channel /
        statistic) AND no interior line pair has a similar difference vector
        (min_j ||d_wrap - d_j|| / ||d_wrap|| > TWIN, vectors smoothed along
        the line with sigma SMOOTH). This is a heuristic: a unique but
        legitimate feature (one pipe rim, one rivet row) lying exactly on the
        wrap line also triggers it, so WARN means "look at the 2x2 tile".
  PASS  otherwise.

Limitations: a misalignment that lands on flat / featureless texels produces
no discontinuity and cannot be detected; statistics cannot tell an intended
hard edge from an accidental one, which is why WARN exists.

    python3 tools/seam_check.py --sets textures        # every texture set in textures/
    python3 tools/seam_check.py some.png other.png      # single files
    python3 tools/seam_check.py --self-test             # negative/positive controls
Exit code 1 if anything fails or a control behaves wrongly.
"""
import sys, os, glob, argparse
import numpy as np
from PIL import Image

Z_MAX = 8.0
TOL = 1.10
TWIN = 0.5
SMOOTH = 2.0


def _prep(arrs):
    chans = []
    for a in arrs:
        a = np.asarray(a).astype(np.float64) / 255.0
        if a.ndim == 2:
            a = a[..., None]
        if a.shape[2] == 4:  # premultiply: colour under alpha 0 is irrelevant
            a = np.concatenate([a[..., :3] * a[..., 3:], a[..., 3:]], -1)
        chans.append(a)
    return np.concatenate(chans, -1)


def _robust_z(vals, w):
    med = np.median(vals, axis=0)
    mad = np.median(np.abs(vals - med), axis=0) * 1.4826 + 1e-3
    return (w - med) / mad


def _axis(img):
    d = np.diff(img, axis=1)                 # H x (W-1) x C
    dw = img[:, 0, :] - img[:, -1, :]        # H x C
    Ai, Aw = np.abs(d).mean(0), np.abs(dw).mean(0)
    Si, Sw = np.abs(d.mean(0)), np.abs(dw.mean(0))
    z = float(max(_robust_z(Ai, Aw).max(), _robust_z(Si, Sw).max()))
    ratio = float(max((Aw / (Ai.max(0) + 1e-3)).max(), (Sw / (Si.max(0) + 1e-3)).max()))
    # twin search on difference vectors smoothed along the line (sigma 2 texels),
    # so per-texel noise does not hide a structural match
    from scipy.ndimage import gaussian_filter1d
    ds = gaussian_filter1d(d, SMOOTH, axis=0, mode="wrap")
    dws = gaussian_filter1d(dw, SMOOTH, axis=0, mode="wrap")
    nw = np.linalg.norm(dws) + 1e-9
    twin = float(min(np.linalg.norm(dws - ds[:, j, :]) for j in range(d.shape[1])) / nw)
    status = "FAIL" if ratio > TOL else ("WARN" if (z > Z_MAX and twin > TWIN) else "PASS")
    return dict(status=status, z=z, twin=twin, ratio=ratio)


def check_arrays(arrs):
    img = _prep(arrs)
    r = {"x": _axis(img), "y": _axis(img.transpose(1, 0, 2))}
    st = {r["x"]["status"], r["y"]["status"]}
    r["status"] = "FAIL" if "FAIL" in st else ("WARN" if "WARN" in st else "PASS")
    r["pass"] = r["status"] == "PASS"
    return r


def texture_set(folder):
    name = os.path.basename(os.path.normpath(folder))
    files = [os.path.join(folder, f"{name}_{k}.png") for k in ("albedo", "normal_gl", "roughness")]
    return name, [np.asarray(Image.open(f)) for f in files]


def _fmt(name, r):
    return (f"{r['status']}  {name:<34} "
            f"x: ratio={r['x']['ratio']:.2f} z={r['x']['z']:6.1f} twin={r['x']['twin']:.2f}   "
            f"y: ratio={r['y']['ratio']:.2f} z={r['y']['z']:6.1f} twin={r['y']['twin']:.2f}")


def self_test(tex_root):
    """Negative controls MUST fail, positive controls MUST pass."""
    from scipy import ndimage
    sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
    from psxlib import snoise, rng_for
    rng = np.random.default_rng(7)
    big = ndimage.gaussian_filter(rng.standard_normal((256, 256)), 6, mode="reflect")
    nontile = (255 * (big[40:168, 70:198] - big.min()) / np.ptp(big)).astype(np.uint8)
    per = (255 * snoise(rng_for("selftest", 1), beta=2.0)).astype(np.uint8)
    _, rust = texture_set(os.path.join(tex_root, "metal_rust_heavy"))
    _, pipes = texture_set(os.path.join(tex_root, "pipes_wall_steel"))
    _, panels = texture_set(os.path.join(tex_root, "metal_brushed_panels"))
    ramp = np.linspace(0.85, 1.15, 128)[None, :, None]
    lit = [np.clip(rust[0].astype(float) * ramp, 0, 255).astype(np.uint8)] + rust[1:]
    cases = [
        ("NEG smooth noise crop (non-periodic)", [nontile], {"FAIL"}),
        ("NEG rust set, albedo x lighting ramp 0.85-1.15", lit, {"FAIL"}),
        ("NEG rust set, cropped 8 px left/right", [m[:, 8:120] for m in rust], {"FAIL"}),
        ("NEG pipe wall set, top 20 rows removed", [m[20:] for m in pipes], {"FAIL", "WARN"}),
        ("NEG panel set, cropped to 120x120", [m[4:124, 4:124] for m in panels], {"FAIL", "WARN"}),
        ("POS periodic spectral noise", [per], {"PASS"}),
        ("POS rust set as shipped", rust, {"PASS"}),
        ("POS pipe wall set as shipped", pipes, {"PASS"}),
        ("POS panel set rolled by (37, 53)", [np.roll(m, (37, 53), (0, 1)) for m in panels], {"PASS"}),
    ]
    ok = True
    for name, arrs, expect in cases:
        r = check_arrays(arrs)
        good = r["status"] in expect
        ok &= good
        print(("ok     " if good else "WRONG  ") + _fmt(name, r))
    print("SELF-TEST", "OK" if ok else "FAILED")
    return ok


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("files", nargs="*")
    ap.add_argument("--sets", default=None, help="folder containing one sub-folder per texture")
    ap.add_argument("--self-test", action="store_true")
    ap.add_argument("--strict", action="store_true", help="treat WARN as failure")
    a = ap.parse_args()
    here = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "textures")
    ok = True
    if a.self_test:
        ok &= self_test(a.sets or here)
    results = []
    if a.sets:
        for d in sorted(glob.glob(os.path.join(a.sets, "*", ""))):
            name, arrs = texture_set(d)
            results.append((name + " (set)", check_arrays(arrs)))
    for f in a.files:
        results.append((os.path.basename(f), check_arrays([np.asarray(Image.open(f))])))
    for name, r in results:
        print(_fmt(name, r))
    if results:
        cnt = {k: sum(r["status"] == k for _, r in results) for k in ("PASS", "WARN", "FAIL")}
        print(f"{cnt['PASS']}/{len(results)} PASS, {cnt['WARN']} WARN, {cnt['FAIL']} FAIL")
        ok &= cnt["FAIL"] == 0 and (cnt["WARN"] == 0 or not a.strict)
    sys.exit(0 if ok else 1)


if __name__ == "__main__":
    main()
