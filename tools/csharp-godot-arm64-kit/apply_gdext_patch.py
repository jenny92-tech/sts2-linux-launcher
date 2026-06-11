#!/usr/bin/env python3
"""
In-place patch of a Godot .pck's .gdextension file to add linux.arm64 entries.
Updates only the gdextension blob + its directory MD5.

Idempotent, with original-content MD5 verification.

Usage:
    python3 apply_gdext_patch.py <pck> [patch_dir]
"""
import sys, os, json, hashlib

if not (2 <= len(sys.argv) <= 3):
    print(__doc__); sys.exit(2)
pck = sys.argv[1]
here = os.path.dirname(os.path.abspath(__file__))
patch_dir = sys.argv[2] if len(sys.argv) == 3 else os.path.join(here, "examples")

meta = json.load(open(os.path.join(patch_dir, "patch_meta.json")))
blob = open(os.path.join(patch_dir, "fmod.gdextension.patched"), "rb").read()
assert len(blob) == meta["gdext_size"], "blob size mismatch"

with open(pck, "r+b") as f:
    f.seek(meta["gdext_ofs"])
    cur = f.read(meta["gdext_size"])
    cur_md5 = hashlib.md5(cur).hexdigest()
    if cur_md5 == meta["new_md5"]:
        print("already patched, nothing to do"); sys.exit(0)
    if cur_md5 != meta["orig_md5"]:
        print("ABORT: bytes at gdext offset have md5 %s, expected original %s.\n"
              "Wrong pck or different build — not touching it." % (cur_md5, meta["orig_md5"]))
        sys.exit(1)
    f.seek(meta["gdext_ofs"]); f.write(blob)
    f.seek(meta["md5_pos"]);   f.write(bytes.fromhex(meta["new_md5"]))
    print("patched OK: fmod.gdextension now lists linux.release/debug/editor.arm64")
