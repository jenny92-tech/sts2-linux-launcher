using System;

namespace STS2LinuxLauncher;

/// User-facing quality preference from launcher UI (SLL_QUALITY env var).
/// Decoupled from RAM: loading strategy (lazy vs full) is auto-determined by
/// MemoryBudget. This axis drives particles, menu animations, and VFX.
public static class QualityProfile
{
    public enum Level { Smooth, Balanced, Quality }

    public static readonly Level Current = Parse(Environment.GetEnvironmentVariable("SLL_QUALITY"));

    public static int ParticleCap => Current switch
    {
        Level.Smooth  => 16,
        Level.Quality => 64,
        _             => 48,
    };

    /// Main menu animation (~370 MB GPU) is cut on Smooth.
    /// Also unconditionally cut when RAM < 1.5 GB (see MenuDietPatches).
    public static bool CutMenuAnim => Current == Level.Smooth;

    private static Level Parse(string raw)
    {
        switch (raw?.Trim().ToLowerInvariant())
        {
            case "smooth":
            case "fluent":
            case "performance":
                return Level.Smooth;
            case "quality":
            case "fidelity":
                return Level.Quality;
            default:
                return Level.Balanced;
        }
    }

    public static void LogChosen()
    {
        var raw = Environment.GetEnvironmentVariable("SLL_QUALITY") ?? "unset";
        Console.Error.WriteLine(
            $"[STS2 Linux Compat] quality = {Current} (particleCap={ParticleCap}, cutMenuAnim={CutMenuAnim}) [SLL_QUALITY={raw}]");
    }
}
