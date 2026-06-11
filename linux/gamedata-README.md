# Game file setup (gamedata/)

Players must own a legal Steam copy of *Slay the Spire 2*. The launcher
ships no game content.

## Target layout

```
gamedata/
├── README.md
├── data_sts2_linuxbsd_arm64/
│   ├── sts2.dll                              ← game C# main assembly (patched to arm64)
│   ├── sts2.deps.json                        ← dependency graph (linux-arm64 RID)
│   ├── JetBrains.Annotations.dll             ┐
│   ├── MonoMod.Backports.dll                 │
│   ├── MonoMod.ILHelpers.dll                 │
│   ├── Sentry.dll                            │ 12 third-party deps
│   ├── SharpGen.Runtime.dll                  ├ copied directly from Steam
│   ├── SharpGen.Runtime.COM.dll              │ (AnyCPU IL, cross-platform)
│   ├── SmartFormat.dll                       │
│   ├── SmartFormat.ZString.dll               │
│   ├── Steamworks.NET.dll                    │
│   ├── Vortice.DXGI.dll                      │
│   ├── Vortice.DirectX.dll                   │
│   └── Vortice.Mathematics.dll               ┘
└── pcks/
    └── SlayTheSpire2.pck                     ← Linux arm64 repack (~1.2 GB)
```

**15 files + 1 pck**, ~1.2 GB total.

## How the launcher uses gamedata/

- `sts2.runtimeconfig.json` — shipped with the launcher (no player action needed)
- `*.dll` — loaded at runtime by `sts2_compat.dll` via `AssemblyLoadContext.Default.Resolving` (zero copy)
- `sts2.deps.json` (34 KB) — `cp -f` by launcher.sh at startup
- `SlayTheSpire2.pck` — loaded by godot.mono via `--main-pack`

The launcher automatically detects missing files and reports them in the log.

---

## Step 0 · Locate the Steam install

Example paths:
- Windows: `C:\Program Files (x86)\Steam\steamapps\common\Slay The Spire 2`
- macOS:   `~/Library/Application Support/Steam/steamapps/common/Slay The Spire 2`
- Linux:   `~/.steam/steam/steamapps/common/Slay The Spire 2`

We'll call this path `STEAM`:

```bash
STEAM=~/Library/Application\ Support/Steam/steamapps/common/Slay\ The\ Spire\ 2
cd "$STEAM"
ls data_sts2_windows_x86_64/  # should show many .dll files
ls SlayTheSpire2.pck          # should show ~1.2 GB pck
```

---

## Step 1 · Patch sts2.dll PE machine (x86_64 → arm64)

CoreCLR checks the PE machine field. Flip 0x8664 → 0xAA64 (2 bytes only, IL unchanged).

```bash
BD=~/Development/Jenny92Work/Bogodroid
LAUNCHER=~/Development/Jenny92Work/sts2-linux-launcher

python3 "$BD/tools/csharp-godot-arm64-kit/patch_pe_machine.py" \
  data_sts2_windows_x86_64/sts2.dll \
  "$LAUNCHER/gamedata/data_sts2_linuxbsd_arm64/sts2.dll"
```

Verify:
```bash
file "$LAUNCHER/gamedata/data_sts2_linuxbsd_arm64/sts2.dll"
# Should say: "PE32+ executable (DLL) (console) Aarch64, for MS Windows"
```

---

## Step 2 · Rewrite sts2.deps.json RID (win-x64 → linux-arm64)

```bash
python3 "$BD/tools/csharp-godot-arm64-kit/rewrite_deps_json.py" \
  data_sts2_windows_x86_64/sts2.deps.json \
  > "$LAUNCHER/gamedata/data_sts2_linuxbsd_arm64/sts2.deps.json"
```

Verify:
```bash
grep -c "linux-arm64\|linux" "$LAUNCHER/gamedata/data_sts2_linuxbsd_arm64/sts2.deps.json"
# Should be > 10
grep -c "win-x64\|win-x86" "$LAUNCHER/gamedata/data_sts2_linuxbsd_arm64/sts2.deps.json"
# Should be 0
```

---

## Step 3 · Copy 12 third-party .NET deps

All are AnyCPU IL (cross-platform, no patching needed).

```bash
DST="$LAUNCHER/gamedata/data_sts2_linuxbsd_arm64"
SRC="data_sts2_windows_x86_64"

cp "$SRC/JetBrains.Annotations.dll"   "$DST/"
cp "$SRC/MonoMod.Backports.dll"       "$DST/"
cp "$SRC/MonoMod.ILHelpers.dll"       "$DST/"
cp "$SRC/Sentry.dll"                  "$DST/"
cp "$SRC/SharpGen.Runtime.dll"        "$DST/"
cp "$SRC/SharpGen.Runtime.COM.dll"    "$DST/"
cp "$SRC/SmartFormat.dll"             "$DST/"
cp "$SRC/SmartFormat.ZString.dll"     "$DST/"
cp "$SRC/Steamworks.NET.dll"          "$DST/"
cp "$SRC/Vortice.DXGI.dll"            "$DST/"
cp "$SRC/Vortice.DirectX.dll"         "$DST/"
cp "$SRC/Vortice.Mathematics.dll"     "$DST/"
```

Verify:
```bash
ls "$DST"/*.dll | wc -l
# Should = 13 (sts2.dll + 12 deps)
```

---

## Step 4 · pck conversion (~40 min, ~5 GB temp space)

The game pck uses BC7-compressed textures (PC GPUs), which Mali can't decode.
Extract → convert to ASTC 8×8 → apply shader/scene patches → repack.

```bash
"$BD/tools/godot_pck/patches/sts2/build.sh" \
  SlayTheSpire2.pck \
  "$LAUNCHER/gamedata/pcks/SlayTheSpire2.pck"
```

What build.sh does:
1. Extract Windows pck
2. Run 8-stage patch pipeline:
   - Flip to mobile texture preset
   - All textures → ASTC 8×8 (logo/splash excluded)
   - Strip WorldEnvironment node
   - Strip SentryInit autoload + sentry.gdextension
   - Mobile shader replacements
   - Device max texture size caps
3. Repack to arm64 + Linux-compatible pck

Verify:
```bash
ls -lh "$LAUNCHER/gamedata/pcks/SlayTheSpire2.pck"
# Should be ~1.1–1.3 GB
```

---

## Final verification

```bash
cd "$LAUNCHER/gamedata"
echo "--- file list ---"
find . -type f -not -name README.md | sort
echo "--- size ---"
du -sh data_sts2_linuxbsd_arm64 pcks
echo "--- key checks ---"
file data_sts2_linuxbsd_arm64/sts2.dll | grep Aarch64 && echo "✓ sts2.dll arm64"
grep -q linux-arm64 data_sts2_linuxbsd_arm64/sts2.deps.json && echo "✓ deps.json linux-arm64"
ls data_sts2_linuxbsd_arm64/*.dll | wc -l | grep -q 13 && echo "✓ 13 dlls"
[ -f pcks/SlayTheSpire2.pck ] && echo "✓ pck present"
```

Expected:
```
✓ sts2.dll arm64
✓ deps.json linux-arm64
✓ 13 dlls
✓ pck present
```

---

## Common errors

### "no game pck at .../SlayTheSpire2.pck"
Step 4 not finished. Rerun build.sh.

### `System.IO.FileNotFoundException: Could not load file or assembly 'sts2'`
sts2.dll missing or PE not patched. Go back to step 1.

### `System.IO.FileNotFoundException` for other assemblies (Sentry, Steamworks.NET, etc.)
Missing one of the 12 third-party deps. Verify `ls $DST/*.dll | wc -l = 13`.

### `BadImageFormatException`
sts2.dll PE was corrupted. Re-run `patch_pe_machine.py`.

### Black screen after launch, no log
Check that `GAMEDIR="/$directory/ports/sts2"` in launcher.sh matches your SD card layout.

---

## Quick reference

| File | Source | Size | Action |
|---|---|---|---|
| `data_/sts2.dll` | Windows | ~9 MB | PE patch |
| `data_/sts2.deps.json` | Windows | ~35 KB | RID rewrite |
| `data_/` 12 third-party .dlls | Windows | ~4 MB total | Direct copy |
| `pcks/SlayTheSpire2.pck` | Windows | ~1.2 GB | Full build.sh pipeline |

~1.2 GB total. Game needs **1 GB RAM + 1.5 GB swap** (launcher creates swap automatically).

[bd]: https://github.com/jenny92-tech/bogodroid
