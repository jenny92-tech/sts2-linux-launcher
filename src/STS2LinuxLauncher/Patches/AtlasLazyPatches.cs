using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;

namespace STS2LinuxLauncher.Patches;

/// <summary>
/// Swaps LoadAllAtlases for MegaCrit's own LoadEssentialAtlases subset,
/// loading the remaining atlases lazily on first GetSprite request.
/// Saves ~300 MB at the main menu on 1 GB devices.
/// </summary>
public static class AtlasLazyPatches
{
    private static MethodInfo _loadEssential;
    private static MethodInfo _loadAtlas;
    private static MethodInfo _isAtlasLoaded;

    private static readonly HashSet<string> _knownLoaded = new(StringComparer.Ordinal);
    private static readonly object _lock = new();

    public static void Apply(Harmony harmony)
    {
        if (MemoryBudget.FullLoadAllowed)
        {
            Console.Error.WriteLine("[STS2 Linux Compat] AtlasLazy: full load (≥1.5G), original LoadAllAtlases kept");
            return;
        }
        var atlasManager = AccessTools.TypeByName("MegaCrit.Sts2.Core.Assets.AtlasManager");
        if (atlasManager == null)
        {
            Console.Error.WriteLine("[STS2 Linux Compat] AtlasLazy: AtlasManager type not found; not installed");
            return;
        }

        _loadEssential = AccessTools.Method(atlasManager, "LoadEssentialAtlases");
        _loadAtlas = AccessTools.Method(atlasManager, "LoadAtlas", new[] { typeof(string) });
        _isAtlasLoaded = AccessTools.Method(atlasManager, "IsAtlasLoaded", new[] { typeof(string) });

        if (_loadEssential == null || _loadAtlas == null || _isAtlasLoaded == null)
        {
            Console.Error.WriteLine(
                $"[STS2 Linux Compat] AtlasLazy: API mismatch (essential={_loadEssential != null}, " +
                $"load={_loadAtlas != null}, isLoaded={_isAtlasLoaded != null}); not installed");
            return;
        }

        int hooks = 0;

        var loadAll = AccessTools.Method(atlasManager, "LoadAllAtlases");
        if (loadAll != null)
        {
            harmony.Patch(loadAll, prefix: new HarmonyMethod(typeof(AtlasLazyPatches), nameof(LoadAllAtlasesPrefix)));
            hooks++;
        }

        var getSprite = AccessTools.Method(atlasManager, "GetSprite", new[] { typeof(string), typeof(string) });
        if (getSprite != null)
        {
            harmony.Patch(getSprite, prefix: new HarmonyMethod(typeof(AtlasLazyPatches), nameof(GetSpritePrefix)));
            hooks++;
        }

        var loadInternal = AccessTools.Method(atlasManager, "LoadAtlasInternal", new[] { typeof(string) });
        if (loadInternal != null)
        {
            harmony.Patch(loadInternal,
                prefix: new HarmonyMethod(typeof(AtlasLazyPatches), nameof(LoadAtlasInternalPrefix)),
                postfix: new HarmonyMethod(typeof(AtlasLazyPatches), nameof(LoadAtlasInternalPostfix)));
            hooks++;
        }

        Console.Error.WriteLine($"[STS2 Linux Compat] AtlasLazyPatches installed ({hooks}/3 hooks)");
    }

    public static bool LoadAllAtlasesPrefix()
    {
        try
        {
            Console.Error.WriteLine("[STS2 Linux Compat] AtlasLazy: LoadAllAtlases intercepted → LoadEssentialAtlases");
            MemSnap.Log("atlas.essential.begin");
            _loadEssential.Invoke(null, null);
            MemSnap.Log("atlas.essential.end");
            Console.Error.WriteLine("[STS2 Linux Compat] AtlasLazy: essential atlases loaded; rest load lazily on first GetSprite");
            return false;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[STS2 Linux Compat] AtlasLazy: essential load failed, falling back to full load: {ex.Message}");
            return true;
        }
    }

    public static void GetSpritePrefix(string __0, string __1)
    {
        var atlasName = __0;
        if (string.IsNullOrEmpty(atlasName)) return;

        lock (_lock)
        {
            if (_knownLoaded.Contains(atlasName)) return;
        }

        try
        {
            if ((bool)_isAtlasLoaded.Invoke(null, new object[] { atlasName }))
            {
                lock (_lock) { _knownLoaded.Add(atlasName); }
                return;
            }

            Console.Error.WriteLine($"[STS2 Linux Compat] AtlasLazy: first use of '{atlasName}' (sprite='{__1}') → lazy loading");
            MemSnap.Log($"atlas.lazy.begin:{atlasName}");
            _loadAtlas.Invoke(null, new object[] { atlasName });
            MemSnap.Log($"atlas.lazy.end:{atlasName}");
            lock (_lock) { _knownLoaded.Add(atlasName); }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[STS2 Linux Compat] AtlasLazy: lazy load '{atlasName}' failed: {ex.Message}");
        }
    }

    // Per-atlas memory cost instrumentation. These fire for EVERY load
    // path (essential, lazy, anything else), so the log gives us a
    // complete per-atlas RSS price list.
    public static void LoadAtlasInternalPrefix(string __0) => MemSnap.Log($"atlas.load.before:{__0}");
    public static void LoadAtlasInternalPostfix(string __0) => MemSnap.Log($"atlas.load.after:{__0}");
}
