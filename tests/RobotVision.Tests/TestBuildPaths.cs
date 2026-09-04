namespace RobotVision.Tests;

/// <summary>解析仓库根与 WPF 构建产物路径（Release 优先，兼容本地 Debug）。</summary>
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

    /// <summary>本机 RobotVisionData 根（captures/recipes/models），与 WPF 运行数据目录同构。</summary>
    public static string? ResolveRobotVisionDataRoot()
    {
        var env = Environment.GetEnvironmentVariable("ROBOTVISION_DATA_DIR");
        if (!string.IsNullOrWhiteSpace(env) && Directory.Exists(env))
            return Path.GetFullPath(env);

        foreach (var candidate in new[]
                 {
                     @"E:\RobotVisionData\RobotVisionData",
                     Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                         "RobotVisionData"),
                 })
        {
            if (Directory.Exists(candidate) &&
                (Directory.Exists(Path.Combine(candidate, "captures")) ||
                 Directory.Exists(Path.Combine(candidate, "recipes"))))
                return Path.GetFullPath(candidate);
        }

        return null;
    }

    public static string? ResolveWpfBin()
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
            var bin = Path.Combine(cfgDir, tfm);
            if (Directory.Exists(bin))
                return Path.GetFullPath(bin);
        }

        return null;
    }

    public static string? ResolveWpfExe()
    {
        var bin = ResolveWpfBin();
        if (bin is null)
            return null;

        var exe = Path.Combine(bin, "RobotVision.Wpf.exe");
        return File.Exists(exe) ? exe : null;
    }

    public static string? ResolveRecipesDir()
    {
        var wpfBin = ResolveWpfBin();
        if (wpfBin is not null)
        {
            var dir = Path.Combine(wpfBin, "recipes");
            if (Directory.Exists(dir))
                return dir;
        }

        var root = FindRepoRoot();
        if (root is null)
            return null;

        var repoRecipes = Path.Combine(root, "recipes");
        return Directory.Exists(repoRecipes) ? repoRecipes : null;
    }

    public static string? ResolveModelsDir()
    {
        var root = FindRepoRoot();
        if (root is not null)
        {
            var models = Path.Combine(root, "models");
            if (Directory.Exists(models) &&
                Directory.EnumerateFiles(models, "*.onnx").Any(RepoAssets.IsUsable))
                return models;
        }

        var wpfBin = ResolveWpfBin();
        if (wpfBin is not null)
        {
            var models = Path.Combine(wpfBin, "models");
            if (Directory.Exists(models))
                return models;
        }

        return root is not null ? Path.Combine(root, "models") : null;
    }

    public static string? ResolveRobotVisionDataRecipesDir()
    {
        var root = ResolveRobotVisionDataRoot();
        if (root is null)
            return null;
        var dir = Path.Combine(root, "recipes");
        return Directory.Exists(dir) ? dir : null;
    }

    public static string? ResolveRobotVisionDataModelsDir()
    {
        var root = ResolveRobotVisionDataRoot();
        if (root is null)
            return null;
        var dir = Path.Combine(root, "models");
        return Directory.Exists(dir) &&
               Directory.EnumerateFiles(dir, "*.onnx").Any()
            ? dir
            : null;
    }

    public static string CombineWpf(params string[] relative)
    {
        var bin = ResolveWpfBin()
                  ?? throw new InvalidOperationException("WPF bin directory not found (build RobotVision.Wpf first).");
        return Path.Combine(bin, Path.Combine(relative));
    }
}
