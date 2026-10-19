#!/usr/bin/env python3
"""Regenerates every texture and decal into a temporary folder with the
default seed and byte-compares them with the shipped PNGs.

    python3 tools/verify_reproducible.py
Exit code 0 = all files byte-identical.
"""
import os, sys, glob, hashlib, subprocess, tempfile, shutil

HERE = os.path.dirname(os.path.abspath(__file__))
KIT = os.path.abspath(os.path.join(HERE, ".."))


def sha(p):
    with open(p, "rb") as f:
        return hashlib.sha256(f.read()).hexdigest()


def main():
    tmp = tempfile.mkdtemp(prefix="psx_regen_")
    try:
        subprocess.run([sys.executable, os.path.join(HERE, "gen_textures.py"), "--out", tmp],
                       check=True, capture_output=True)
        shipped = sorted(glob.glob(os.path.join(KIT, "textures", "*", "*.png")) +
                         glob.glob(os.path.join(KIT, "decals", "*.png")))
        regen = sorted(glob.glob(os.path.join(tmp, "textures", "*", "*.png")) +
                       glob.glob(os.path.join(tmp, "decals", "*.png")))
        rel_s = {os.path.relpath(p, KIT) for p in shipped}
        rel_r = {os.path.relpath(p, tmp) for p in regen}
        bad = sorted(rel_s ^ rel_r)
        same = 0
        for rel in sorted(rel_s & rel_r):
            if sha(os.path.join(KIT, rel)) == sha(os.path.join(tmp, rel)):
                same += 1
            else:
                bad.append(rel)
        for b in bad:
            print("DIFFERENT / MISSING:", b)
        print(f"reproducible: {same}/{len(rel_s | rel_r)} files byte-identical")
        import numpy, scipy, PIL
        print(f"python {sys.version.split()[0]}  numpy {numpy.__version__}  scipy {scipy.__version__}  Pillow {PIL.__version__}")
        return 0 if not bad else 1
    finally:
        shutil.rmtree(tmp, ignore_errors=True)


if __name__ == "__main__":
    sys.exit(main())
