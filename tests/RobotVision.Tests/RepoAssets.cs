namespace RobotVision.Tests;

/// <summary>从测试输出目录向上找仓库根与可用 ONNX。</summary>
internal static class RepoAssets
{
    public static string? FindRoot() => TestBuildPaths.FindRepoRoot();

    public static string? FindOnnx()
    {
        var modelsDir = TestBuildPaths.ResolveModelsDir();
        if (modelsDir is null || !Directory.Exists(modelsDir))
            return null;

        foreach (var name in new[] { "a01_kpt.onnx", "OSFP-SEG.onnx" })
        {
            var path = Path.Combine(modelsDir, name);
            if (IsUsable(path))
                return path;
        }

        return Directory.EnumerateFiles(modelsDir, "*.onnx")
            .FirstOrDefault(IsUsable);
    }

    public static bool IsUsable(string? path) =>
        !string.IsNullOrEmpty(path) && File.Exists(path) && new FileInfo(path).Length > 0;
}
