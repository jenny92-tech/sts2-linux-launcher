using System;
using System.Reflection;
using System.Text;
using HarmonyLib;

namespace STS2LinuxLauncher.Patches;

/// Hide heavy background/Spine nodes on the main menu.
/// Activated when RAM < 1.5 GB or quality == Smooth.
public static class MenuDietPatches
{
    private static bool _dietActive;

    public static void Apply(Harmony harmony)
    {
        // Hide heavy menu background nodes when RAM < 1.5 G or quality is Smooth.
        _dietActive = !MemoryBudget.FullLoadAllowed || QualityProfile.CutMenuAnim;
        var t = AccessTools.TypeByName("MegaCrit.Sts2.Core.Nodes.Screens.MainMenu.NMainMenu");
        var m = t == null ? null : AccessTools.Method(t, "_Ready");
        if (m == null) { Console.Error.WriteLine("[STS2 Linux Compat] MenuDiet: NMainMenu._Ready not found"); return; }
        harmony.Patch(m, postfix: new HarmonyMethod(typeof(MenuDietPatches), nameof(ReadyPostfix)));
        Console.Error.WriteLine($"[STS2 Linux Compat] MenuDiet installed (diet={_dietActive})");
    }

    public static void ReadyPostfix(object __instance)
    {
        if (_dietActive)
        {
            try
            {
                var sb = new StringBuilder("[STS2 Linux Compat][TREE] NMainMenu:\n");
                DumpAndDiet(__instance, sb, 0, 3);
                Console.Error.WriteLine(sb.ToString());
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[STS2 Linux Compat] MenuDiet failed: {ex.Message}");
            }
        }
        // GameSettingsDefault's NMainMenu postfix does not fire on device;
        // piggyback on this verified postfix instead. _done prevents double-run.
        GameSettingsDefaultPatches.ReadyPostfix();
    }

    private static void DumpAndDiet(object node, StringBuilder sb, int depth, int maxDepth)
    {
        if (node == null || depth > maxDepth) return;
        var nt = node.GetType();
        var name = nt.GetProperty("Name")?.GetValue(node)?.ToString() ?? "?";
        sb.Append(new string(' ', depth * 2)).Append(name).Append(" (").Append(nt.Name).Append(")");

        var lower = name.ToLowerInvariant();
        if (depth > 0 && (lower.Contains("background") || lower.Contains("bg") || nt.Name.Contains("Spine")))
        {
            var vis = nt.GetProperty("Visible");
            if (vis != null && vis.PropertyType == typeof(bool))
            {
                vis.SetValue(node, false);
                sb.Append("  <- HIDDEN");
            }
        }
        sb.Append('\n');

        var getCount = AccessTools.Method(nt, "GetChildCount");
        if (getCount == null) return;
        int count;
        try { count = (int)getCount.Invoke(node, new object[] { false }); } catch { return; }

        MethodInfo getChild = null;
        foreach (var mi in nt.GetMethods(BindingFlags.Public | BindingFlags.Instance))
        {
            if (mi.Name != "GetChild" || mi.IsGenericMethod) continue;
            var ps = mi.GetParameters();
            if (ps.Length == 2 && ps[0].ParameterType == typeof(int)) { getChild = mi; break; }
        }
        if (getChild == null) return;
        for (int i = 0; i < count; i++)
        {
            object child = null;
            try { child = getChild.Invoke(node, new object[] { i, false }); } catch { }
            DumpAndDiet(child, sb, depth + 1, maxDepth);
        }
    }
}
