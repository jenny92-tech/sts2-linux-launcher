#!/usr/bin/env bash
# Push freshly built artifacts to device over SSH.
# Run assemble-launcher-pack.sh for a full pack rebuild.
#
# Usage:
#   ./scripts/deploy-to-device.sh
#   DEVICE=root@10.10.1.193 ./scripts/deploy-to-device.sh
#   PORT_PATH=/mnt/sdcard/.../ports/sts2 ./scripts/deploy-to-device.sh

set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT"

DEVICE="${DEVICE:-root@10.10.1.193}"
PORT_PATH="${PORT_PATH:-/mnt/sdcard/mmcblk1p1/Data/ports/sts2}"
PORTMASTER_PATH="${PORTMASTER_PATH:-/mnt/sdcard/mmcblk1p1/Roms/PORTS}"
LAUNCHER_NAME="${LAUNCHER_NAME:-Slay the Spire 2.sh}"

blue()  { printf "\033[34m%s\033[0m\n" "$*"; }
green() { printf "\033[32m%s\033[0m\n" "$*"; }

# Verify build artifacts exist
for f in linux/build/sts2_compat.dll linux/build/bootstrap.pck linux/build/port_compat.pck; do
    [ -f "$f" ] || { echo "missing $f — run 'dotnet build' + scripts/make-*-pck.py first"; exit 1; }
done

blue "=== ssh $DEVICE ==="
ssh -o ConnectTimeout=5 "$DEVICE" 'echo alive' >/dev/null

blue "=== push 4 artifacts ==="
scp linux/build/sts2_compat.dll  "$DEVICE:$PORT_PATH/data_sts2_linuxbsd_arm64/sts2_compat.dll"
scp linux/build/bootstrap.pck    "$DEVICE:$PORT_PATH/bootstrap.pck"
scp linux/build/port_compat.pck  "$DEVICE:$PORT_PATH/port_compat.pck"
scp linux/data-template/sts2.runtimeconfig.json \
                                  "$DEVICE:$PORT_PATH/data_sts2_linuxbsd_arm64/sts2.runtimeconfig.json"

blue "=== push launcher.sh ==="
scp linux/launcher.sh "$DEVICE:$PORTMASTER_PATH/$LAUNCHER_NAME"

green "=== done ==="
echo "next: launch on device, then ./scripts/pull-log.sh to inspect"
