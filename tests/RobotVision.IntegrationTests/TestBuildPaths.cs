namespace RobotVision.IntegrationTests;

internal static class TestBuildPaths
{
    public static string? FindRepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        for (var i = 0; i < 12 && !string.IsNullOrEmpty(dir); i++)
        {
            if (File.Exists(Path.Combine(dir, "RobotVision.sln")))
                return dir;
            dir = Path.GetDirectoryName(dir);
        }

        return null;
    }

    public static string? ResolveWpfExe()
    {
        var root = FindRepoRoot();
        if (root is null)
            return null;

        foreach (var cfg in new[] { "Release", "Debug" })
        {
            var cfgDir = Path.Combine(root, "src", "RobotVision.Wpf", "bin", cfg);
            if (!Directory.Exists(cfgDir))
                continue;
            // 动态匹配 TFM 目录（net8.0-windows / net10.0-windows …），避免框架升级后写死失效
            var tfm = Directory.GetDirectories(cfgDir)
                .Select(Path.GetFileName)
                .FirstOrDefault(n => n is not null && n.StartsWith("net", StringComparison.Ordinal));
            if (tfm is null)
                continue;
            var exe = Path.Combine(cfgDir, tfm, "RobotVision.Wpf.exe");
            if (File.Exists(exe))
                return Path.GetFullPath(exe);
        }

        return null;
    }
}
