using System.IO;

namespace RobotVision.WpfHost.Shared;

/// <summary>用资源管理器打开本地目录。各功能页共用，避免依赖配方 ViewModel。</summary>
internal static class Explorer
{
    public static void OpenFolder(string path, IDialogService? dialogs = null)
    {
        if (!Directory.Exists(path))
        {
            dialogs?.ShowWarning(
                $"目录不存在: {path}\n（目录缺失属异常，请检查是否被移动/误删）",
                "打开目录");
            return;
        }
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("explorer.exe", $"\"{path}\"")
        {
            UseShellExecute = true,
        });
    }
}
