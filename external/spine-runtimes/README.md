# spine-runtimes build artifact

Godot GDExtension bindings for Spine 2D skeletal animation (template_release).

## Contents

| File | Size | Description |
|---|---|---|
| `libspine_godot.linux.template_release.arm64.so` | 4.4 MB | Spine ↔ Godot bridge (GDExtension) |

## Source

- **Repo**: <https://github.com/jenny92-tech/spine-runtimes>
- **Upstream**: EsotericSoftware/spine-runtimes
- **Branch**: `4.2`
- **CI Workflow**: "🛠 Build spine-godot extension linux arm64"
- **This build**: run [26935186871](https://github.com/jenny92-tech/spine-runtimes/actions/runs/26935186871) (2026-06-04 06:34 UTC, glibc 2.31)
- **Artifact**: `spine-godot-linux-arm64-glibc2.31`

## Refresh

```bash
gh workflow run "🛠 Build spine-godot extension linux arm64" -R jenny92-tech/spine-runtimes --ref 4.2
# wait ~13 min
RUN=$(gh run list -R jenny92-tech/spine-runtimes --workflow "🛠 Build spine-godot extension linux arm64" --limit 1 --json databaseId --jq '.[0].databaseId')
gh run download $RUN -R jenny92-tech/spine-runtimes -n spine-godot-linux-arm64-glibc2.31 --dir .
```

## License

The [Spine Runtime License](https://esotericsoftware.com/spine-runtimes-license)
requires every end user to hold a **valid Spine Editor License** to use the
runtime legally (any version). StS2 players typically hold this indirectly
through the game itself (covered by MegaCrit's agreement).

This `.so` is built from the open-source spine-runtimes (the runtime part is
open; only the editor is paid), but it is **still bound by Esoteric
Software's EULA** — commercial redistribution requires explicit notice to
Esoteric.

## Deploy to device

```
addons/spine/linux/libspine_godot.linux.template_release.arm64.so
```

## ⚠️ Binaries are NOT distributed in this repo

The `.so` is not committed to git. Spine Runtimes are governed by Esoteric
Software's proprietary [Spine Runtime License](https://esotericsoftware.com/spine-runtimes-license),
which requires a valid Spine license. Build your own ARM64 binary per the
source above.
