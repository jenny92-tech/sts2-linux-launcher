#!/bin/bash
# PORTMASTER: sts2_lite, Slay the Spire 2.sh
# Stage 1: GDScript launcher UI (bootstrap.pck) — quality / language / layout
# Stage 2: game (gamedata/pcks/SlayTheSpire2.pck) — swap + gptokeyb

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

# Logging: .debug marker on SD → full verbose to SD log.txt;
# otherwise quiet: everything goes to RAM tmpfs, only errors/warnings
# are extracted to a small SD log.txt on exit. SLL_DEBUG forwarded to patcher.
ERRLOG="$GAMEDIR/log.txt"
if [ -f "$GAMEDIR/.debug" ]; then
  export SLL_DEBUG=1
  > "$ERRLOG" && exec > >(tee "$ERRLOG") 2>&1
else
  export SLL_DEBUG=0
  RUNLOG="/tmp/sts2_run.log"
  > "$RUNLOG" && exec >> "$RUNLOG" 2>&1
  trap 'grep -iE "error|fail|fatal|exception|abort|segfault|crash|panic|script error|user error|no game pck" "$RUNLOG" 2>/dev/null | tail -n 200 > "$ERRLOG"' EXIT
fi
echo "[STS2] CFW=$CFW_NAME ${DISPLAY_WIDTH}x${DISPLAY_HEIGHT} GAMEDIR=$GAMEDIR debug=$SLL_DEBUG"

# Device-specific lib paths are sourced from PortMaster; we prepend standard
# /usr/lib for libmali/libEGL which PortMaster may not include.
export LD_LIBRARY_PATH="/usr/lib:/usr/lib64:${LD_LIBRARY_PATH}"
export SDL_VIDEODRIVER=dummy
export SDL_AUDIODRIVER=alsa

# ═══════════════ STAGE 1: launcher UI ═══════════════════════════════════
# No gptokeyb during launcher (it EVIOCGRABs event4, blocking godot evdev).
# Hide the game's override.cfg (1280×720 viewport) — launcher runs full-res.
[ -f "$GAMEDIR/override.cfg" ] && mv "$GAMEDIR/override.cfg" "$GAMEDIR/override.cfg.gamehide"
echo "[STS2] stage 1: launcher (bootstrap.pck)"
XDG_CONFIG_HOME="$CONFDIR" XDG_DATA_HOME="$CONFDIR" \
  ./godot.mono --display-driver sdl2 --rendering-driver opengl3 \
  --resolution ${DISPLAY_WIDTH}x${DISPLAY_HEIGHT} \
  --main-pack "$GAMEDIR/bootstrap.pck"
launcher_exit=$?
[ -f "$GAMEDIR/override.cfg.gamehide" ] && mv "$GAMEDIR/override.cfg.gamehide" "$GAMEDIR/override.cfg"
echo "[STS2] launcher exited: $launcher_exit"

if [ "$launcher_exit" != "42" ]; then
  echo "[STS2] not StartGame — quitting."
  pm_finish
  exit 0
fi

# ═══════════════ STAGE 2: game ═══════════════════════════════════════════
# launch_config.env written by stage 1 UI → source into godot.mono's env.
export SLL_LANGUAGE SLL_LAYOUT SLL_QUALITY 2>/dev/null || true
SLL_ENV="$CONFDIR/godot/app_userdata/STS2 Linux Launcher/launch_config.env"
[ -f "$SLL_ENV" ] && source "$SLL_ENV"
case "$SLL_LANGUAGE" in en_US|zh_CN) ;; *) SLL_LANGUAGE=en_US ;; esac

case "$SLL_LANGUAGE" in zh_CN) GAME_LANG=zhs ;; *) GAME_LANG=eng ;; esac
for SF in "$CONFDIR"/SlayTheSpire2/*/*/settings.save; do
  [ -f "$SF" ] && sed -i 's/"language": "[a-z]*"/"language": "'$GAME_LANG'"/' "$SF"
done

# Audio: use existing daemon, start pulseaudio if available, or fall back to ALSA.
export XDG_RUNTIME_DIR=/tmp/xdg-sts2
mkdir -p "$XDG_RUNTIME_DIR" && chmod 700 "$XDG_RUNTIME_DIR"
GODOT_AUDIO_ARG=""
if pgrep -x pulseaudio >/dev/null 2>&1 || pgrep -x pipewire-pulse >/dev/null 2>&1; then
  echo "[STS2] pulse/pipewire daemon already running"
elif command -v pulseaudio >/dev/null 2>&1; then
  pulseaudio --start --exit-idle-time=-1 >/dev/null 2>&1
  sleep 1
  if ! pactl list short sinks 2>/dev/null | grep -qv auto_null; then
    pactl load-module module-alsa-sink device=default tsched=0 >/dev/null 2>&1
    SINK=$(pactl list short sinks 2>/dev/null | grep -v auto_null | head -1 | awk '{print $2}')
    [ -n "$SINK" ] && pactl set-default-sink "$SINK" >/dev/null 2>&1
    echo "[STS2] pulse → ALSA default ($SINK)"
  fi
else
  echo "[STS2] no pulse, godot falls back to ALSA (FMOD music unavailable)"
  GODOT_AUDIO_ARG="--audio-driver ALSA"
fi

# gamedata overlay: cp player's MegaCrit files to runtime data_/.
# -fu: skip copy when mtime matches → zero SD writes after second launch.
GAMEDATA="$GAMEDIR/gamedata"
if [ -d "$GAMEDATA/data_sts2_linuxbsd_arm64" ]; then
  cp -fu "$GAMEDATA/data_sts2_linuxbsd_arm64/"*.dll  "$GAMEDIR/data_sts2_linuxbsd_arm64/" 2>/dev/null
  cp -fu "$GAMEDATA/data_sts2_linuxbsd_arm64/"*.json "$GAMEDIR/data_sts2_linuxbsd_arm64/" 2>/dev/null
fi

GAME_PCK="$GAMEDATA/pcks/SlayTheSpire2.pck"
if [ ! -f "$GAME_PCK" ]; then
  echo "[STS2] no game pck at $GAME_PCK"
  echo "[STS2] Place game files in $GAMEDATA/ (see README), then restart."
  pm_finish
  exit 1
fi

# override.cfg: Mali-required settings + patcher entry redirect.
# Panel size/rotation via GODOT_SDL2 env vars (DRM defaults).
cat > "$GAMEDIR/override.cfg" << 'EOF'
[rendering]
renderer/rendering_method="gl_compatibility"
renderer/rendering_method.mobile="gl_compatibility"

[dotnet]
project/assembly_name="sts2_compat"
EOF

$GPTOKEYB "godot.mono" &
pm_platform_helper "godot.mono"

VERBOSE_ARG=""
[ "$SLL_DEBUG" = "1" ] && VERBOSE_ARG="--verbose"
echo "[STS2] stage 2: lang=$SLL_LANGUAGE quality=$SLL_QUALITY debug=$SLL_DEBUG pck=$GAME_PCK"
LANG="${SLL_LANGUAGE}.UTF-8" \
XDG_CONFIG_HOME="$CONFDIR" XDG_DATA_HOME="$CONFDIR" \
  ./godot.mono $VERBOSE_ARG $GODOT_AUDIO_ARG --display-driver sdl2 --rendering-driver opengl3 \
  --resolution ${DISPLAY_WIDTH}x${DISPLAY_HEIGHT} \
  --main-pack "$GAME_PCK"
echo "[STS2] exit code: $?"

pm_finish
