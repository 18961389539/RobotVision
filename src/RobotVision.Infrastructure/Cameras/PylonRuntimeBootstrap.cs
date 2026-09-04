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

        var path = Environment.GetEnvironmentVariable("PATH") ?? "";
        if (path.IndexOf(runtime, StringComparison.OrdinalIgnoreCase) < 0)
            Environment.SetEnvironmentVariable("PATH", runtime + Path.PathSeparator + path);

        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("GENICAM_GENTL64_PATH")))
            Environment.SetEnvironmentVariable("GENICAM_GENTL64_PATH", runtime);
    }

    private static string? FindRuntimeDir()
    {
        foreach (var candidate in new[]
        {
            Environment.GetEnvironmentVariable("PYLON_ROOT"),
            @"D:\Program Files\Basler\pylon\Runtime\x64",
            @"C:\Program Files\Basler\pylon\Runtime\x64",
            @"D:\Program Files\Basler\pylon",
            @"C:\Program Files\Basler\pylon",
        })
        {
            var resolved = ResolveRuntimeDir(candidate);
            if (resolved is not null)
                return resolved;
        }

        // 注册表仅 Windows 可用：非 Windows 直接跳过（CA1416 平台守卫）
        if (!OperatingSystem.IsWindows())
            return null;
        try
        {
            using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Basler\pylon");
            return ResolveRuntimeDir(key?.GetValue("InstallationFolder") as string);
        }
        catch (Exception)
        {
            // 无 pylon 安装/无注册表权限：视为未定位到，交由上层空枚举
            return null;
        }
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
