using System.IO;
using System.Windows;
using Microsoft.Win32;
using RobotVision.WpfHost.Features.Recipe;

namespace RobotVision.WpfHost.Shared;

public sealed class WpfDialogService : IDialogService
{
    public void ShowInfo(string message, string title = "RobotVision") =>
        MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);

    public void ShowWarning(string message, string title = "RobotVision") =>
        MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Warning);

    public void ShowError(string message, string title = "RobotVision") =>
        MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error);

    public bool ConfirmYesNo(string message, string title, bool warningIcon = true, bool questionIcon = false)
    {
        var icon = questionIcon ? MessageBoxImage.Question
            : warningIcon ? MessageBoxImage.Warning
            : MessageBoxImage.None;
        return MessageBox.Show(message, title, MessageBoxButton.YesNo, icon) == MessageBoxResult.Yes;
    }

    public string? PickOpenFile(string title, string filter, string? initialDirectory = null)
    {
        var dialog = new OpenFileDialog
        {
            Title = title,
            Filter = filter,
            CheckFileExists = true,
        };
        if (!string.IsNullOrWhiteSpace(initialDirectory) && Directory.Exists(initialDirectory))
            dialog.InitialDirectory = initialDirectory;
        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    public string? PickFolder(string description, string? initialDirectory = null)
    {
        var dialog = new OpenFolderDialog { Title = description };
        if (!string.IsNullOrWhiteSpace(initialDirectory) && Directory.Exists(initialDirectory))
            dialog.InitialDirectory = initialDirectory;
        return dialog.ShowDialog() == true ? dialog.FolderName : null;
    }
}
