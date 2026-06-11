using System;
using System.Reflection;
using HarmonyLib;

namespace STS2LinuxLauncher.Patches;

/// Caps particle emitter Amount at AddChild time, based on the quality profile.
/// GpuParticles2D is expensive on Mali/gl_compatibility.
public static class ParticleDietPatches
{
    private static int _cap;

    public static void Apply(Harmony harmony)
    {
        _cap = QualityProfile.ParticleCap;
        Console.Error.WriteLine($"[STS2 Linux Compat] ParticleDiet cap = {_cap} (quality={QualityProfile.Current})");

        var ngame = AccessTools.TypeByName("MegaCrit.Sts2.Core.Nodes.NGame");
        Assembly godotSharp = null;
        var t = ngame?.BaseType;
        while (t != null && t != typeof(object))
        {
            if (t.Assembly.GetName().Name == "GodotSharp") { godotSharp = t.Assembly; break; }
            t = t.BaseType;
        }
        var nodeType = godotSharp?.GetType("Godot.Node");
        if (nodeType == null)
        {
            Console.Error.WriteLine("[STS2 Linux Compat] ParticleDiet: Godot.Node unresolved");
            return;
        }

        int patched = 0;
        foreach (var m in nodeType.GetMethods(BindingFlags.Public | BindingFlags.Instance))
        {
            if (m.Name != "AddChild" || m.IsGenericMethod) continue;
            try
            {
                harmony.Patch(m, postfix: new HarmonyMethod(typeof(ParticleDietPatches), nameof(AddChildPostfix)));
                patched++;
            }
            catch { }
        }
        Console.Error.WriteLine($"[STS2 Linux Compat] ParticleDiet installed (cap={_cap}, AddChild x{patched})");
    }

    public static void AddChildPostfix(object __0)
    {
        if (__0 == null) return;
        try { CapRecursive(__0, 0); }
        catch { }
    }

    private static void CapRecursive(object node, int depth)
    {
        if (node == null || depth > 6) return;
        var nt = node.GetType();
        var name = nt.Name;
        if (name == "GpuParticles2D" || name == "CpuParticles2D" ||
            name == "GpuParticles3D" || name == "CpuParticles3D")
        {
            var amountProp = nt.GetProperty("Amount");
            if (amountProp != null)
            {
                try
                {
                    var cur = Convert.ToInt32(amountProp.GetValue(node));
                    if (cur > _cap)
                        amountProp.SetValue(node, Convert.ChangeType(_cap, amountProp.PropertyType));
                }
                catch { }
            }
        }

        var getCount = AccessTools.Method(nt, "GetChildCount");
        if (getCount == null) return;
        int count;
        try { count = (int)getCount.Invoke(node, new object[] { false }); } catch { return; }
        if (count == 0) return;

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
            CapRecursive(child, depth + 1);
        }
    }
}
