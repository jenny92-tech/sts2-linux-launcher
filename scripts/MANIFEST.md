# Launcher Pack Manifest

This is the file inventory shipped in a stock launcher pack
(`dist/sts2-linux-launcher-<date>.zip`). Categorized by source and
license posture so anyone can verify what's distributable.

The pack assembles via `./scripts/assemble-launcher-pack.sh`.

## Layout

```
sts2-linux-launcher/
├── Roms/
│   └── PORTS/
│       └── Slay the Spire 2.sh              [our] launcher.sh
└── ports/
    └── sts2/
        ├── godot.mono                          [our fork CI]
        ├── bootstrap.pck                       [our build]
        ├── port_compat.pck                     [our build]
        ├── libsteam_api64.so                   [our stub]
        ├── input_remap.cfg                     [our template]
        ├── data_sts2_linuxbsd_arm64/
        │   ├── sts2_compat.dll                 [our build]
        │   ├── sts2.runtimeconfig.json         [our template]
        │   ├── GodotSharp.dll                  [our fork CI]
        │   ├── 0Harmony.dll                    [NuGet MIT]
        │   ├── System.Private.CoreLib.dll      [Microsoft .NET 9 BCL]
        │   ├── ... (~100 System.*.dll + *.so)  [Microsoft .NET 9 BCL]
        │   ├── Microsoft.CSharp.dll            [Microsoft .NET 9 BCL]
        │   ├── ... (~5 Microsoft.*.dll)        [Microsoft .NET 9 BCL]
        │   ├── mscorlib.dll / netstandard.dll  [Microsoft .NET 9 BCL]
        │   ├── WindowsBase.dll                 [Microsoft .NET 9 BCL]
        │   └── System.IO.Hashing.dll           [NuGet MIT, ships with .NET 9]
        ├── addons/
        │   ├── fmod/libs/linux/
        │   │   └── libGodotFmod.linux.template_release.arm64.so  [our fork CI]
        │   ├── spine/linux/
        │   │   └── libspine_godot.linux.template_release.arm64.so  [our fork CI]
        │   └── sentry/
        │       └── SentryStub.gd               [our 3-line stub]
        └── gamedata/
            └── README.md                       [our tutorial; user fills the dir]
```

## Source / license breakdown

| Origin | License | Count | Size | Notes |
|---|---|---|---|---|
| **Our source code build** | MIT (this repo) | 4 | ~5.7 MB | sts2_compat.dll, bootstrap.pck, port_compat.pck, launcher.sh |
| **Our forks CI artifacts** | godot=MIT, fmod fork=Utopia/MIT, spine fork=Esoteric EULA | 4 | ~76 MB | godot.mono, GodotSharp.dll, libGodotFmod, libspine_godot |
| **Our stubs** | MIT (this repo) | 4 | ~165 KB | libsteam_api64.so, SentryStub.gd, runtimeconfig.json, input_remap.cfg |
| **NuGet / Microsoft public** | MIT/Apache | 110+ | ~85 MB | 0Harmony.dll, Microsoft .NET 9 BCL + native interop |
| **Total launcher pack** |  | ~120 | **~170 MB** | (zipped: ~50 MB) |

## What's NOT in the pack (player provides via gamedata/)

| File | License | Source |
|---|---|---|
| `sts2.dll` | MegaCrit IP | Steam Windows install, PE-patched to arm64 |
| `sts2.deps.json` | factual data | Steam Windows install, RID-rewritten |
| 12 third-party .NET dependencies | open source NuGet | Steam Windows install, direct copy (AnyCPU) |
| `SlayTheSpire2.pck` | MegaCrit IP + arm64 patches | Steam Windows install, processed via build.sh pipeline |
| FMOD runtime `.so` files (`libfmod.so`, `libfmodstudio.so`, etc.) | FMOD EULA | FMOD official download (not redistributable) |

Player follows `gamedata/README.md` to produce these.

## License posture

The launcher pack contains:
- **No MegaCrit code or assets** — game files stay in player's gamedata/
- **No FMOD runtime libraries** — player provides via FMOD's official download
- Only MIT / Apache / our-own code

This makes the launcher pack itself freely redistributable. Game compatibility
depends on player having a legal Steam copy.

## Verification

After `./scripts/assemble-launcher-pack.sh` produces the zip:

```bash
unzip -l dist/sts2-linux-launcher-*.zip | tail -1
# Expected: ~170 MB uncompressed, ~120 files
```

For the running install on a device:

```bash
ssh root@<device> 'find /path/to/ports/sts2 -type f | wc -l'
# Expected after player adds gamedata/: ~135 files (launcher 120 + gamedata 15)
```
