using RobotVision.Core.Recipe;
using RobotVision.Hosting;
using RobotVision.Infrastructure.Communication;
using RobotVision.Teach;
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

internal sealed class NoopRecipeSetupAnalysis : IRecipeSetupAnalysisService
{
    public RecipeSetupAnalysisResult AnalyzeGrab(
        RecipeSetupAnalysisRequest request,
        CancellationToken ct = default,
        IProgress<string>? progress = null) =>
        new() { Message = "noop" };

    public RecipeSetupAnalysisResult AnalyzePlayback(
        RecipeSetupAnalysisRequest request,
        CancellationToken ct = default,
        IProgress<string>? progress = null) =>
        new() { Message = "noop" };
}

internal sealed class NullRecipeWindowService : IRecipeWindowService
{
    public bool ShowSetupWizard(RecipeWorkspaceContext workspace) => false;

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

internal sealed class RecordingRecipeSetupAnalysis : IRecipeSetupAnalysisService
{
    public bool LastUsedPlayback { get; private set; }

    public RecipeSetupAnalysisResult NextResult { get; set; } = new() { Message = "recorded" };

    public int AnalyzeGrabCalls { get; private set; }

    public RecipeSetupAnalysisResult AnalyzeGrab(
        RecipeSetupAnalysisRequest request,
        CancellationToken ct = default,
        IProgress<string>? progress = null)
    {
        AnalyzeGrabCalls++;
        LastUsedPlayback = false;
        progress?.Report("grab");
        ct.ThrowIfCancellationRequested();
        return NextResult;
    }

    public RecipeSetupAnalysisResult AnalyzePlayback(
        RecipeSetupAnalysisRequest request,
        CancellationToken ct = default,
        IProgress<string>? progress = null)
    {
        LastUsedPlayback = true;
        progress?.Report("playback");
        ct.ThrowIfCancellationRequested();
        return NextResult;
    }
}

internal sealed class DelayedRecipeSetupAnalysis : IRecipeSetupAnalysisService
{
    public int DelayMs { get; set; } = 400;

    public RecipeSetupAnalysisResult AnalyzeGrab(
        RecipeSetupAnalysisRequest request,
        CancellationToken ct = default,
        IProgress<string>? progress = null)
    {
        Task.Delay(DelayMs, ct).GetAwaiter().GetResult();
        return new RecipeSetupAnalysisResult
        {
            Scene = new SceneDescriptor(
                SceneKind.Silhouette, LightingClass.DarkField, 2, 0.8, 4, 0.2, false, 0, 100, "delayed"),
            Message = "delayed",
        };
    }

    public RecipeSetupAnalysisResult AnalyzePlayback(
        RecipeSetupAnalysisRequest request,
        CancellationToken ct = default,
        IProgress<string>? progress = null) =>
        AnalyzeGrab(request, ct, progress);
}

#pragma warning disable CS0067
internal sealed class FakeTcpRuntime : ITcpRuntime
{
    public bool IsRunning { get; set; }

    public int ConnectedClients { get; set; }

    public string ListenEndPoint { get; set; } = "127.0.0.1:0";

    public long TotalConnections { get; set; }

    public long TotalRequests { get; set; }

    public long RejectedConnections { get; set; }

    public int TimeoutMs { get; set; }

    public long IdleTimeoutMs { get; set; }

    public int Backlog { get; set; }

    public int MaxConnections { get; set; }

    public bool PlcAlwaysOkMode { get; set; }

    public double PlcDebugDefaultX { get; set; }

    public double PlcDebugDefaultY { get; set; }

    public double PlcDebugDefaultRz { get; set; }

    public IReadOnlyList<string> IpWhitelist { get; set; } = [];

    public event Action<TcpClientView>? ClientConnected;

    public event Action<TcpClientView>? ClientDisconnected;

    public event Action<TcpRequestView>? RequestStarted;

    public event Action<TcpRequestView>? RequestProcessed;

    public IReadOnlyList<TcpClientView> GetClients() => [];

    public IReadOnlyList<TcpRequestView> GetRecentRequests() => [];

    public void Start() => IsRunning = true;

    public void Stop() => IsRunning = false;

    public bool Restart(string ipAddress, int port)
    {
        Stop();
        Start();
        return true;
    }

    public void DisconnectClient(long clientId)
    {
    }
}
#pragma warning restore CS0067
