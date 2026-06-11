using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using HarmonyLib;

namespace STS2LinuxLauncher.Patches;

internal static class MemSnap
{
    private static readonly Stopwatch _wall = Stopwatch.StartNew();

    public static void Log(string tag)
    {
        if (!Diag.Enabled) return;
        try
        {
            long vmRssKb = 0, vmDataKb = 0;
            foreach (var line in File.ReadAllLines("/proc/self/status"))
            {
                if (line.StartsWith("VmRSS:", StringComparison.Ordinal)) vmRssKb = ParseKb(line);
                else if (line.StartsWith("VmData:", StringComparison.Ordinal)) vmDataKb = ParseKb(line);
            }
            long memAvailKb = 0;
            foreach (var line in File.ReadAllLines("/proc/meminfo"))
            {
                if (line.StartsWith("MemAvailable:", StringComparison.Ordinal)) memAvailKb = ParseKb(line);
            }
            Console.Error.WriteLine(
                $"[STS2 Linux Compat][MEM] t={_wall.ElapsedMilliseconds}ms tag={tag} " +
                $"RSS={vmRssKb / 1024}MB VmData={vmDataKb / 1024}MB sysAvail={memAvailKb / 1024}MB");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[STS2 Linux Compat][MEM] snap failed: {ex.Message}");
        }
    }

    private static long ParseKb(string line)
    {
        var parts = line.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 2 && long.TryParse(parts[1], out var kb)) return kb;
        return 0;
    }
}

/// Lazy load axis (auto, based on /proc/meminfo via MemoryBudget):
///   < 1.5 GB → boot preload is a no-op; only combat-entry sets pass through.
///   ≥ 1.5 GB → no patches installed; the game's native loader runs as-is.
/// The old chunked mode has been removed — it was worse than both extremes.
public static class LifecyclePreloadPatches
{
    private static Type _assetLoadingSessionType;
    private static MethodInfo _assetLoadingSessionEmpty;
    private static MethodInfo _taskFromResultGeneric;

    public static void Apply(Harmony harmony)
    {
        MemSnap.Log("LifecyclePreloadPatches.Apply enter");

        if (MemoryBudget.FullLoadAllowed)
        {
            Console.Error.WriteLine(
                $"[STS2 Linux Compat] full load (MemTotal={MemoryBudget.TotalMB}MB ≥ {MemoryBudget.FullLoadFloorMB}MB): "
                + "native PreloadManager kept (PC parity), no preload patches installed");
            return;
        }

        Console.Error.WriteLine(
            $"[STS2 Linux Compat] lazy load (MemTotal={MemoryBudget.TotalMB}MB < {MemoryBudget.FullLoadFloorMB}MB, "
            + $"avail={MemoryBudget.AvailableMB()}MB): boot no-op + runtime set skip");

        var preloadManagerType = AccessTools.TypeByName("MegaCrit.Sts2.Core.Assets.PreloadManager");
        if (preloadManagerType == null)
        {
            Console.Error.WriteLine("[STS2 Linux Compat] PreloadManager type not found; preload patches not installed");
            return;
        }

        var loadCommon = AccessTools.Method(preloadManagerType, "LoadCommonAndMainMenuAssets");
        if (loadCommon != null)
        {
            harmony.Patch(loadCommon, prefix: new HarmonyMethod(typeof(LifecyclePreloadPatches), nameof(LoadCommonAndMainMenuAssetsPrefix)));
            Console.Error.WriteLine("[STS2 Linux Compat] boot preload → no-op installed");
        }
        else
        {
            Console.Error.WriteLine("[STS2 Linux Compat] LoadCommonAndMainMenuAssets not found; boot no-op skipped");
        }

        if (TryResolveSkipReflection())
        {
            var loadAssetSets = AccessTools.Method(preloadManagerType, "LoadAssetSets");
            if (loadAssetSets != null)
            {
                harmony.Patch(loadAssetSets, prefix: new HarmonyMethod(typeof(LifecyclePreloadPatches), nameof(LoadAssetSetsPrefix)));
                Console.Error.WriteLine("[STS2 Linux Compat] runtime set skip installed");
            }
        }

        MemSnap.Log("LifecyclePreloadPatches.Apply exit");
    }

    public static bool LoadCommonAndMainMenuAssetsPrefix(ref Task __result)
    {
        Console.Error.WriteLine("[STS2 Linux Compat] lazy: LoadCommonAndMainMenuAssets → no-op");
        __result = Task.CompletedTask;
        return false;
    }

    public static bool LoadAssetSetsPrefix(string name, ref object __result)
    {
        try
        {
            if (!ShouldSkipRuntimeSet(name, out var reason)) return true;
            var empty = _assetLoadingSessionEmpty.Invoke(null, null);
            __result = _taskFromResultGeneric.Invoke(null, new object[] { empty });
            Console.Error.WriteLine($"[STS2 Linux Compat] runtime preload skipped: {name} ({reason})");
            return false;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[STS2 Linux Compat] LoadAssetSetsPrefix({name}) failed, running original: {ex.Message}");
            return true;
        }
    }

    private static bool ShouldSkipRuntimeSet(string name, out string reason)
    {
        if (name is "IntroLogo" or "MainMenuEssentials")
        {
            reason = null;
            return false;
        }
        // Allow combat-entry preloads through; skip everything else.
        if (name.StartsWith("Combat", StringComparison.Ordinal) ||
            name.StartsWith("characters=", StringComparison.Ordinal))
        {
            reason = null;
            return false;
        }
        reason = "lazy(<1.5G): defer to lazy load";
        return true;
    }

    private static bool TryResolveSkipReflection()
    {
        _assetLoadingSessionType = AccessTools.TypeByName("MegaCrit.Sts2.Core.Assets.AssetLoadingSession");
        if (_assetLoadingSessionType == null) return false;
        _assetLoadingSessionEmpty = AccessTools.Method(_assetLoadingSessionType, "Empty");
        if (_assetLoadingSessionEmpty == null) return false;
        var fromResult = typeof(Task).GetMethod(nameof(Task.FromResult), BindingFlags.Public | BindingFlags.Static);
        if (fromResult == null) return false;
        _taskFromResultGeneric = fromResult.MakeGenericMethod(_assetLoadingSessionType);
        return true;
    }
}
