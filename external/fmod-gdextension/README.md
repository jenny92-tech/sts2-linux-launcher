# fmod-gdextension build artifact

Godot GDExtension bindings for the FMOD audio engine, plus the FMOD runtime
libraries.

## Contents

| File | Size | Description |
|---|---|---|
| `libGodotFmod.linux.template_release.arm64.so` | 3.4 MB | FMOD ↔ Godot bridge (GDExtension) |
| `libfmod.so` + `libfmod.so.14` + `libfmod.so.14.6` | ~1.3 MB ×3 | FMOD core runtime (release) |
| `libfmodL.so` + `libfmodL.so.14` + `libfmodL.so.14.6` | ~1.5 MB ×3 | FMOD core runtime (debug, 'L' = Logging) |
| `libfmodstudio.so` + `.so.14` + `.so.14.6` | ~1.3 MB ×3 | FMOD Studio runtime (release) |
| `libfmodstudioL.so` + `.so.14` + `.so.14.6` | ~2.1 MB ×3 | FMOD Studio runtime (debug) |

`.so.14` is a symlink to `.so`; `.so.14.6` is the real file. When packaging,
**dereference** (`cp -L`) so Linux exFAT can read them.

## Source

- **Repo**: <https://github.com/jenny92-tech/fmod-gdextension>
- **Upstream**: utopia-rise/fmod-gdextension
- **Branch**: `master`
- **CI Workflow**: "🛠 Build linux arm64"
- **This build**: run [26936848302](https://github.com/jenny92-tech/fmod-gdextension/actions/runs/26936848302) (2026-06-04 07:14 UTC, glibc 2.31)
- **Artifact**: `libGodotFmod-linux-arm64-glibc2.31-template_release`

## Refresh

```bash
gh workflow run "🛠 Build linux arm64" -R jenny92-tech/fmod-gdextension --ref master
# wait ~5 min
RUN=$(gh run list -R jenny92-tech/fmod-gdextension --workflow "🛠 Build linux arm64" --limit 1 --json databaseId --jq '.[0].databaseId')
gh run download $RUN -R jenny92-tech/fmod-gdextension -n libGodotFmod-linux-arm64-glibc2.31-template_release --dir .
```

## License

FMOD is a commercial audio engine: **personal use is fine, commercial
redistribution is not**. The FMOD runtime binaries come from the official
FMOD download and are governed by the [FMOD EULA](https://www.fmod.com/licensing).

Our fork is only the Godot binding layer, but the `fmod` / `fmodstudio`
`.so` files **must not** be bundled into a public launcher package — players
must download them from FMOD themselves (or be guided to install them after
the launcher is installed).

## Deploy to device

```
addons/fmod/libs/linux/libGodotFmod.linux.template_release.arm64.so
addons/fmod/libs/linux/libfmod.so          (+.so.14 +.so.14.6)
addons/fmod/libs/linux/libfmodstudio.so    (+.so.14 +.so.14.6)
# debug builds (libfmodL / libfmodstudioL) are dev-only, not needed to deploy
```

## ⚠️ Binaries are NOT distributed in this repo

The `.so` files are not committed to git. FMOD is a closed-source commercial
engine; its runtime libraries (`libfmod*` / `libfmodstudio*`) are governed by
the [FMOD EULA](https://www.fmod.com/licensing) and may **NOT** be
redistributed. The `libGodotFmod` bridge must be compiled yourself. Build
your own ARM64 binaries — per the source above and under your own FMOD
license — and place them in this directory.
