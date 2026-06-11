# STS2 Linux Launcher

*[English](README.md)*

A launcher and runtime compatibility layer for running *Slay the Spire 2* on
ARM Linux handhelds (TrimUI Smart Pro, MiniLoong Pocket One, similar
PortMaster-class devices). Built for 1 GB Mali GPUs where the stock PC build
OOMs immediately.

> The launcher does not ship game content. Players must own a legal Steam
> copy and provide the game files themselves; see
> [`linux/gamedata-README.md`](linux/gamedata-README.md) for the player-side
> recipe.

## What's in this repo

- `src/STS2LinuxLauncher/` — Harmony patcher (`sts2_compat.dll`) injected
  into the game's .NET runtime at boot. Each `Patches/*.cs` is a single
  hook with a short header comment describing what it fixes.
- `linux/launcher_ui.gd` — GDScript launcher UI (background art, language
  picker, button-layout picker, preload-mode picker, start button).
- `linux/launcher.sh` — PortMaster two-stage launch script.
- `linux/data-template/sts2.runtimeconfig.json` — generic .NET 9 self-contained
  config shipped in the launcher pack.
- `linux/assets/` — bundled launcher background image and CJK font subset.
- `scripts/` — pck builders, dist-pack assembler, device deploy helper.
- `external/` — pinned CI build artifacts of the three forks listed below
  (kept in LFS).

## External forks

| Fork | Branch | Purpose |
|---|---|---|
| [`jenny92-tech/godot`][gh-godot] | `linuxbsd-sdl2` | Godot 4.5 mono with a KMSDRM+SDL2 display server backend for PortMaster devices |
| [`jenny92-tech/fmod-gdextension`][gh-fmod] | `master` | FMOD Studio bindings for the audio layer |
| [`jenny92-tech/spine-runtimes`][gh-spine] | `4.2` | Spine 4.2 GDExtension for character animation |

[gh-godot]: https://github.com/jenny92-tech/godot
[gh-fmod]: https://github.com/jenny92-tech/fmod-gdextension
[gh-spine]: https://github.com/jenny92-tech/spine-runtimes

Each fork's CI workflow uploads ARM64 build artifacts; the current pinned
artifacts live under `external/<name>/`, with each `README.md` recording the
upstream workflow run and refresh command.

## Build

Requires .NET 9 SDK and Python 3.10+.

```sh
# Patcher dll → linux/build/sts2_compat.dll
(cd src/STS2LinuxLauncher && dotnet build -c Release)

# Launcher UI pck → linux/build/bootstrap.pck
python3 scripts/make-bootstrap-pck.py

# Shader-overlay pck → linux/build/port_compat.pck
python3 scripts/make-overlay-pck.py
```

The patcher build references the game's `sts2.dll` and `0Harmony.dll`; place
them in `refs/` (gitignored) before building. See
`linux/gamedata-README.md` for how to obtain them.

## Distribute

`scripts/assemble-launcher-pack.sh` produces a redistributable launcher pack
(`dist/sts2-linux-launcher-<date>.zip`) by:

1. Verifying source artifacts are present.
2. Building the three pck/dll outputs above.
3. Downloading the Microsoft .NET 9 runtime (cached to `.cache/`).
4. Composing the on-device directory layout under `dist/`.
5. Zipping the result.

`scripts/MANIFEST.md` documents every file in the pack, categorized by
licence and origin. The pack contains no MegaCrit content — players provide
that separately via the `gamedata/` directory.

## Deploy (dev iteration)

`scripts/deploy-to-device.sh` rsyncs the freshly-built patcher, pcks, and
`launcher.sh` to a device over SSH. Use environment variables to override
the target:

```sh
DEVICE=root@<ip> PORT_PATH=/path/to/sts2 ./scripts/deploy-to-device.sh
```

## Acknowledgements

- [ModinMobileSTS/Sts2MobileLauncher][modin] — three pieces here draw on
  its work: writing the game's default settings before the process starts
  (rather than switching at runtime), the lazy asset-loading approach, and
  the shader compatibility set.
- [Harmony](https://github.com/pardeike/Harmony) by Andreas Pardeike — the
  runtime patching framework.
- [LXGW WenKai Lite](https://github.com/lxgw/LxgwWenkai-Lite) — the CJK
  font used in the launcher UI (subsetted, SIL OFL 1.1).
- [PortMaster](https://portmaster.games/) — the handheld port distribution
  framework this launcher targets.

[modin]: https://github.com/ModinMobileSTS/Sts2MobileLauncher

## License

[CC BY-NC-SA 4.0][cc-by-nc-sa] — see [`LICENSE`](LICENSE). Original code,
assets, and documentation in this repository may be shared and adapted for
non-commercial purposes with attribution and the same license. Derivatives
must remain non-commercial and credit both this project and
[ModinMobileSTS/Sts2MobileLauncher][modin].

This license grants no rights to *Slay the Spire 2* itself.
Redistribution of MegaCrit game files (including `sts2.dll`,
`SlayTheSpire2.pck`, and the third-party .NET dependencies the game
ships) is not authorised by this license and is explicitly prohibited
by the notice in `LICENSE`. Players must own a legal copy of the game.

[cc-by-nc-sa]: https://creativecommons.org/licenses/by-nc-sa/4.0/
