#!/usr/bin/env python3
"""Exact periodicity test for the generator (complements seam_check.py).

Re-runs gen_textures.py with the coordinate grid shifted by whole multiples of
128 texels (env PSX_COORD_OFFSET). Every pattern in a correctly tileable
generator is 128-periodic, so the PNGs must be (nearly) identical: only rare
1-level rounding flips from floating point are tolerated. A stripe or dot
pattern whose period does not divide 128 changes visibly and fails.

    python3 tools/periodicity_test.py             # all textures
    python3 tools/periodicity_test.py --inject    # also prove it catches a bad period
"""
import os, sys, glob, subprocess, tempfile, argparse, shutil
import numpy as np
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
OFFSETS = ["1,0", "0,1", "3,-2"]
MAX_FLIP_FRAC = 0.002   # at most 0.2 % of texels may differ ...
MAX_FLIP_LEVEL = 2      # ... and by at most 2 levels (out of 255)


def gen(tools_dir, out, offset, only=None):
    env = dict(os.environ, PSX_COORD_OFFSET=offset)
    cmd = [sys.executable, os.path.join(tools_dir, "gen_textures.py"), "--out", out]
    if only:
        cmd += ["--only", only]
    subprocess.run(cmd, check=True, env=env, capture_output=True)


def compare(a_dir, b_dir):
    res = {}
    for fa in sorted(glob.glob(os.path.join(a_dir, "textures", "*", "*.png"))):
        rel = os.path.relpath(fa, a_dir)
        a = np.asarray(Image.open(fa)).astype(int)
        b = np.asarray(Image.open(os.path.join(b_dir, rel))).astype(int)
        d = np.abs(a - b)
        frac = float((d.reshape(d.shape[0], d.shape[1], -1).max(-1) > 0).mean())
        res[rel] = (frac, int(d.max()))
    return res


def run(tools_dir, only=None, verbose=True):
    tmp = tempfile.mkdtemp(prefix="psx_period_")
    try:
        gen(tools_dir, os.path.join(tmp, "base"), "0,0", only)
        worst = {}
        for off in OFFSETS:
            gen(tools_dir, os.path.join(tmp, off), off, only)
            for rel, (frac, mx) in compare(os.path.join(tmp, "base"), os.path.join(tmp, off)).items():
                w = worst.get(rel, (0.0, 0, ""))
                if (frac, mx) > w[:2]:
                    worst[rel] = (frac, mx, off)
                else:
                    worst.setdefault(rel, (frac, mx, off))
        bad = 0
        for rel, (frac, mx, off) in sorted(worst.items()):
            ok = frac <= MAX_FLIP_FRAC and mx <= MAX_FLIP_LEVEL
            bad += not ok
            if verbose and (not ok or frac > 0):
                print(f"{'ok  ' if ok else 'FAIL'} {rel}: {frac * 100:.3f}% texels differ, max {mx} (offset {off})")
        print(f"periodicity: {len(worst) - bad}/{len(worst)} maps identical within tolerance "
              f"(offsets {', '.join(OFFSETS)})")
        return bad == 0
    finally:
        shutil.rmtree(tmp, ignore_errors=True)


def inject():
    """Copy the tools, give one stripe pattern a period of 21 texels, expect FAIL."""
    tmp = tempfile.mkdtemp(prefix="psx_inject_")
    try:
        t = os.path.join(tmp, "tools")
        shutil.copytree(HERE, t, ignore=shutil.ignore_patterns("__pycache__"))
        p = os.path.join(t, "textures_def.py")
        s = open(p).read()
        assert "mk = (((XC // 2) % 8) < 3)" in s
        s = s.replace("mk = (((XC // 2) % 8) < 3)", "mk = (((XC // 3) % 7) < 3)")
        open(p, "w").write(s)
        print("-- injected: cable print marks with a 21-texel period (does not divide 128)")
        caught = not run(t, only="cables_bundle_strip")
        print("-- injection", "CAUGHT" if caught else "MISSED")
        return caught
    finally:
        shutil.rmtree(tmp, ignore_errors=True)


if __name__ == "__main__":
    ap = argparse.ArgumentParser()
    ap.add_argument("--inject", action="store_true")
    a = ap.parse_args()
    ok = run(HERE)
    if a.inject:
        ok &= inject()
    sys.exit(0 if ok else 1)
