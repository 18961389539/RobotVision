namespace RobotVision.Hosting;

/// <summary>
/// 把配方/标定/回放/失败图等相对路径绑到 <see cref="AppConfig.DataRoot"/>。
/// 模型目录不绑。已是绝对路径的项保持不变（可重复调用）。
/// </summary>
public static class DataRootBinder
{
    /// <summary>将 cfg 中尚未绝对化的工位数据路径改写为 DataRoot 下的绝对路径。</summary>
    public static void Apply(AppConfig cfg)
    {
        var root = cfg.ResolveDataRoot();
        if (string.IsNullOrEmpty(root))
            return;

        Directory.CreateDirectory(root);
        cfg.RecipesFolder = Combine(root, cfg.RecipesFolder);
        cfg.CalibrationFolder = Combine(root, cfg.CalibrationFolder);
        cfg.FailureImage.Folder = Combine(root, cfg.FailureImage.Folder);
        cfg.ResultLog.Folder = Combine(root, cfg.ResultLog.Folder);
        cfg.CaptureSuccess.Folder = Combine(root, cfg.CaptureSuccess.Folder);
        cfg.ProcessHealth.Folder = Combine(root, cfg.ProcessHealth.Folder);
        cfg.FileLogging.Folder = Combine(root, cfg.FileLogging.Folder);
        cfg.Chat.AuditFolder = Combine(root, cfg.Chat.AuditFolder);
        cfg.Chat.CaptureFolder = Combine(root,
            string.IsNullOrWhiteSpace(cfg.Chat.CaptureFolder) ? "chat-captures" : cfg.Chat.CaptureFolder);
        foreach (var camera in cfg.Cameras)
        {
            if (!string.IsNullOrWhiteSpace(camera.Folder))
                camera.Folder = Combine(root, camera.Folder);
        }
    }

    /// <summary>
    /// 数据根下对应目录还没有文件时，从 exe 旁旧布局拷入（bin/recipes、bin/data/…），
    /// 避免改 DataRoot 后第一次启动看不到已有配方和回放图。
    /// </summary>
    public static void CopyLegacyIfEmpty(AppConfig cfg, string exeDirectory)
    {
        var root = cfg.ResolveDataRoot();
        if (string.IsNullOrEmpty(root) || string.IsNullOrWhiteSpace(exeDirectory))
            return;

        CopyJsonIfDestEmpty(Path.Combine(exeDirectory, "recipes"), cfg.RecipesFolder);
        CopyAllIfDestEmpty(Path.Combine(exeDirectory, "data", "replay"), ReplayDestination(cfg));
        CopyAllIfDestEmpty(Path.Combine(exeDirectory, "data", "calibration"), cfg.CalibrationFolder);
        CopyAllIfDestEmpty(Path.Combine(exeDirectory, "data", "failures"), cfg.FailureImage.Folder);
        CopyAllIfDestEmpty(Path.Combine(exeDirectory, "data", "results"), cfg.ResultLog.Folder);
        CopyAllIfDestEmpty(Path.Combine(exeDirectory, "data", "captures"), cfg.CaptureSuccess.Folder);
        CopyAllIfDestEmpty(Path.Combine(exeDirectory, "data", "metrics"), cfg.ProcessHealth.Folder);
        CopyAllIfDestEmpty(Path.Combine(exeDirectory, "data", "chat-captures"), cfg.Chat.CaptureFolder);
        CopyAllIfDestEmpty(Path.Combine(exeDirectory, "data", "chat-audit"), cfg.Chat.AuditFolder);
    }

    private static string ReplayDestination(AppConfig cfg)
    {
        var file = cfg.Cameras.FirstOrDefault(c =>
            string.Equals(c.Type, "File", StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(c.Folder));
        return file is null ? Path.Combine(cfg.ResolveDataRoot(), "replay") : file.Folder;
    }

    private static string Combine(string root, string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return root;
        if (Path.IsPathRooted(path))
            return Path.GetFullPath(path);
        return Path.GetFullPath(Path.Combine(root, path));
    }

    private static void CopyJsonIfDestEmpty(string source, string dest)
    {
        if (!Directory.Exists(source) || string.IsNullOrWhiteSpace(dest))
            return;
        Directory.CreateDirectory(dest);
        if (Directory.EnumerateFiles(dest, "*.json").Any())
            return;
        foreach (var src in Directory.EnumerateFiles(source, "*.json"))
        {
            var target = Path.Combine(dest, Path.GetFileName(src));
            if (!File.Exists(target))
                File.Copy(src, target);
        }
    }

    private static void CopyAllIfDestEmpty(string source, string dest)
    {
        if (!Directory.Exists(source) || string.IsNullOrWhiteSpace(dest))
            return;
        Directory.CreateDirectory(dest);
        if (Directory.EnumerateFileSystemEntries(dest).Any())
            return;
        foreach (var src in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            var rel = Path.GetRelativePath(source, src);
            var target = Path.Combine(dest, rel);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            if (!File.Exists(target))
                File.Copy(src, target);
        }
    }
}
