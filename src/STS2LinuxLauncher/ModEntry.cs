using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using HarmonyLib;

namespace STS2LinuxLauncher;

/// Entry point for sts2_compat.dll. All Godot interaction is via reflection
/// (Harmony __instance/__result carry the game's GodotSharp bridge).
/// We never reference GodotSharp at compile time — avoids a duplicate GodotSharp
/// instance when hostfxr loads us into a separate ALC, which would SEGV.
public static class ModEntry
{
    private static bool _applied;
    private static Harmony _harmony;

    /// Called by the godot fork before sts2.dll loads. Installs a gamedata
    /// assembly resolver as a fallback (primary path is launcher.sh cp -fu).
    [UnmanagedCallersOnly]
    public static int InitializeGodotSharp(
        IntPtr godotDllHandle,
        IntPtr outManagedCallbacks,
        IntPtr unmanagedCallbacks,
        int unmanagedCallbacksSize)
    {
        InstallGamedataResolver();
        return 1;
    }

    private static bool _resolverInstalled;
    private static string _gamedataDir;

    /// Falls back to ../gamedata/data_sts2_linuxbsd_arm64/ when the standard
    /// data_/ path is missing.
    private static void InstallGamedataResolver()
    {
        if (_resolverInstalled)
            return;
        try
        {
            var ourDir = System.IO.Path.GetDirectoryName(typeof(ModEntry).Assembly.Location);
            _gamedataDir = System.IO.Path.GetFullPath(
                System.IO.Path.Combine(ourDir, "..", "gamedata", "data_sts2_linuxbsd_arm64"));

            if (!System.IO.Directory.Exists(_gamedataDir))
            {
                Console.Error.WriteLine(
                    $"[STS2 Linux Compat] gamedata dir missing, resolver disabled: {_gamedataDir}");
                return;
            }

            System.Runtime.Loader.AssemblyLoadContext.Default.Resolving += ResolveFromGamedata;
            _resolverInstalled = true;
            Console.Error.WriteLine(
                $"[STS2 Linux Compat] gamedata resolver installed → {_gamedataDir}");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(
                $"[STS2 Linux Compat] InstallGamedataResolver failed: {ex.Message}");
        }
    }

    private static System.Reflection.Assembly ResolveFromGamedata(
        System.Runtime.Loader.AssemblyLoadContext ctx,
        System.Reflection.AssemblyName name)
    {
        var dllPath = System.IO.Path.Combine(_gamedataDir, name.Name + ".dll");
        if (!System.IO.File.Exists(dllPath))
            return null;
        try
        {
            var asm = ctx.LoadFromAssemblyPath(dllPath);
            Console.Error.WriteLine($"[STS2 Linux Compat] resolved {name.Name} ← gamedata/");
            return asm;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(
                $"[STS2 Linux Compat] resolve failed for {name.Name}: {ex.Message}");
            return null;
        }
    }

    /// Called by the godot fork after GodotPlugins init.
    [UnmanagedCallersOnly]
    public static void Apply() => ApplyInternal();

    public static void ApplyInternal()
    {
        if (_applied)
            return;
        _applied = true;

        _harmony = new Harmony("net.jenny92.sts2_linux_compat");
        QualityProfile.LogChosen();
        SafeApply("MemoryProbePatches", () => Patches.MemoryProbePatches.Apply(_harmony));
        SafeApply("PlatformPatches", () => Patches.PlatformPatches.Apply(_harmony));
        SafeApply("LifecyclePreloadPatches", () => Patches.LifecyclePreloadPatches.Apply(_harmony));
        SafeApply("AtlasLazyPatches", () => Patches.AtlasLazyPatches.Apply(_harmony));
        SafeApply("TransitionMaterialPatches", () => Patches.TransitionMaterialPatches.Apply(_harmony));
        SafeApply("ShaderCompatibilityPatches", () => Patches.ShaderCompatibilityPatches.Apply(_harmony));
        SafeApply("MenuDietPatches", () => Patches.MenuDietPatches.Apply(_harmony));
        SafeApply("ParticleDietPatches", () => Patches.ParticleDietPatches.Apply(_harmony));
        SafeApply("GameSettingsDefaultPatches", () => Patches.GameSettingsDefaultPatches.Apply(_harmony));
        Console.Error.WriteLine("[STS2 Linux Compat] ready.");
    }

    private static void SafeApply(string name, Action body)
    {
        try { body(); }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[STS2 Linux Compat] {name}.Apply failed (continuing): {ex.Message}");
        }
    }
}
