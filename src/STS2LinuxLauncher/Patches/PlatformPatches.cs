using System;
using System.Threading.Tasks;
using HarmonyLib;

namespace STS2LinuxLauncher.Patches;

/// Strips Steam, Sentry, and related telemetry at the Harmony layer
/// (the libsteam_api64.so stub is the P/Invoke-level fallback).
/// All hooks are best-effort via reflection — missing types are skipped.
public static class PlatformPatches
{
    public static void Apply(Harmony harmony)
    {
        Hook(harmony,
             "MegaCrit.Sts2.Core.Nodes.NGame", "InitializePlatform",
             nameof(InitializePlatformPrefix), "Steam init");

        Hook(harmony,
             "MegaCrit.Sts2.Core.Debug.SentryService", "Initialize",
             nameof(SkipPrefix), "Sentry init");

        Hook(harmony,
             "MegaCrit.Sts2.Core.Debug.OsDebugInfo", "LogSystemInfo",
             nameof(CompleteTaskPrefix), "OsDebugInfo");

        HookGetter(harmony,
                   "MegaCrit.Sts2.Core.Saves.PrefsSave", "UploadData",
                   nameof(ReturnFalsePrefix), "PrefsSave.UploadData");
    }

    private static void Hook(Harmony h, string typeName, string method,
                             string prefixName, string label)
    {
        var t = AccessTools.TypeByName(typeName);
        var m = t == null ? null : AccessTools.Method(t, method);
        if (m == null) { Log($"{label}: {typeName}.{method} not found, skipping"); return; }
        h.Patch(m, prefix: new HarmonyMethod(typeof(PlatformPatches), prefixName));
        Log($"{label}: hooked {typeName}.{method}");
    }

    private static void HookGetter(Harmony h, string typeName, string propName,
                                   string prefixName, string label)
    {
        var t = AccessTools.TypeByName(typeName);
        var m = t == null ? null : AccessTools.PropertyGetter(t, propName);
        if (m == null) { Log($"{label}: {typeName}.{propName} getter not found, skipping"); return; }
        h.Patch(m, prefix: new HarmonyMethod(typeof(PlatformPatches), prefixName));
        Log($"{label}: hooked {typeName}.{propName} getter");
    }

    public static bool InitializePlatformPrefix(ref Task<bool> __result)
    {
        __result = Task.FromResult(true);
        return false;
    }

    public static bool SkipPrefix() => false;

    public static bool CompleteTaskPrefix(ref Task __result)
    {
        __result = Task.CompletedTask;
        return false;
    }

    public static bool ReturnFalsePrefix(ref bool __result)
    {
        __result = false;
        return false;
    }

    private static void Log(string s) => Console.Error.WriteLine($"[STS2 Linux Compat] {s}");
}
