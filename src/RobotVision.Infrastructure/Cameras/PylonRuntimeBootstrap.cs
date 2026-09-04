using System.Runtime.InteropServices;

namespace RobotVision.Infrastructure.Cameras;

/// <summary>
/// pylon 原生库定位：把 <c>ProducerGEV.cti / PylonC_v10.dll</c> 所在目录注入 <c>PATH</c> 与
/// <c>GENICAM_GENTL64_PATH</c>，使首次 <c>CameraFinder</c> 之前能找到原生组件。
/// 与相机采集无关（VS 直接启动时常缺 pylon Viewer 快捷方式里设的 PATH/GENTL），从
/// <see cref="BaslerCamera"/> 下沉出来，保持适配器只管 connect/grab/参数。
/// </summary>
internal static class PylonRuntimeBootstrap
{
    /// <summary>确保 pylon 原生库可被找到；已配置或未装 pylon 时静默（不阻断进程启动）。</summary>
    public static void EnsureNativePath()
    {
        var runtime = FindRuntimeDir();
        if (runtime is null)
            return;

        PrependPath(runtime);
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("GENICAM_GENTL64_PATH")))
            Environment.SetEnvironmentVariable("GENICAM_GENTL64_PATH", runtime);
    }

    /// <summary>是否已定位到 pylon Runtime\x64（用于初始化失败时的提示）。</summary>
    internal static bool IsRuntimeLocated => FindRuntimeDir() is not null;

    private static void PrependPath(string runtime)
    {
        var path = Environment.GetEnvironmentVariable("PATH") ?? "";
        if (path.IndexOf(runtime, StringComparison.OrdinalIgnoreCase) < 0)
            Environment.SetEnvironmentVariable("PATH", runtime + Path.PathSeparator + path);
    }

    private static string? FindRuntimeDir()
    {
        foreach (var candidate in EnumerateRootCandidates())
        {
            var resolved = ResolveRuntimeDir(candidate);
            if (resolved is not null)
                return resolved;
        }

        var path = Environment.GetEnvironmentVariable("PATH");
        if (!string.IsNullOrWhiteSpace(path))
        {
            foreach (var segment in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
            {
                var trimmed = segment.Trim();
                if (IsRuntimeDir(trimmed))
                    return trimmed;
            }
        }

        return null;
    }

    private static IEnumerable<string?> EnumerateRootCandidates()
    {
        yield return Environment.GetEnvironmentVariable("PYLON_ROOT");
        yield return @"D:\Program Files\Basler\pylon\Runtime\x64";
        yield return @"C:\Program Files\Basler\pylon\Runtime\x64";
        yield return @"D:\Program Files\Basler\pylon";
        yield return @"C:\Program Files\Basler\pylon";

        foreach (var folder in TryReadPylonInstallFolder())
            yield return folder;
    }

    private static IEnumerable<string?> TryReadPylonInstallFolder()
    {
        if (!OperatingSystem.IsWindows())
            yield break;

        string? folder = null;
        try
        {
            using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Basler\pylon");
            folder = key?.GetValue("InstallationFolder") as string;
        }
        catch (Exception)
        {
            // 无 pylon 安装/无注册表权限：视为未定位到，交由上层空枚举
        }

        if (!string.IsNullOrWhiteSpace(folder))
            yield return folder;
    }

    private static string? ResolveRuntimeDir(string? candidate)
    {
        if (string.IsNullOrWhiteSpace(candidate))
            return null;

        var dir = candidate.TrimEnd('\\', '/');
        if (IsRuntimeDir(dir))
            return dir;

        var x64 = Path.Combine(dir, "Runtime", "x64");
        return IsRuntimeDir(x64) ? x64 : null;
    }

    private static bool IsRuntimeDir(string dir) =>
        Directory.Exists(dir) &&
        (File.Exists(Path.Combine(dir, "ProducerGEV.cti")) ||
         File.Exists(Path.Combine(dir, "PylonC_v10.dll")));
}
