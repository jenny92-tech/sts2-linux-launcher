using System;
using System.IO;

namespace STS2LinuxLauncher;

/// Reads /proc/meminfo to determine whether the device has enough RAM
/// to keep all game assets resident (full load) or must defer to lazy loading.
/// This is a hard hardware limit (will it OOM?), not a user preference.
/// For quality preferences (particles/animations/VFX), see QualityProfile.
public static class MemoryBudget
{
    public static readonly long TotalMB = ReadMeminfoKB("MemTotal") / 1024;

    public static long AvailableMB() => ReadMeminfoKB("MemAvailable") / 1024;

    /// Physical RAM >= 1.5 GB → full native load; below → lazy load.
    /// Full-resident peak ~1 GB leaves no headroom on a nominal 1 GB device,
    /// while 2 GB devices land cleanly on the full-load side of the threshold.
    public const long FullLoadFloorMB = 1536;
    public static bool FullLoadAllowed => TotalMB >= FullLoadFloorMB;

    private static long ReadMeminfoKB(string key)
    {
        try
        {
            foreach (var line in File.ReadLines("/proc/meminfo"))
            {
                if (!line.StartsWith(key, StringComparison.Ordinal)) continue;
                var parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 2 && long.TryParse(parts[1], out var kb)) return kb;
            }
        }
        catch { }
        return 0;
    }
}
