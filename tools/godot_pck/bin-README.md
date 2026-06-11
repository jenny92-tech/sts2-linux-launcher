# tools/godot_pck/bin (gitignored)

The pck pipeline needs two macOS apps not stored in this repo:

- **Godot RE Tools** — https://github.com/bruvzg/gdsdecomp/releases
  Used by `extract.sh` to recover the project tree from a stock `.pck`.
- **Godot 4.5 mono** (editor) — https://godotengine.org/download
  Used by `reimport.sh` to regenerate `.import` metadata after texture
  resampling, and to re-pack the modified project.

Place the `.app` bundles (or any other Godot RE Tools / Godot editor
binary that exposes the same CLI) under `tools/godot_pck/bin/`. The
shell scripts auto-detect their location.
