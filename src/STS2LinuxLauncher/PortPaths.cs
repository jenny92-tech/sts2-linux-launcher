using System;
using System.IO;
using System.Reflection;

namespace STS2LinuxLauncher;

/// GAMEDIR derived from assembly location. Config reads env vars first
/// (set by launcher.sh), falling back to launch_config.env.
public static class PortPaths
{
    public static readonly string GameDir;
    private static readonly string _envFile;

    static PortPaths()
    {
        try
        {
            var dllDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            GameDir = Path.GetDirectoryName(dllDir);
        }
        catch { GameDir = "."; }
        _envFile = Path.Combine(GameDir, "conf", "godot", "app_userdata", "STS2 Linux Launcher", "launch_config.env");
    }

    public static string Get(string key)
    {
        var v = Environment.GetEnvironmentVariable(key);
        if (!string.IsNullOrEmpty(v)) return v;
        try
        {
            if (File.Exists(_envFile))
                foreach (var line in File.ReadAllLines(_envFile))
                    if (line.StartsWith(key + "=", StringComparison.Ordinal))
                        return line.Substring(key.Length + 1).Trim().Trim('"');
        }
        catch { }
        return null;
    }
}
