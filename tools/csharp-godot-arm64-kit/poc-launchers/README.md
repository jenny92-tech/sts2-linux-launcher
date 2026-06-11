# POC launcher presets

godot 4.5 + SDL2 POC on-device test launcher suite. **One godot binary, 21 presets, zero rebuilds.**

## Deployment

```bash
# 1. Ensure godot binary is in place on device
ls /mnt/SDCARD/Data/ports/poc_sdl2/godot

# 2. Push shim_egl.so (diagnostic hook, see ../shim_egl/)
scp ../shim_egl/shim_egl.so root@device:/mnt/SDCARD/Data/ports/poc_sdl2/

# 3. Push all launchers to PORTS/
scp '[POC]'*.sh root@device:/mnt/sdcard/mmcblk1p1/Roms/PORTS/

# 4. Clear PortMaster cache so new launchers appear
ssh root@device 'rm /mnt/sdcard/mmcblk1p1/Roms/PORTS/PORTS_cache*.db'
```

## Preset categories

### 01–12: `POC_*` env config toggles (binary-internal behaviour)
| | Env set | Purpose |
|---|---|---|
| 01-default | (none) | Baseline |
| 02-no_master | POC_DRM_MASTER=0 | Test whether Mali needs to grab master |
| 03-argb_a8 | POC_GBM_FORMAT=ARGB + POC_EGL_ALPHA=8 | Format compatibility |
| 04-gbm_linear | POC_GBM_LINEAR=1 | Disable modifier |
| 05-no_plat_ext | POC_EGL_PLATFORM_EXT=0 | eglGetDisplay instead of EXT |
| 06-no_depth | POC_EGL_DEPTH=0 | Disable depth buffer |
| 07-gles2 | POC_EGL_GLES_VER=2 | Legacy ES 2.0 context |
| 08-skip_pageflip | POC_SKIP_PAGE_FLIP=1 | Skip page flip |
| 09-skip_makecurrent | POC_SKIP_MAKE_CURRENT=1 | Skip eglMakeCurrent |
| 10-skip_surface | POC_SKIP_EGL_SURFACE=1 | Skip surface creation |
| 11-skip_egl | POC_SKIP_EGL=1 | Skip entire EGL block |
| 12-skip_kms | POC_SKIP_KMS=1 | Skip entire KMS block |

### 20–28: LD_PRELOAD shim_egl.so blacklist (external GL probe hooking)
| | EGL_SHIM_BLACKLIST | Purpose |
|---|---|---|
| 20-shim | (empty) | Hook only, log all queries — see last query before SIGSEGV |
| 21-block_compute | compute/memory_barrier family | Ban GLES 3.1 compute |
| 22-block_multiview | OVR multiview family | Ban OVR extensions |
| 23-block_khr_debug | debug message/object label full set | Ban KHR_debug |
| 24-block_storage3d_multi | TexStorage2/3DMultisample | Ban multisample storage |
| 25-block_aggressive | All 4 families above | Ban common trouble spots at once |
| 26-shim_full_strace | (no blacklist + full strace) | Most detailed forensic |
| 27-block_31_compute | Explicit 3.1 compute/indirect/multisample | Conservative version |
| 28-block_all_31_32 | All 78 GLES 3.1+ functions | Most aggressive |

## Output files (4 per preset)

```
$GAMEDIR/<preset>.log      Full stdout/stderr (tee'd)
$GAMEDIR/<preset>.diag     syscall-safe diagnostics from poc_diag
$GAMEDIR/<preset>.shim     Hook log from shim_egl.so
$GAMEDIR/<preset>.strace   Syscall log from strace
```

Bisect workflow:
1. Run `[POC]20-shim` → `cat <preset>.shim | tail -20` to see the last query before SIGSEGV
2. Add that function name to `EGL_SHIM_BLACKLIST` and re-run
3. If it dies on another function, add that too — repeat until godot finishes startup (you should see `[POC-DIAG] DSDL2: CONSTRUCTOR COMPLETE` followed by godot's `Godot Engine v4.5...` banner)
