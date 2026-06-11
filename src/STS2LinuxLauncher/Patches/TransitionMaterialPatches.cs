using System;
using System.Reflection;
using HarmonyLib;

namespace STS2LinuxLauncher.Patches;

/// Fixes black screen after intro logo. When preloading is disabled,
/// AssetCache.UnloadMissedCacheAssets disposes shared fade/fight transition
/// ShaderMaterials while NMainMenu.FadeIn is still animating them →
/// ObjectDisposedException. Fix: duplicate transition materials in
/// NTransition._Ready and AssetCache.GetMaterial so cleanup can't dispose
/// materials still in use.
public static class TransitionMaterialPatches
{
    private const string FadeTransitionPath = "res://materials/transitions/fade_transition_mat.tres";
    private const string FightTransitionPath = "res://materials/transitions/fight_transition_mat.tres";

    public static void Apply(Harmony harmony)
    {
        int installed = 0;

        var nTransitionType = AccessTools.TypeByName("MegaCrit.Sts2.Core.Nodes.NTransition");
        if (nTransitionType != null)
        {
            var readyMethod = AccessTools.Method(nTransitionType, "_Ready");
            if (readyMethod != null)
            {
                harmony.Patch(
                    readyMethod,
                    postfix: new HarmonyMethod(typeof(TransitionMaterialPatches), nameof(TransitionReadyPostfix)));
                installed++;
                Console.Error.WriteLine("[STS2 Linux Compat] pinned NTransition._Ready (transition material duplicate)");
            }
        }
        else
        {
            Console.Error.WriteLine("[STS2 Linux Compat] NTransition type not found; transition material patch partial");
        }

        var assetCacheType = AccessTools.TypeByName("MegaCrit.Sts2.Core.Assets.AssetCache");
        if (assetCacheType != null)
        {
            var getMaterialMethod = AccessTools.Method(assetCacheType, "GetMaterial");
            if (getMaterialMethod != null)
            {
                harmony.Patch(
                    getMaterialMethod,
                    postfix: new HarmonyMethod(typeof(TransitionMaterialPatches), nameof(GetMaterialPostfix)));
                installed++;
                Console.Error.WriteLine("[STS2 Linux Compat] pinned AssetCache.GetMaterial (transition material duplicate)");
            }
        }
        else
        {
            Console.Error.WriteLine("[STS2 Linux Compat] AssetCache type not found; transition material patch partial");
        }

        Console.Error.WriteLine($"[STS2 Linux Compat] TransitionMaterialPatches installed ({installed}/2 hooks)");
    }

    public static void TransitionReadyPostfix(object __instance)
    {
        try
        {
            if (__instance == null) return;

            var materialProp = __instance.GetType().GetProperty("Material");
            if (materialProp == null) return;

            var material = materialProp.GetValue(__instance);
            if (material == null) return;

            if (material.GetType().Name != "ShaderMaterial") return;

            var duplicate = TryDuplicateMaterial(material);
            if (duplicate != null)
                materialProp.SetValue(__instance, duplicate);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[STS2 Linux Compat] NTransition._Ready postfix failed: {ex.Message}");
        }
    }

    public static void GetMaterialPostfix(string path, ref object __result)
    {
        try
        {
            if (!IsTransitionMaterial(path)) return;
            if (__result == null) return;
            if (__result.GetType().Name != "ShaderMaterial") return;

            var duplicate = TryDuplicateMaterial(__result);
            if (duplicate != null)
                __result = duplicate;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[STS2 Linux Compat] AssetCache.GetMaterial postfix failed: {ex.Message}");
        }
    }

    private static bool IsTransitionMaterial(string path)
    {
        return path == FadeTransitionPath || path == FightTransitionPath;
    }

    private static object TryDuplicateMaterial(object material)
    {
        var duplicateMethod = material.GetType().GetMethod(
                "Duplicate",
                BindingFlags.Public | BindingFlags.Instance,
                null,
                new[] { typeof(bool) },
                null);
        if (duplicateMethod == null) return null;
        try
        {
            return duplicateMethod.Invoke(material, new object[] { false });
        }
        catch
        {
            return null;
        }
    }
}
