using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using HarmonyLib;

namespace STS2LinuxLauncher.Patches;

/// Dense memory-pressure tracing. Only active when SLL_DEBUG=1.
/// Runs a 500ms background ticker on its own thread reading
/// /proc/self/status and /proc/meminfo, and hooks AtlasManager,
/// AssetCache, PreloadManager, NTransition, and NSceneContainer
/// for per-event snapshots.
public static class MemoryProbePatches
{
    private static Thread _bgThread;
    private static volatile bool _bgRunning;
    private static readonly Stopwatch _wall = Stopwatch.StartNew();

    public static void Apply(Harmony harmony)
    {
        if (!Diag.Enabled)
        {
            Console.Error.WriteLine("[STS2 Linux Compat] MemoryProbePatches disabled (quiet; touch .debug to enable)");
            return;
        }
        StartBackgroundTicker();
        HookEventProbes(harmony);
        Console.Error.WriteLine("[STS2 Linux Compat] MemoryProbePatches installed (bg-ticker + event hooks)");
    }

    private static void StartBackgroundTicker()
    {
        if (_bgRunning) return;
        _bgRunning = true;
        _bgThread = new Thread(() =>
        {
            while (_bgRunning)
            {
                try
                {
                    LogProc("bg-tick");
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"[STS2 Linux Compat][PROBE] bg-tick failed: {ex.Message}");
                }
                Thread.Sleep(500);
            }
        })
        {
            IsBackground = true,
            Name = "sts2-mem-probe",
        };
        _bgThread.Start();
    }

    private static void LogProc(string tag)
    {
        long rssKb = 0, vmSizeKb = 0, vmDataKb = 0;
        long memFreeKb = 0, memAvailKb = 0;
        try
        {
            foreach (var line in File.ReadAllLines("/proc/self/status"))
            {
                if (line.StartsWith("VmRSS:", StringComparison.Ordinal)) rssKb = ParseKb(line);
                else if (line.StartsWith("VmSize:", StringComparison.Ordinal)) vmSizeKb = ParseKb(line);
                else if (line.StartsWith("VmData:", StringComparison.Ordinal)) vmDataKb = ParseKb(line);
            }
        }
        catch { }
        try
        {
            foreach (var line in File.ReadAllLines("/proc/meminfo"))
            {
                if (line.StartsWith("MemFree:", StringComparison.Ordinal)) memFreeKb = ParseKb(line);
                else if (line.StartsWith("MemAvailable:", StringComparison.Ordinal)) memAvailKb = ParseKb(line);
            }
        }
        catch { }

        long gcAllocMb = -1;
        try { gcAllocMb = GC.GetTotalMemory(false) / 1024 / 1024; } catch { }

        long anonKb = 0, fileKb = 0, shmemKb = 0;
        try
        {
            foreach (var line in File.ReadAllLines("/proc/self/smaps_rollup"))
            {
                if (line.StartsWith("Anonymous:", StringComparison.Ordinal)) anonKb = ParseKb(line);
                else if (line.StartsWith("Private_Clean:", StringComparison.Ordinal)) fileKb += ParseKb(line);
                else if (line.StartsWith("Shared_Clean:", StringComparison.Ordinal)) fileKb += ParseKb(line);
                else if (line.StartsWith("Shmem:", StringComparison.Ordinal)) shmemKb = ParseKb(line);
            }
        }
        catch { }

        Console.Error.WriteLine(
            $"[STS2 Linux Compat][PROBE] t={_wall.ElapsedMilliseconds}ms tag={tag} " +
            $"RSS={rssKb / 1024}MB anon={anonKb / 1024}MB fileClean={fileKb / 1024}MB shmem={shmemKb / 1024}MB " +
            $"VmData={vmDataKb / 1024}MB sysAvail={memAvailKb / 1024}MB gcHeap={gcAllocMb}MB");

        if (rssKb > 450 * 1024 && !_smapsDumped)
        {
            _smapsDumped = true;
            DumpTopMappings();
        }
    }

    private static long ParseKb(string line)
    {
        var parts = line.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 2 && long.TryParse(parts[1], out var kb)) return kb;
        return 0;
    }

    private static volatile bool _smapsDumped;

    private static void DumpTopMappings()
    {
        try
        {
            var totals = new Dictionary<string, long>(StringComparer.Ordinal);
            string currentName = null;
            foreach (var line in File.ReadLines("/proc/self/smaps"))
            {
                // smaps mapping header line
                if (line.Length > 0 && line[0] != ' ' && char.IsLetterOrDigit(line[0]) && line.Contains('-') && line.Contains(' '))
                {
                    var idx = line.IndexOf('/');
                    var bracket = line.IndexOf('[');
                    if (idx >= 0) currentName = line.Substring(idx);
                    else if (bracket >= 0) currentName = line.Substring(bracket);
                    else currentName = "<anon>";
                }
                else if (line.StartsWith("Rss:", StringComparison.Ordinal) && currentName != null)
                {
                    var kb = ParseKb(line);
                    totals.TryGetValue(currentName, out var prev);
                    totals[currentName] = prev + kb;
                }
            }
            Console.Error.WriteLine("[STS2 Linux Compat][SMAPS] top mappings by RSS:");
            foreach (var kv in totals.OrderByDescending(k => k.Value).Take(12))
                Console.Error.WriteLine($"[STS2 Linux Compat][SMAPS]   {kv.Value / 1024,5} MB  {kv.Key}");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[STS2 Linux Compat][SMAPS] dump failed: {ex.Message}");
        }
    }

    private static void HookEventProbes(Harmony harmony)
    {
        var atlasManager = AccessTools.TypeByName("MegaCrit.Sts2.Core.Assets.AtlasManager");
        TryProbe(harmony, atlasManager, "Load", nameof(AtlasLoadPrefix), nameof(AtlasLoadPostfix));
        TryProbe(harmony, atlasManager, "Unload", nameof(AtlasUnloadPrefix), nameof(AtlasUnloadPostfix));

        var assetCache = AccessTools.TypeByName("MegaCrit.Sts2.Core.Assets.AssetCache");
        TryProbe(harmony, assetCache, "UnloadMissedCacheAssets", nameof(UnloadMissedPrefix), nameof(UnloadMissedPostfix));
        TryProbe(harmony, assetCache, "SetAsset", null, nameof(SetAssetPostfix));

        var preloadManager = AccessTools.TypeByName("MegaCrit.Sts2.Core.Assets.PreloadManager");
        TryProbe(harmony, preloadManager, "LoadCommonAndMainMenuAssets", nameof(LoadCommonPrefix), nameof(LoadCommonPostfix));

        var nTransition = AccessTools.TypeByName("MegaCrit.Sts2.Core.Nodes.NTransition");
        TryProbe(harmony, nTransition, "FadeIn", nameof(FadeInPrefix), nameof(FadeInPostfix));
        TryProbe(harmony, nTransition, "FadeOut", nameof(FadeOutPrefix), nameof(FadeOutPostfix));

        var nSceneContainer = AccessTools.TypeByName("MegaCrit.Sts2.Core.Nodes.NSceneContainer");
        TryProbe(harmony, nSceneContainer, "SetScene", nameof(SetScenePrefix), nameof(SetScenePostfix));
    }

    private static void TryProbe(Harmony harmony, Type type, string methodName, string prefixName, string postfixName)
    {
        if (type == null) return;
        try
        {
            var method = AccessTools.Method(type, methodName);
            if (method == null) return;

            HarmonyMethod prefix = prefixName != null
                ? new HarmonyMethod(typeof(MemoryProbePatches), prefixName)
                : null;
            HarmonyMethod postfix = postfixName != null
                ? new HarmonyMethod(typeof(MemoryProbePatches), postfixName)
                : null;
            harmony.Patch(method, prefix: prefix, postfix: postfix);
            Console.Error.WriteLine($"[STS2 Linux Compat][PROBE] hooked {type.Name}.{methodName}");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[STS2 Linux Compat][PROBE] hook {type.Name}.{methodName} skipped: {ex.Message}");
        }
    }

    public static void AtlasLoadPrefix(string atlasName) => LogProc($"atlas.Load.before:{atlasName}");
    public static void AtlasLoadPostfix(string atlasName) => LogProc($"atlas.Load.after:{atlasName}");
    public static void AtlasUnloadPrefix(string atlasName) => LogProc($"atlas.Unload.before:{atlasName}");
    public static void AtlasUnloadPostfix(string atlasName) => LogProc($"atlas.Unload.after:{atlasName}");
    public static void UnloadMissedPrefix() => LogProc("cache.UnloadMissed.before");
    public static void UnloadMissedPostfix() => LogProc("cache.UnloadMissed.after");
    public static void SetAssetPostfix(string path) => LogProc($"cache.SetAsset:{path}");
    public static void LoadCommonPrefix() => LogProc("PreloadManager.LoadCommon.before");
    public static void LoadCommonPostfix() => LogProc("PreloadManager.LoadCommon.after");
    public static void FadeInPrefix() => LogProc("NTransition.FadeIn.before");
    public static void FadeInPostfix() => LogProc("NTransition.FadeIn.after");
    public static void FadeOutPrefix() => LogProc("NTransition.FadeOut.before");
    public static void FadeOutPostfix() => LogProc("NTransition.FadeOut.after");
    public static void SetScenePrefix() => LogProc("NSceneContainer.SetScene.before");
    public static void SetScenePostfix() => LogProc("NSceneContainer.SetScene.after");
}
