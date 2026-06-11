using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using HarmonyLib;

namespace STS2LinuxLauncher.Patches;

/// Swaps stock blur/distortion/overlay shaders for Mali-friendly versions.
/// 1. Loads port_compat.pck via ProjectSettings.LoadResourcePack.
/// 2. Node.AddChild postfix sweeps subtrees, swapping ShaderMaterial.shader
///    paths that match the override map.
public static class ShaderCompatibilityPatches
{
    private const string OverlayPackFileName = "port_compat.pck";

    private static readonly Dictionary<string, string> ShaderOverrides = new(StringComparer.Ordinal)
    {
        { "res://shaders/dark_blur.gdshader",                                              "res://shaders/mobile_compat/dark_blur_compat.gdshader" },
        { "res://shaders/radial_blur.gdshader",                                            "res://shaders/mobile_compat/radial_blur_compat.gdshader" },
        { "res://shaders/doom_overlay.gdshader",                                           "res://shaders/mobile_compat/doom_overlay_compat.gdshader" },
        { "res://shaders/vfx/distortion/vfx_screen_distortion_outward_shader.gdshader",    "res://shaders/mobile_compat/screen_distortion_compat.gdshader" },
        { "res://shaders/vfx/scream/vfx_scream_distortion_polar_shader.gdshader",          "res://shaders/mobile_compat/scream_distortion_compat.gdshader" },
        { "res://shaders/vfx/vfx_water_reflection_post.gdshader",                          "res://shaders/mobile_compat/water_reflection_post_compat.gdshader" },
        { "res://shaders/vfx/the_insatiable_sand_fall_2.gdshader",                         "res://shaders/mobile_compat/sand_fall_post_compat.gdshader" },
        { "res://shaders/overlay_blend.gdshader",                                          "res://shaders/mobile_compat/overlay_blend_compat.gdshader" },
        { "res://shaders/wiggle.gdshader",                                                 "res://shaders/mobile_compat/wiggle_compat.gdshader" },
        // canvas_group_mask_blur intentionally not swapped — mobile replacement renders solid white.
    };

    private static readonly Dictionary<string, object> _replacementCache = new(StringComparer.Ordinal);

    private static bool _overlayLoaded;
    private static MethodInfo _projectSettingsLoadResourcePack;
    private static MethodInfo _resourceLoaderLoad;

    public static void Apply(Harmony harmony)
    {
        var godotSharp = ResolveGameGodotSharp();
        if (godotSharp == null)
        {
            Console.Error.WriteLine("[STS2 Linux Compat] ShaderCompat: GodotSharp not found in AppDomain");
            return;
        }

        var psType = godotSharp.GetType("Godot.ProjectSettings");
        var rlType = godotSharp.GetType("Godot.ResourceLoader");
        if (psType == null || rlType == null)
        {
            Console.Error.WriteLine("[STS2 Linux Compat] ShaderCompat: ProjectSettings/ResourceLoader types missing");
            return;
        }

        _projectSettingsLoadResourcePack = psType.GetMethod(
            "LoadResourcePack",
            BindingFlags.Public | BindingFlags.Static,
            null,
            new[] { typeof(string), typeof(bool), typeof(int) },
            null);
        if (_projectSettingsLoadResourcePack == null)
        {
            _projectSettingsLoadResourcePack = rlType.Assembly.GetType("Godot.ProjectSettings")
                .GetMethod("LoadResourcePack", BindingFlags.Public | BindingFlags.Static);
        }
        if (_projectSettingsLoadResourcePack == null)
        {
            Console.Error.WriteLine("[STS2 Linux Compat] ShaderCompat: ProjectSettings.LoadResourcePack not found");
            return;
        }

        foreach (var m in rlType.GetMethods(BindingFlags.Public | BindingFlags.Static))
        {
            if (m.Name != "Load" || m.IsGenericMethod || m.IsGenericMethodDefinition) continue;
            var ps = m.GetParameters();
            if (ps.Length == 0 || ps[0].ParameterType != typeof(string)) continue;
            _resourceLoaderLoad = m;
            break;
        }
        if (_resourceLoaderLoad == null)
        {
            Console.Error.WriteLine("[STS2 Linux Compat] ShaderCompat: ResourceLoader.Load not found");
            return;
        }

        LoadOverlayPack();

        var nodeType = godotSharp.GetType("Godot.Node");
        if (nodeType == null)
        {
            Console.Error.WriteLine("[STS2 Linux Compat] ShaderCompat: Godot.Node type not found");
            return;
        }

        int patched = 0;
        foreach (var m in nodeType.GetMethods(BindingFlags.Public | BindingFlags.Instance))
        {
            if (m.Name != "AddChild" || m.IsGenericMethod) continue;
            try
            {
                harmony.Patch(m, postfix: new HarmonyMethod(typeof(ShaderCompatibilityPatches), nameof(NodeAddChildPostfix)));
                patched++;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[STS2 Linux Compat] ShaderCompat: failed to patch one AddChild overload: {ex.Message}");
            }
        }

        Console.Error.WriteLine($"[STS2 Linux Compat] ShaderCompatibilityPatches installed (overlay={_overlayLoaded}, AddChild overloads patched={patched})");
    }

    private static Assembly ResolveGameGodotSharp()
    {
        // Walk NGame's base-class chain to find the game's GodotSharp instance
        // (ours is in a separate load context and would SEGV).
        var ngame = AccessTools.TypeByName("MegaCrit.Sts2.Core.Nodes.NGame");
        if (ngame == null) return null;
        var t = ngame.BaseType;
        while (t != null && t != typeof(object))
        {
            if (t.Assembly.GetName().Name == "GodotSharp")
                return t.Assembly;
            t = t.BaseType;
        }
        return null;
    }

    private static void LoadOverlayPack()
    {
        try
        {
            var pckPath = System.IO.Path.Combine(PortPaths.GameDir, "port_compat.pck");
            if (!File.Exists(pckPath))
            {
                Console.Error.WriteLine($"[STS2 Linux Compat] ShaderCompat: overlay pck missing at {pckPath}");
                return;
            }
            var args = _projectSettingsLoadResourcePack.GetParameters().Length switch
            {
                1 => new object[] { pckPath },
                2 => new object[] { pckPath, true },
                3 => new object[] { pckPath, true, 0 },
                _ => null,
            };
            if (args == null)
            {
                Console.Error.WriteLine($"[STS2 Linux Compat] ShaderCompat: unexpected LoadResourcePack arity {_projectSettingsLoadResourcePack.GetParameters().Length}");
                return;
            }
            var ok = (bool)_projectSettingsLoadResourcePack.Invoke(null, args);
            _overlayLoaded = ok;
            Console.Error.WriteLine($"[STS2 Linux Compat] ShaderCompat: overlay pack load {(ok ? "OK" : "FAILED")} @ {pckPath}");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[STS2 Linux Compat] ShaderCompat: LoadResourcePack threw: {ex.Message}");
        }
    }

    public static void NodeAddChildPostfix(object __instance)
    {
        if (!_overlayLoaded || __instance == null) return;
        try
        {
            SweepSubtreeForShaderSwaps(__instance);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[STS2 Linux Compat] ShaderCompat: traversal failed: {ex.Message}");
        }
    }

    private static void SweepSubtreeForShaderSwaps(object node)
    {
        if (node == null) return;

        SwapShadersOnNode(node);

        var nodeType = node.GetType();
        var getChildCount = AccessTools.Method(nodeType, "GetChildCount");
        if (getChildCount == null) return;
        int count;
        try { count = (int)getChildCount.Invoke(node, new object[] { false }); }
        catch { return; }

        MethodInfo getChild = null;
        foreach (var m in nodeType.GetMethods(BindingFlags.Public | BindingFlags.Instance))
        {
            if (m.Name != "GetChild" || m.IsGenericMethod) continue;
            var ps = m.GetParameters();
            if (ps.Length == 2 && ps[0].ParameterType == typeof(int) && ps[1].ParameterType == typeof(bool))
            {
                getChild = m;
                break;
            }
        }
        if (getChild == null) return;

        for (int i = 0; i < count; i++)
        {
            object child;
            try { child = getChild.Invoke(node, new object[] { i, false }); }
            catch { continue; }
            if (child != null)
                SweepSubtreeForShaderSwaps(child);
        }
    }

    private static void SwapShadersOnNode(object node)
    {
        var matProp = node.GetType().GetProperty("Material");
        if (matProp == null) return;
        var material = matProp.GetValue(node);
        if (material == null) return;
        if (material.GetType().Name != "ShaderMaterial") return;

        var shaderProp = material.GetType().GetProperty("Shader");
        if (shaderProp == null) return;
        var shader = shaderProp.GetValue(material);
        if (shader == null) return;

        var resPathProp = shader.GetType().GetProperty("ResourcePath");
        if (resPathProp == null) return;
        var resPath = resPathProp.GetValue(shader) as string;
        if (string.IsNullOrEmpty(resPath)) return;
        if (!ShaderOverrides.TryGetValue(resPath, out var replacementPath)) return;

        var replacement = ResolveReplacement(replacementPath);
        if (replacement == null) return;

        try
        {
            shaderProp.SetValue(material, replacement);
            Console.Error.WriteLine($"[STS2 Linux Compat] ShaderCompat: swapped {resPath} -> {replacementPath}");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[STS2 Linux Compat] ShaderCompat: shader setter failed: {ex.Message}");
        }
    }

    private static object ResolveReplacement(string path)
    {
        if (_replacementCache.TryGetValue(path, out var cached)) return cached;
        try
        {
            object loaded;
            switch (_resourceLoaderLoad.GetParameters().Length)
            {
                case 1: loaded = _resourceLoaderLoad.Invoke(null, new object[] { path }); break;
                case 2: loaded = _resourceLoaderLoad.Invoke(null, new object[] { path, null }); break;
                case 3: loaded = _resourceLoaderLoad.Invoke(null, new object[] { path, null, 1 /* CacheMode.Reuse */ }); break;
                case 4: loaded = _resourceLoaderLoad.Invoke(null, new object[] { path, null, false, 1 }); break;
                default: return null;
            }
            if (loaded != null)
                _replacementCache[path] = loaded;
            return loaded;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[STS2 Linux Compat] ShaderCompat: failed to load replacement {path}: {ex.Message}");
            return null;
        }
    }
}
