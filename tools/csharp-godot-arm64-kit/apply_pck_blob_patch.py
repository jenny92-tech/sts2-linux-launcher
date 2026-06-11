#!/usr/bin/env python3
"""
In-place patch of a Godot .pck: replaces one file with same-length content
(updates only the blob + its 16-byte md5 in the directory).

Idempotent, with original-content MD5 verification.

Usage:
    python3 apply_pck_blob_patch.py <pck> <meta.json> <blob_file>
    python3 apply_pck_blob_patch.py <pck>  # defaults to fmod patch
"""
import sys, os, json, hashlib

here = os.path.dirname(os.path.abspath(__file__))
if len(sys.argv) == 2:
    pck = sys.argv[1]
    meta_path = os.path.join(here, "examples", "patch_meta.json")
    blob_path = os.path.join(here, "examples", "fmod.gdextension.patched")
elif len(sys.argv) == 4:
    pck, meta_path, blob_path = sys.argv[1], sys.argv[2], sys.argv[3]
else:
    print(__doc__); sys.exit(2)

meta = json.load(open(meta_path))
blob = open(blob_path, "rb").read()
ofs = meta.get("blob_ofs",  meta.get("gdext_ofs"))
sz  = meta.get("blob_size", meta.get("gdext_size"))
assert ofs is not None and sz is not None, "meta missing blob_ofs/blob_size"
assert len(blob) == sz, f"blob size {len(blob)} != metadata {sz}"

with open(pck, "r+b") as f:
    f.seek(ofs)
    cur = f.read(sz)
    cur_md5 = hashlib.md5(cur).hexdigest()
    if cur_md5 == meta["new_md5"]:
        print(f"already patched at @{ofs}, nothing to do"); sys.exit(0)
    if cur_md5 != meta["orig_md5"]:
        print(f"ABORT: bytes at @{ofs} have md5 {cur_md5}, expected original {meta['orig_md5']}.\n"
              f"Wrong pck or different build — not touching it."); sys.exit(1)
    f.seek(ofs);              f.write(blob)
    f.seek(meta["md5_pos"]);  f.write(bytes.fromhex(meta["new_md5"]))
    print(f"patched OK @{ofs} ({sz}B). {meta.get('description','')}")
