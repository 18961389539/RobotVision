using RobotVision.WpfHost.Features.Recipe;
using RobotVision.WpfHost.Shared;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace RobotVision.Wpf.Tests;

internal static class TestLog
{
    public static ILogger<T> Null<T>() => NullLogger<T>.Instance;
}

internal sealed class TestDialogService : IDialogService
{
    public bool ConfirmYesNoResult { get; set; } = true;

    public bool ConfirmDiscardResult { get; set; } = true;

    public string? PickFolderResult { get; set; }

    public string? PickOpenFileResult { get; set; }

    public List<string> Warnings { get; } = [];

    public void ShowInfo(string message, string title = "RobotVision") { }

    public void ShowWarning(string message, string title = "RobotVision") => Warnings.Add(message);

    public void ShowError(string message, string title = "RobotVision") { }

    public bool ConfirmYesNo(string message, string title, bool warningIcon = true, bool questionIcon = false) =>
        ConfirmYesNoResult;

    public bool ConfirmDiscard(string message, string title = "未保存修改") =>
        ConfirmDiscardResult;

    public string? PickOpenFile(string title, string filter, string? initialDirectory = null) =>
        PickOpenFileResult;

    public string? PickFolder(string description, string? initialDirectory = null) =>
        PickFolderResult;
}

internal sealed class NullRecipeWindowService : IRecipeWindowService
{
    public bool ShowSetupWizard(
        IRecipeWorkspace host,
        Hosting.ICameraRuntime cameras,
        Hosting.IModelRuntime models,
        Hosting.ICalibrationRuntime calibration,
        Hosting.ILightingRuntime lighting,
        RecipeRoiEditor roi,
        RecipeTestSession test) => false;

    public bool ShowRefineDetails(RecipeViewModel host, out bool requestTemplateRoiDraw)
    {
        requestTemplateRoiDraw = false;
        return false;
    }
}

internal sealed class NullHtmlPreviewService : IHtmlPreviewService
{
    public string? LastHtml { get; private set; }

    public void Show(string html, string title = "HTML 预览（模型生成内容，脚本已禁用）") =>
        LastHtml = html;
}
