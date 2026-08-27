using System.IO;
using System.Windows;

namespace RobotVision.WpfHost.Shared;

/// <summary>用资源管理器打开本地目录。各功能页共用，避免依赖配方 ViewModel。</summary>
internal static class Explorer
{
    public static void OpenFolder(string path)
    {
        // 目录缺失不静默重建：静默重建会掩盖目录曾被误删的异常
        if (!Directory.Exists(path))
        {
            MessageBox.Show($"目录不存在: {path}\n（目录缺失属异常，请检查是否被移动/误删）",
                "打开目录", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("explorer.exe", $"\"{path}\"")
        {
            UseShellExecute = true,
        });
    }
}
