#!/usr/bin/env python3
"""
Rewrite a self-contained .NET app's deps.json for a different RID
(e.g. win-x64 → linux-arm64).

Replaces RID in runtimeTarget.name, targets keys, runtimepack keys,
and libraries keys. Rebuilds the 'native' runtimepack mapping from
*.so files actually present in the data folder (excluding hostfxr /
hostpolicy, which go through the host chain).

Usage:
    python3 rewrite_deps_json.py <data_folder> [--old win-x64] [--new linux-arm64]
Backs up <app>.deps.json.<old>.bak.
"""
import sys, os, json, glob, shutil, argparse


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("data_folder", help="folder containing *.deps.json, target-arch .so files, and IL assemblies")
    ap.add_argument("--old", default="win-x64")
    ap.add_argument("--new", default="linux-arm64")
    a = ap.parse_args()
    OLD, NEW, ddir = a.old, a.new, a.data_folder

    deps_files = glob.glob(os.path.join(ddir, "*.deps.json"))
    if len(deps_files) != 1:
        print("ABORT: expected exactly 1 *.deps.json, found:", deps_files); sys.exit(1)
    deps = deps_files[0]
    d = json.load(open(deps))

    host_excl = {"libhostfxr.so", "libhostpolicy.so"}
    have_so = sorted(f for f in os.listdir(ddir) if f.endswith(".so") and f not in host_excl)

    d["runtimeTarget"]["name"] = d["runtimeTarget"]["name"].replace(OLD, NEW)

    targets = d["targets"]
    for k in list(targets):
        if OLD in k:
            targets[k.replace(OLD, NEW)] = targets.pop(k)
    for tval in targets.values():
        for pk in list(tval):
            entry = tval[pk]
            if "native" in entry:
                entry["native"] = {so: {"fileVersion": "0.0.0.0"} for so in have_so}
            npk = pk.replace(OLD, NEW)
            if npk != pk:
                tval[npk] = tval.pop(pk)

    libs = d["libraries"]
    for k in list(libs):
        if OLD in k:
            libs[k.replace(OLD, NEW)] = libs.pop(k)

    shutil.copy(deps, deps + "." + OLD + ".bak")
    json.dump(d, open(deps, "w"), indent=2)
    print("Rewrote %s: %s → %s | %d native libs" % (os.path.basename(deps), OLD, NEW, len(have_so)))
    print("Note: if a game library ships its own win-native deps (not in runtimepack), verify manually.")


if __name__ == "__main__":
    main()
