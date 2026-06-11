#!/usr/bin/env bash
# Assemble a distributable launcher pack from committed sources + cached
# external artifacts + downloaded Microsoft .NET 9 runtime.
#
# Output: dist/sts2-linux-launcher/ (mirrors device install layout, minus gamedata/)
#         dist/sts2-linux-launcher-<date>.zip (ready to ship)
#
# Requirements on build host:
#   - dotnet SDK 9.x (for sts2_compat.dll build)
#   - python3 (for bootstrap/overlay pck build)
#   - curl + tar (for .NET runtime download)
#
# Usage:
#   ./scripts/assemble-launcher-pack.sh           # full clean build
#   SKIP_DOTNET_DOWNLOAD=1 ./scripts/...          # use cached runtime if exists

set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT"

DOTNET_VERSION="${DOTNET_VERSION:-9.0.7}"
DOTNET_ARCH="${DOTNET_ARCH:-arm64}"
DOTNET_CACHE="${DOTNET_CACHE:-$ROOT/.cache/dotnet-runtime-$DOTNET_VERSION-linux-$DOTNET_ARCH}"
SKIP_DOTNET_DOWNLOAD="${SKIP_DOTNET_DOWNLOAD:-0}"

DIST="$ROOT/dist/sts2-linux-launcher"
PORT="$DIST/ports/sts2"
DATA="$PORT/data_sts2_linuxbsd_arm64"

red()   { printf "\033[31m%s\033[0m\n" "$*"; }
green() { printf "\033[32m%s\033[0m\n" "$*"; }
blue()  { printf "\033[34m%s\033[0m\n" "$*"; }

blue "=== 1. preflight ==="

require_file() {
    [ -f "$1" ] || { red "missing: $1"; exit 1; }
}
require_dir() {
    [ -d "$1" ] || { red "missing: $1"; exit 1; }
}

require_dir  "$ROOT/external/godot"
require_file "$ROOT/external/godot/godot.linuxbsd.template_release.arm64.mono"
require_file "$ROOT/external/godot/GodotSharp.dll"
require_dir  "$ROOT/external/fmod-gdextension"
require_file "$ROOT/external/fmod-gdextension/libGodotFmod.linux.template_release.arm64.so"
require_dir  "$ROOT/external/spine-runtimes"
require_file "$ROOT/external/spine-runtimes/libspine_godot.linux.template_release.arm64.so"
require_file "$ROOT/linux/data-template/sts2.runtimeconfig.json"
require_file "$ROOT/linux/launcher.sh"
require_file "$ROOT/linux/gamedata-README.md"
require_file "$ROOT/refs/0Harmony.dll"
STEAM_STUB="${STEAM_STUB:-../Bogodroid/tools/steam_mock/libsteam_api64.so}"
[ -f "$STEAM_STUB" ] || { red "missing libsteam_api64.so at $STEAM_STUB (set STEAM_STUB env)"; exit 1; }

green "  all sources present"

blue "=== 2. build launcher artifacts ==="

(cd src/STS2LinuxLauncher && dotnet build -c Release -v:q) || { red "dotnet build failed"; exit 1; }
python3 scripts/make-bootstrap-pck.py
python3 scripts/make-overlay-pck.py

require_file "$ROOT/linux/build/sts2_compat.dll"
require_file "$ROOT/linux/build/bootstrap.pck"
require_file "$ROOT/linux/build/port_compat.pck"
green "  build/ produced 3 artifacts"

blue "=== 3. .NET 9 runtime ($DOTNET_VERSION linux-$DOTNET_ARCH) ==="

if [ ! -d "$DOTNET_CACHE/shared/Microsoft.NETCore.App/$DOTNET_VERSION" ]; then
    if [ "$SKIP_DOTNET_DOWNLOAD" = "1" ]; then
        red "  cache missing + SKIP_DOTNET_DOWNLOAD=1; aborting"
        exit 1
    fi
    mkdir -p "$DOTNET_CACHE"
    URL="https://builds.dotnet.microsoft.com/dotnet/Runtime/$DOTNET_VERSION/dotnet-runtime-$DOTNET_VERSION-linux-$DOTNET_ARCH.tar.gz"
    echo "  downloading $URL"
    curl -fsSL "$URL" | tar -xz -C "$DOTNET_CACHE"
fi

BCL_DIR="$DOTNET_CACHE/shared/Microsoft.NETCore.App/$DOTNET_VERSION"
[ -d "$BCL_DIR" ] || { red "BCL not found at $BCL_DIR"; exit 1; }
BCL_COUNT=$(find "$BCL_DIR" -maxdepth 1 -type f \( -name "*.dll" -o -name "*.so" \) | wc -l | tr -d ' ')
green "  BCL ready: $BCL_COUNT files in $BCL_DIR"

blue "=== 4. assemble dist/ ==="

rm -rf "$DIST"
mkdir -p "$PORT" "$DATA" "$PORT/addons/fmod/libs/linux" \
         "$PORT/addons/spine/linux" "$PORT/addons/sentry" \
         "$PORT/gamedata/data_sts2_linuxbsd_arm64" \
         "$PORT/gamedata/pcks" \
         "$DIST/Roms/PORTS"

# Engine
cp "$ROOT/external/godot/godot.linuxbsd.template_release.arm64.mono" "$PORT/godot.mono"
chmod +x "$PORT/godot.mono"

# Our launcher artifacts
cp "$ROOT/linux/build/bootstrap.pck"            "$PORT/"
cp "$ROOT/linux/build/port_compat.pck"          "$PORT/"
cp "$ROOT/linux/build/sts2_compat.dll"          "$DATA/"
cp "$ROOT/external/godot/GodotSharp.dll"        "$DATA/"
cp "$ROOT/refs/0Harmony.dll"                    "$DATA/"
cp "$ROOT/linux/data-template/sts2.runtimeconfig.json"  "$DATA/"

# Steam stub
cp "$STEAM_STUB" "$PORT/libsteam_api64.so"

# Microsoft .NET 9 runtime (BCL + native interop)
cp "$BCL_DIR"/*.dll "$DATA/"
cp "$BCL_DIR"/*.so  "$DATA/" 2>/dev/null || true

# External addons (CI artifacts)
cp "$ROOT/external/fmod-gdextension"/*.so       "$PORT/addons/fmod/libs/linux/"
cp "$ROOT/external/spine-runtimes"/libspine_godot.linux.template_release.arm64.so \
   "$PORT/addons/spine/linux/"

cat > "$PORT/addons/sentry/SentryStub.gd" <<'EOF'
extends Node
# Stub for missing Sentry GDExtension on arm64 Linux.
# Build pipeline removes the SentryInit autoload + sentry.gdextension
# entries from the pck; this stub is only here for any other res:// reference.
EOF

cat > "$PORT/input_remap.cfg" <<'EOF'
; default input remap; launcher overwrites based on ABXY layout pick
EOF

cp "$ROOT/linux/gamedata-README.md" "$PORT/gamedata/README.md"

cp "$ROOT/linux/launcher.sh" "$DIST/Roms/PORTS/Slay the Spire 2.sh"

green "  layout assembled at $DIST"

blue "=== 5. verify ==="

verify_files=(
    "$PORT/godot.mono"
    "$PORT/bootstrap.pck"
    "$PORT/port_compat.pck"
    "$PORT/libsteam_api64.so"
    "$DATA/sts2_compat.dll"
    "$DATA/GodotSharp.dll"
    "$DATA/0Harmony.dll"
    "$DATA/sts2.runtimeconfig.json"
    "$DATA/System.Private.CoreLib.dll"
    "$PORT/addons/fmod/libs/linux/libGodotFmod.linux.template_release.arm64.so"
    "$PORT/addons/spine/linux/libspine_godot.linux.template_release.arm64.so"
    "$PORT/addons/sentry/SentryStub.gd"
    "$PORT/gamedata/README.md"
    "$DIST/Roms/PORTS/Slay the Spire 2.sh"
)
for f in "${verify_files[@]}"; do
    [ -f "$f" ] || { red "  MISSING: $f"; exit 1; }
done
green "  ${#verify_files[@]}/${#verify_files[@]} key files present"

echo
blue "=== 6. size report ==="
du -sh "$DIST/ports/sts2"/*/ 2>/dev/null | sort -h | tail -10
echo
du -sh "$PORT"
du -sh "$DIST"

ZIP="$ROOT/dist/sts2-linux-launcher-$(date +%Y%m%d).zip"
(cd "$ROOT/dist" && zip -qr "$ZIP" "sts2-linux-launcher")
echo
green "=== ready ==="
green "  layout: $DIST"
green "  zip:    $ZIP ($(du -h "$ZIP" | awk '{print $1}'))"
