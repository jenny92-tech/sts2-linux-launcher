using System;
using System.IO;
using System.Reflection;
using HarmonyLib;

namespace STS2LinuxLauncher.Patches;

/// Writes power-saving defaults on first launch (MSAA=0).
/// Marker file prevents overwriting if the player changes settings later.
public static class GameSettingsDefaultPatches
{
    private static string MarkerPath => Path.Combine(PortPaths.GameDir, "conf", ".sll_defaults_applied");
    private static bool _done;

    public static void Apply(Harmony harmony)
    {
        var t = AccessTools.TypeByName("MegaCrit.Sts2.Core.Nodes.Screens.MainMenu.NMainMenu");
        var m = t == null ? null : AccessTools.Method(t, "_Ready");
        if (m == null) { Console.Error.WriteLine("[STS2 Linux Compat] GameSettingsDefault: hook missing"); return; }
        harmony.Patch(m, postfix: new HarmonyMethod(typeof(GameSettingsDefaultPatches), nameof(ReadyPostfix)));
        Console.Error.WriteLine("[STS2 Linux Compat] GameSettingsDefault installed");
    }

    public static void ReadyPostfix()
    {
        if (_done) return;
        _done = true;
        ApplyLanguage();
        try
        {
            if (File.Exists(MarkerPath)) return;

            var saveManagerType = AccessTools.TypeByName("MegaCrit.Sts2.Core.Saves.SaveManager");
            var instance = saveManagerType?.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
            var settings = instance?.GetType().GetProperty("SettingsSave")?.GetValue(instance);
            if (settings == null) { Console.Error.WriteLine("[STS2 Linux Compat] GameSettingsDefault: SettingsSave null"); return; }

            var msaaProp = settings.GetType().GetProperty("Msaa");
            if (msaaProp != null)
                msaaProp.SetValue(settings, Convert.ChangeType(0, Nullable.GetUnderlyingType(msaaProp.PropertyType) ?? msaaProp.PropertyType));

            instance.GetType().GetMethod("SaveSettings", Type.EmptyTypes)?.Invoke(instance, null);
            File.WriteAllText(MarkerPath, "1");
            Console.Error.WriteLine("[STS2 Linux Compat] GameSettingsDefault applied (Msaa=0, first launch)");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[STS2 Linux Compat] GameSettingsDefault failed: {ex.Message}");
        }
    }

    /// Writes SLL_LANGUAGE to game settings and switches the locale immediately.
    /// (Without this, the game defaults to English in invariant mode.)
    private static void ApplyLanguage()
    {
        try
        {
            var sll = PortPaths.Get("SLL_LANGUAGE");

            var target = sll switch
            {
                "zh_CN" => "zhs",
                "en_US" => "eng",
                _ => null,
            };
            if (target == null) return;

            var saveManagerType = AccessTools.TypeByName("MegaCrit.Sts2.Core.Saves.SaveManager");
            var instance = saveManagerType?.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
            var settings = instance?.GetType().GetProperty("SettingsSave")?.GetValue(instance);
            var langProp = settings?.GetType().GetProperty("Language");
            if (langProp == null) return;

            var current = langProp.GetValue(settings) as string;
            if (current == target) return;

            langProp.SetValue(settings, target);
            instance.GetType().GetMethod("SaveSettings", Type.EmptyTypes)?.Invoke(instance, null);

            var locType = AccessTools.TypeByName("MegaCrit.Sts2.Core.Localization.LocManager");
            var loc = locType?.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
            loc?.GetType().GetMethod("SetLanguage", new[] { typeof(string) })?.Invoke(loc, new object[] { target });

            Console.Error.WriteLine($"[STS2 Linux Compat] language: {current} -> {target} (from launcher SLL_LANGUAGE={sll})");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[STS2 Linux Compat] ApplyLanguage failed: {ex.Message}");
        }
    }
}
