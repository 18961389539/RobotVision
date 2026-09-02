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
            var exe = Path.Combine(root, "src", "RobotVision.Wpf", "bin", cfg, "net8.0-windows",
                "RobotVision.Wpf.exe");
            if (File.Exists(exe))
                return Path.GetFullPath(exe);
        }

        return null;
    }
}
