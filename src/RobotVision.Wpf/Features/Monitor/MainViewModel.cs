using System.Collections.ObjectModel;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenCvSharp;
using RobotVision.Core;
using RobotVision.Core.Abstractions;
using RobotVision.Core.Models;
using RobotVision.Core.Recipe;
using RobotVision.Hosting;
using RobotVision.Infrastructure.Calibration;
using RobotVision.Infrastructure.Cameras;
using RobotVision.Infrastructure.Communication;

namespace RobotVision.WpfHost.Features.Monitor;

public sealed record PoseRow(int Index, double X, double Y, double AngleDeg, double Score);

public sealed record LogLine(string Time, string Level, string Message)
{
    public string ClipboardText => $"{Time} {Level} {Message}";
}

public partial class MainViewModel : ObservableObject, ICommitPendingEdits, IDisposable
{
    private const int LogCapacity = 500;
    private static readonly TimeSpan PreviewInterval = TimeSpan.FromMilliseconds(500);

    private readonly VisionService _vision;
    private readonly AppConfig _cfg;
    private readonly CameraManager _cameras;
    private readonly CalibrationManager _calibration;
    private readonly RecipeLoader _recipes;
    private readonly TcpServerManager _tcp;
    private readonly LogSink _sink;
    private readonly DispatcherTimer _previewTimer;
    private readonly DispatcherTimer _statusTimer;
    private readonly Random _random = new();

    public Action? FlushPendingEdits { get; set; }

    private bool _showingSnapshot;
    private int _previewBusy;

    [ObservableProperty]
    private bool _includeTriggerPose;

    [ObservableProperty]
    private double _triggerPoseX;

    [ObservableProperty]
    private double _triggerPoseY;

    [ObservableProperty]
    private double _triggerPoseRz;

    [ObservableProperty]
    private BitmapSource? _displayImage;

    [ObservableProperty]
    private string _statusText = "就绪";

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string? _selectedRecipe;

    [ObservableProperty]
    private string? _selectedCamera;

    [ObservableProperty]
    private bool _previewEnabled;

    [ObservableProperty]
    private bool _autoScroll = true;

    /// <summary>监控页面当前是否显示（导航离开时停止预览抓图，避免后台空转）。</summary>
    [ObservableProperty]
    private bool _monitorActive;

    [ObservableProperty]
    private string _tcpStatus = "TCP 未启动";

    /// <summary>TCP 服务是否在运行（底部状态栏圆点指示：绿=运行 / 橙=未启动）。</summary>
    [ObservableProperty]
    private bool _isTcpRunning;

    /// <summary>触发按钮可用性（任务执行中禁用，防止连点并发触发）。</summary>
    [ObservableProperty]
    private bool _canTrigger = true;

    /// <summary>触发按钮文案（执行中显示"触发中..."）。</summary>
    [ObservableProperty]
    private string _triggerButtonText = "手动触发";

    /// <summary>触发结果横幅（成功/错误反馈，远距可读；null = 不显示）。</summary>
    [ObservableProperty]
    private string? _triggerBannerText;

    /// <summary>触发是否成功（决定横幅绿/红底色）。</summary>
    [ObservableProperty]
    private bool _triggerSuccess;

    /// <summary>连续失败联锁提示（1018）；空 = 不显示横幅。</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasInterlock))]
    private string _interlockText = "";

    public bool HasInterlock => !string.IsNullOrEmpty(InterlockText);

    /// <summary>监控页参数浮动面板展开状态（图像主导布局，与相机/配方页同构）。</summary>
    [ObservableProperty]
    private bool _isParamPanelVisible = true;

    [RelayCommand]
    private void ToggleParamPanel() => IsParamPanelVisible = !IsParamPanelVisible;

    /// <summary>监控页日志浮动面板展开状态。</summary>
    [ObservableProperty]
    private bool _isLogPanelVisible = true;

    [RelayCommand]
    private void ToggleLogPanel() => IsLogPanelVisible = !IsLogPanelVisible;

    [RelayCommand]
    private void ClearInhibit()
    {
        _vision.ClearInhibit();
        RefreshInterlock();
    }

    private void RefreshInterlock()
    {
        if (!_vision.AnyInhibited)
        {
            InterlockText = "";
            return;
        }

        var limit = Math.Max(1, _vision.ConsecutiveFailLimit);
        var locked = _vision.GetRecipeStats()
            .Where(s => s.ConsecutiveFails >= limit)
            .Select(s => $"{s.Recipe}×{s.ConsecutiveFails}")
            .ToList();
        InterlockText = locked.Count == 0
            ? "连续失败联锁已触发（1018）。排除现场问题后点「解除联锁」。"
            : $"连续失败联锁：{string.Join("、", locked)}。TRIGGER 返回 1018。";
    }

    /// <summary>日志级别过滤选项。</summary>
    public IReadOnlyList<string> LogFilterOptions { get; } = ["全部", "警告及错误", "仅错误"];

    /// <summary>当前日志过滤（全部 / 警告及错误 / 仅错误）。</summary>
    [ObservableProperty]
    private string _logFilter = "全部";

    /// <summary>全量日志（过滤前的完整队列，过滤切换时重放）。</summary>
    private readonly List<LogLine> _allLogs = [];

    public ObservableCollection<string> Recipes { get; } = [];

    public ObservableCollection<CameraOption> CameraOptions { get; } = [];

    public ObservableCollection<PoseRow> Poses { get; } = [];

    public bool HasPoseResults => Poses.Count > 0;

    public ObservableCollection<LogLine> Logs { get; } = [];

    public MainViewModel(
        VisionService vision,
        AppConfig cfg,
        CameraManager cameras,
        CalibrationManager calibration,
        RecipeLoader recipes,
        TcpServerManager tcp,
        LogSink sink)
    {
        _vision = vision;
        _cfg = cfg;
        _cameras = cameras;
        _calibration = calibration;
        _recipes = recipes;
        _tcp = tcp;
        _sink = sink;

        foreach (var option in BuildCameraOptions())
            CameraOptions.Add(option);
        SelectedCamera = ResolveDefaultMonitorCamera(null);

        RefreshRecipes();

        Poses.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasPoseResults));

        _vision.FrameProcessed += OnFrameProcessed;
        _sink.EntryAdded += OnLogEntry;
        foreach (var entry in _sink.Snapshot())
            AppendLog(entry);

        _previewTimer = new DispatcherTimer { Interval = PreviewInterval };
        _previewTimer.Tick += (_, _) => _ = GrabPreviewAsync();
        _previewTimer.Start();

        _statusTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _statusTimer.Tick += (_, _) =>
        {
            IsTcpRunning = _tcp.IsRunning;
            TcpStatus = $"TCP {_tcp.ConnectedClients} 客户端 · 队列 {_vision.QueueDepth}/{_vision.MaxQueueDepth}";
            RefreshInterlock();
        };
        _statusTimer.Start();
        RefreshInterlock();
    }

    /// <summary>重建相机下拉（保留仍存在的选中项）；相机管理页增删相机后由 MonitorPage Loaded 调用。</summary>
    public void RefreshCameras()
    {
        var current = SelectedCamera;
        CameraOptions.Clear();
        foreach (var option in BuildCameraOptions())
            CameraOptions.Add(option);
        SelectedCamera = current is not null && CameraOptions.Any(o =>
                string.Equals(o.Id, current, StringComparison.OrdinalIgnoreCase))
            ? current
            : current is null
                ? ResolveDefaultMonitorCamera(null)
                : null;
    }

    private IReadOnlyList<CameraOption> BuildCameraOptions() =>
        CameraOption.FromRegistered(_cfg.Cameras, _cameras.CameraIds);

    /// <summary>监控页默认相机：优先 Virtual，其次 File，最后列表首项（无硬件时不默认 Basler）。</summary>
    private string? ResolveDefaultMonitorCamera(string? preferred)
    {
        if (!string.IsNullOrEmpty(preferred) && CameraOptions.Any(o =>
                string.Equals(o.Id, preferred, StringComparison.OrdinalIgnoreCase)))
            return preferred;

        foreach (var option in CameraOptions)
        {
            if (_cameras.TryGet(option.Id, out var camera) && camera?.Kind == CameraKind.Virtual)
                return option.Id;
        }

        foreach (var option in CameraOptions)
        {
            if (_cameras.TryGet(option.Id, out var camera) && camera?.Kind == CameraKind.File)
                return option.Id;
        }

        return CameraOptions.FirstOrDefault()?.Id;
    }

    [RelayCommand]
    private void RefreshRecipes()
    {
        var current = SelectedRecipe;
        Recipes.Clear();
        foreach (var name in _recipes.ListNames())
            Recipes.Add(name);
        if (!string.IsNullOrEmpty(current) && Recipes.Contains(current))
            SelectedRecipe = current;
        else if (string.IsNullOrEmpty(current))
            SelectedRecipe = Recipes.FirstOrDefault();
        else
            SelectedRecipe = null;
    }

    [RelayCommand]
    private void ResumePreview()
    {
        _showingSnapshot = false;
        TriggerBannerText = null; // 恢复预览时清除上次触发结果横幅
        StatusText = $"预览中 · {SelectedCamera ?? "-"}";
    }

    [RelayCommand]
    private async Task TriggerAsync()
    {
        this.Commit();
        var recipeName = SelectedRecipe;
        if (string.IsNullOrEmpty(recipeName) || IsBusy)
            return;

        IsBusy = true;
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(Math.Max(500, _tcp.TimeoutMs)));
            TcpClientPose? pose = IncludeTriggerPose
                ? new TcpClientPose(TriggerPoseX, TriggerPoseY, TriggerPoseRz)
                : null;
            var result = pose is null
                ? await _vision.RunAsync(recipeName, cts.Token)
                : await _vision.RunAsync(recipeName, pose, cts.Token);

            if (!result.Ok)
            {
                Poses.Clear();
                StatusText = $"ERR {result.ErrorCode} · {result.Message}";
                TriggerBannerText = $"ERR {result.ErrorCode} · {result.Message}";
                TriggerSuccess = false;
                return;
            }

            Poses.Clear();
            for (var i = 0; i < result.Poses.Count; i++)
            {
                var robotPose = result.Poses[i];
                var score = i < result.Confidences.Count ? result.Confidences[i] : 0;
                Poses.Add(new PoseRow(i + 1, robotPose.X, robotPose.Y, robotPose.AngleDeg, score));
            }

            StatusText = $"{result.RecipeName} · {result.Poses.Count} 个目标 · {result.ElapsedMs:0}ms";
            TriggerBannerText = $"检出 {result.Poses.Count} 个目标 · 配方 {result.RecipeName} · {result.ElapsedMs:0}ms";
            TriggerSuccess = true;
        }
        catch (Exception ex)
        {
            StatusText = $"触发异常: {ex.Message}";
            TriggerBannerText = $"触发异常: {ex.Message}";
            TriggerSuccess = false;
        }
        finally
        {
            IsBusy = false;
        }
    }

    partial void OnIsBusyChanged(bool value)
    {
        // 触发按钮防误触：执行中禁用并显示"触发中..."
        CanTrigger = !value;
        TriggerButtonText = value ? "触发中..." : "手动触发";
    }

    [RelayCommand]
    private void ClearLog()
    {
        _allLogs.Clear();
        Logs.Clear();
    }

    /// <summary>日志级别过滤：全部 / 警告及错误（Warning/Error/Critical）/ 仅错误（Error/Critical）。</summary>
    private static bool MatchesFilter(LogLine line, string filter)
    {
        if (filter == "全部")
            return true;
        var isError = string.Equals(line.Level, "Error", StringComparison.OrdinalIgnoreCase)
            || string.Equals(line.Level, "Critical", StringComparison.OrdinalIgnoreCase);
        if (filter == "仅错误")
            return isError;
        return isError || string.Equals(line.Level, "Warning", StringComparison.OrdinalIgnoreCase);
    }

    partial void OnLogFilterChanged(string value) => RebuildLogs();

    /// <summary>按当前过滤器重放全量日志（切换过滤即时生效）。</summary>
    private void RebuildLogs()
    {
        Logs.Clear();
        foreach (var line in _allLogs)
        {
            if (MatchesFilter(line, LogFilter))
                Logs.Add(line);
        }
    }

    partial void OnPreviewEnabledChanged(bool value)
    {
        if (value)
            _showingSnapshot = false;
    }

    private void HandlePreviewFailure(string statusMessage)
    {
        PreviewEnabled = false;
        StatusText = statusMessage + " · 已自动停止预览";
    }

    private async Task GrabPreviewAsync()
    {
        if (!PreviewEnabled || !MonitorActive || _showingSnapshot || IsBusy || SelectedCamera is null)
            return;

        if (Interlocked.Exchange(ref _previewBusy, 1) == 1)
            return;

        var cameraId = SelectedCamera;
        try
        {
            var recipeName = SelectedRecipe;
            var image = await Task.Run(() =>
            {
                var frame = _cameras.Grab(cameraId!);
                try
                {
                    string? stationId = null;
                    if (!string.IsNullOrEmpty(recipeName))
                    {
                        try { stationId = _recipes.Get(recipeName).StationId; }
                        catch { /* 预览不因配方无效失败 */ }
                    }

                    if (!string.IsNullOrEmpty(stationId) && _calibration.HasPolynomial(stationId))
                        return frame.Image.Clone();
                    if (_calibration.IsCalibrated(cameraId))
                        return _calibration.Undistort(cameraId, frame.Image);
                    return frame.Image.Clone();
                }
                finally
                {
                    frame.Dispose();
                }
            });

            BitmapSource source;
            try
            {
                source = ImageConverter.ToBitmapSource(image);
            }
            finally
            {
                image.Dispose();
            }

            UiDispatch.Begin(() =>
            {
                if (_showingSnapshot)
                    return;
                DisplayImage = source;
                StatusText = $"预览中 · {cameraId}";
            });
        }
        catch (VisionException vex)
        {
            UiDispatch.Begin(() => HandlePreviewFailure(FormatPreviewError(cameraId!, vex)));
        }
        catch (Exception ex)
        {
            UiDispatch.Begin(() => HandlePreviewFailure($"预览失败 · {cameraId}: {ex.Message}"));
        }
        finally
        {
            Volatile.Write(ref _previewBusy, 0);
        }
    }

    private static string FormatPreviewError(string cameraId, VisionException vex)
    {
        var hint = vex.Message.Contains("GigE Vision", StringComparison.OrdinalIgnoreCase)
            ? " · 建议相机页将类型改为 Basler，并通过「枚举设备」选择本机相机"
            : vex.Message.Contains("自动重连未恢复", StringComparison.OrdinalIgnoreCase)
                || vex.Message.Contains("未发现 Basler 相机", StringComparison.OrdinalIgnoreCase)
                ? " · 请检查 pylon/网线/上电，或改用 cam_virtual / cam_file"
                : "";
        return $"预览失败 · {cameraId} ({(int)vex.ErrorCode}): {vex.Message}{hint}";
    }

    /// <summary>管线线程回调：同步绘制叠加并转换，再封送到 UI 线程更新显示。</summary>
    private void OnFrameProcessed(VisionFrameSnapshot snapshot)
    {
        BitmapSource source;
        try
        {
            using (snapshot.UndistortedImage)
            {
                OverlayDrawer.DrawPoses(snapshot.UndistortedImage, snapshot.Poses);
                source = ImageConverter.ToBitmapSource(snapshot.UndistortedImage);
            }

            var count = snapshot.Poses.Count;
            var recipeName = snapshot.RecipeName;
            UiDispatch.Begin(() =>
            {
                _showingSnapshot = true;
                DisplayImage = source;
                StatusText = count > 0
                    ? $"结果 · {recipeName} · {count} 个目标"
                    : $"结果 · {recipeName} · 未检出目标";
            });
        }
        catch (Exception)
        {
            // 绘制失败不影响管线；快照图像由 using 释放
        }
    }

    private void OnLogEntry(LogEntry entry) =>
        UiDispatch.Begin(() => AppendLog(entry));

    private void AppendLog(LogEntry entry)
    {
        var line = new LogLine(
            entry.Time.ToString("HH:mm:ss"), entry.Level.ToString(), entry.Message);
        _allLogs.Add(line);
        while (_allLogs.Count > LogCapacity)
            _allLogs.RemoveAt(0);
        if (MatchesFilter(line, LogFilter))
        {
            Logs.Add(line);
            while (Logs.Count > LogCapacity)
                Logs.RemoveAt(0);
        }
    }

    public void Dispose()
    {
        _vision.FrameProcessed -= OnFrameProcessed;
        _sink.EntryAdded -= OnLogEntry;
        _previewTimer.Stop();
        _statusTimer.Stop();
    }
}
