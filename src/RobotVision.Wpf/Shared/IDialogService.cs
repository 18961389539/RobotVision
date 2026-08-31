namespace RobotVision.WpfHost.Shared;

/// <summary>对话框结果（与 WPF MessageBoxResult 解耦，便于测试）。</summary>
public enum AppDialogResult
{
    None,
    Ok,
    Cancel,
    Yes,
    No,
}

/// <summary>UI 对话框服务：ViewModel 不直接调用 MessageBox / OpenFileDialog。</summary>
public interface IDialogService
{
    void ShowInfo(string message, string title = "RobotVision");
    void ShowWarning(string message, string title = "RobotVision");
    void ShowError(string message, string title = "RobotVision");

    /// <summary>是/否确认；取消或否返回 false。</summary>
    bool ConfirmYesNo(string message, string title, bool warningIcon = true, bool questionIcon = false);

    /// <summary>是/否确认；用于「丢弃未保存修改」等场景，语义同 ConfirmYesNo。</summary>
    bool ConfirmDiscard(string message, string title = "未保存修改") =>
        ConfirmYesNo(message, title, warningIcon: true);

    /// <summary>选择文件；取消返回 null。</summary>
    string? PickOpenFile(string title, string filter, string? initialDirectory = null);

    /// <summary>选择文件夹；取消返回 null。</summary>
    string? PickFolder(string description, string? initialDirectory = null);
}
