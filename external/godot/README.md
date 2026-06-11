# godot fork build artifact

The godot 4.5 mono linuxbsd arm64 engine binary used to run the game, plus
its matching GodotSharp.dll.

## Contents

| File | Size | Description |
|---|---|---|
| `godot.linuxbsd.template_release.arm64.mono` | 62 MB | Engine binary, renamed to `godot.mono` on deploy |
| `GodotSharp.dll` | 5.6 MB | The C# bindings it must be paired with (from the same build) |

## Source

- **Repo**: <https://github.com/jenny92-tech/godot>
- **Branch**: `linuxbsd-sdl2`
- **CI Workflow**: `.github/workflows/build-linuxbsd-sdl2-arm64.yml` ("🛠 Build linuxbsd SDL2 arm64 POC")
- **This build**: run [27211008063](https://github.com/jenny92-tech/godot/actions/runs/27211008063) (2026-06-09 13:53 UTC, glibc ≤ 2.31)
- **Artifact**: `godot-linuxbsd-sdl2-mono-arm64-POC`

## Refresh (pull a new CI build)

```bash
# trigger the workflow (manual dispatch)
gh workflow run "🛠 Build linuxbsd SDL2 arm64 POC" -R jenny92-tech/godot --ref linuxbsd-sdl2

# wait ~20 min, then download
gh run list -R jenny92-tech/godot --workflow "🛠 Build linuxbsd SDL2 arm64 POC" --limit 1
RUN=<run-id>
gh run download $RUN -R jenny92-tech/godot --dir /tmp/godot-fresh
cp /tmp/godot-fresh/godot-linuxbsd-sdl2-mono-arm64-POC/__w/godot/godot/bin/godot.linuxbsd.template_release.arm64.mono ./
cp /tmp/godot-fresh/godot-linuxbsd-sdl2-mono-arm64-POC/__w/godot/godot/bin/GodotSharp/Api/Release/GodotSharp.dll ./
```

> ⚠️ **The two files must be paired**: the C# bindings' P/Invoke signatures
> must match the native functions exposed by godot.mono. Mixing them (e.g. an
> old godot.mono with a new GodotSharp.dll) SEGVs immediately.

## Deploy to device

```
godot.mono                                                # renamed from godot.linuxbsd.template_release.arm64.mono
data_sts2_linuxbsd_arm64/GodotSharp.dll                   # used by the game
runtimes/sdl2_fixed/godot.mono                            # source for switch_runtime.sh
runtimes/sdl2_fixed/GodotSharp.dll                        # same
```

## Binaries are NOT distributed in this repo

The engine binary + GodotSharp.dll (62 MB / 5.6 MB) are not committed to git
(size). godot is MIT-licensed — just build your own linuxbsd arm64 mono
binaries per the source above.
