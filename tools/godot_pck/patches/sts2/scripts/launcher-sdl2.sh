#!/bin/bash
# PORTMASTER: sts2, Slay the Spire 2 — SDL2 backend variant.
# godot 4.5 mono with KMS/GBM/EGL, bypassing weston/crusty.

XDG_DATA_HOME=${XDG_DATA_HOME:-$HOME/.local/share}
if [ -d "/opt/system/Tools/PortMaster/" ]; then controlfolder="/opt/system/Tools/PortMaster"
elif [ -d "/opt/tools/PortMaster/" ]; then controlfolder="/opt/tools/PortMaster"
elif [ -d "$XDG_DATA_HOME/PortMaster/" ]; then controlfolder="$XDG_DATA_HOME/PortMaster"
else controlfolder="/roms/ports/PortMaster"
fi
source $controlfolder/control.txt
[ -f "${controlfolder}/mod_${CFW_NAME}.txt" ] && source "${controlfolder}/mod_${CFW_NAME}.txt"
get_controls

GAMEDIR="/$directory/ports/sts2"
CONFDIR="$GAMEDIR/conf"
mkdir -p "$CONFDIR"
cd "$GAMEDIR"
> "$GAMEDIR/log.txt" && exec > >(tee "$GAMEDIR/log.txt") 2>&1
echo "[STS2-SDL2] CFW=$CFW_NAME ${DISPLAY_WIDTH}x${DISPLAY_HEIGHT} GAMEDIR=$GAMEDIR"

# malloc tuning for 1 GB devices
export MALLOC_ARENA_MAX=2
export MALLOC_TRIM_THRESHOLD_=131072
export MALLOC_MMAP_THRESHOLD_=131072

# 1.5 GB swap — Mali can't decode BPTC/S3TC, so CPU decompresses to raw RGBA.
SWAP_FILE="/mnt/SDCARD/.sts2_swap"
if ! swapon -s 2>/dev/null | grep -q "$SWAP_FILE"; then
  [ ! -f "$SWAP_FILE" ] && dd if=/dev/zero of="$SWAP_FILE" bs=1M count=1536 status=none && mkswap "$SWAP_FILE" >/dev/null
  $ESUDO swapon "$SWAP_FILE" 2>&1
  $ESUDO sysctl -w vm.swappiness=80 >/dev/null 2>&1
fi
echo "[STS2-SDL2] swap: $(swapon -s | tail -1)"

export SDL_VIDEODRIVER=dummy
export SDL_AUDIODRIVER=alsa

export DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1

# Purge weston/crusty path pollution from control.txt / mod_TrimUI.txt
export LD_LIBRARY_PATH="/usr/lib:/usr/lib64:/usr/trimui/lib:/mnt/SDCARD/System/lib"
echo "[STS2-SDL2] LD_LIBRARY_PATH=$LD_LIBRARY_PATH"

$GPTOKEYB "godot.mono" &
pm_platform_helper "godot.mono"

echo "[STS2-SDL2] launching..."
XDG_CONFIG_HOME="$CONFDIR" XDG_DATA_HOME="$CONFDIR" \
  ./godot.mono --verbose --display-driver sdl2 --rendering-driver opengl3 \
  --resolution 1280x720 --main-pack "$GAMEDIR/SlayTheSpire2.astc.pck"
echo "[STS2-SDL2] exit code: $?"

pm_finish
