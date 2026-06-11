# shim_egl — godot GL startup hook

LD_PRELOAD shim to bisect "which GL function causes Mali NULL deref" on closed-source libmali.

## Build (Mac M-series / Linux)

```bash
zig cc -target aarch64-linux-gnu -shared -fPIC -O2 -Wl,--strip-all shim_egl.c -o shim_egl.so
```

Output ~5.8 KB, GLIBC 2.17 only, links libc + libdl. Works on any arm64 PortMaster handheld.

## Usage

```bash
LD_PRELOAD=/path/to/shim_egl.so \
EGL_SHIM_LOG_FILE=/tmp/shim.log \
EGL_SHIM_BLACKLIST="glDispatchCompute:glObjectLabel" \
./godot ...
```

- `EGL_SHIM_LOG_FILE` (optional): also write hook log to this file (stderr fallback)
- `EGL_SHIM_BLACKLIST` (optional, colon-separated): return NULL for these GL function names via `eglGetProcAddress`, causing godot's glad to skip them → no Mali bug trigger

## Hooked functions

- `eglGetProcAddress(name)` — godot queries GL function pointers; logs name + return value
- `eglQueryString(display, name)` — godot queries EGL extensions/version/vendor; logs returned string
- `glGetString(name)` — godot RasterizerGLES3 first batch, queries VENDOR/RENDERER/VERSION/EXTENSIONS
- `glGetIntegerv(pname, params)` — godot Config constructor, queries MAX_TEXTURE_SIZE etc.

## Bisect workflow

1. Run godot + shim → inspect last few lines of shim output
2. The last hook before SIGSEGV = the function causing Mali NULL deref
3. Add it to `EGL_SHIM_BLACKLIST` → re-run
4. Dies on another? Add it → iterate until godot finishes startup
