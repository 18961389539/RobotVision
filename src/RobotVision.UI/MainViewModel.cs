using System.Collections.ObjectModel;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenCvSharp;
using RobotVision.Core.Models;
using RobotVision.Core.Recipe;
using RobotVision.Hosting;
using RobotVision.Infrastructure.Calibration;
using RobotVision.Infrastructure.Cameras;
using RobotVision.Infrastructure.Communication;

namespace RobotVision.UI;

public sealed record PoseRow(int Index, double X, double Y, double AngleDeg);

public sealed record LogLine(string Time, string Level, string Message);

public partial class MainViewModel : ObservableObject, IDisposable
{
    private const int LogCapacity = 500;
    private static readonly TimeSpan PreviewInterval = TimeSpan.FromMilliseconds(500);
    private static readonly TimeSpan TriggerTimeout = TimeSpan.FromSeconds(10);

    private readonly VisionService _vision;
    private readonly CameraManager _cameras;
    private readonly CalibrationManager _calibration;
    private readonly RecipeLoader _recipes;
    private readonly TcpServerManager _tcp;
    private readonly LogSink _sink;
    private readonly DispatcherTimer _previewTimer;
    private readonly DispatcherTimer _statusTimer;
    private readonly Random _random = new();

    private bool _showingSnapshot;
    private int _previewBusy;

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
    private bool _previewEnabled = true;

    [ObservableProperty]
    private bool _autoScroll = true;

    /// <summary>监控页面当前是否显示（导航离开时停止预览抓图，避免后台空转）。</summary>
    [ObservableProperty]
    private bool _monitorActive;

    [ObservableProperty]
    private string _tcpStatus = "TCP 未启动";

    public ObservableCollection<string> Recipes { get; } = [];

    public ObservableCollection<string> Cameras { get; } = [];

    public ObservableCollection<PoseRow> Poses { get; } = [];

    public ObservableCollection<LogLine> Logs { get; } = [];

    public MainViewModel(
        VisionService vision,
        CameraManager cameras,
        CalibrationManager calibration,
        RecipeLoader recipes,
        TcpServerManager tcp,
        LogSink sink)
    {
        _vision = vision;
        _cameras = cameras;
        _calibration = calibration;
        _recipes = recipes;
        _tcp = tcp;
        _sink = sink;

        foreach (var camera in cameras.CameraIds)
            Cameras.Add(camera);
        SelectedCamera = Cameras.FirstOrDefault();

        RefreshRecipes();

        _vision.FrameProcessed += OnFrameProcessed;
        _sink.EntryAdded += OnLogEntry;
        foreach (var entry in _sink.Snapshot())
            AppendLog(entry);

        _previewTimer = new DispatcherTimer { Interval = PreviewInterval };
        _previewTimer.Tick += (_, _) => _ = GrabPreviewAsync();
        _previewTimer.Start();

        _statusTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _statusTimer.Tick += (_, _) => TcpStatus =
            $"TCP {_tcp.ConnectedClients} 客户端 · 队列 {_vision.QueueDepth}/{_vision.MaxQueueDepth}";
        _statusTimer.Start();
    }

    /// <summary>重建相机下拉（保留仍存在的选中项）；相机管理页增删相机后由 MonitorPage Loaded 调用。</summary>
    public void RefreshCameras()
    {
        var current = SelectedCamera;
        Cameras.Clear();
        foreach (var camera in _cameras.CameraIds)
            Cameras.Add(camera);
        SelectedCamera = current is not null && Cameras.Contains(current) ? current : Cameras.FirstOrDefault();
    }

    [RelayCommand]
    private void RefreshRecipes()
    {
        Recipes.Clear();
        foreach (var name in _recipes.ListNames())
            Recipes.Add(name);
        if (SelectedRecipe is null || !Recipes.Contains(SelectedRecipe))
            SelectedRecipe = Recipes.FirstOrDefault();
    }

    [RelayCommand]
    private void ResumePreview()
    {
        _showingSnapshot = false;
        StatusText = $"预览中 · {SelectedCamera ?? "-"}";
    }

    [RelayCommand]
    private async Task TriggerAsync()
    {
        var recipeName = SelectedRecipe;
        if (string.IsNullOrEmpty(recipeName) || IsBusy)
            return;

        IsBusy = true;
        try
        {
            using var cts = new CancellationTokenSource(TriggerTimeout);
            var result = await _vision.RunAsync(recipeName, cts.Token);

            if (!result.Ok)
            {
                Poses.Clear();
                StatusText = $"ERR {result.ErrorCode} · {result.Message}";
                return;
            }

            Poses.Clear();
            for (var i = 0; i < result.Poses.Count; i++)
            {
                var pose = result.Poses[i];
                Poses.Add(new PoseRow(i + 1, pose.X, pose.Y, pose.AngleDeg));
            }

            StatusText = $"{result.RecipeName} · {result.Poses.Count} 个目标 · {result.ElapsedMs:0}ms";
        }
        catch (Exception ex)
        {
            StatusText = $"触发异常: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void ClearLog() => Logs.Clear();

    partial void OnPreviewEnabledChanged(bool value)
    {
        if (value)
            _showingSnapshot = false;
    }

    private async Task GrabPreviewAsync()
    {
        if (!PreviewEnabled || !MonitorActive || _showingSnapshot || IsBusy || SelectedCamera is null)
            return;

        if (Interlocked.Exchange(ref _previewBusy, 1) == 1)
            return;

        try
        {
            var cameraId = SelectedCamera;
            var image = await Task.Run(() =>
            {
                var frame = _cameras.Grab(cameraId);
                try
                {
                    return _calibration.Undistort(cameraId, frame.Image);
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

            _ = App.Current.Dispatcher.BeginInvoke(() =>
            {
                if (_showingSnapshot)
                    return;
                DisplayImage = source;
                StatusText = $"预览中 · {cameraId}";
            });
        }
        catch (Exception ex)
        {
            _ = App.Current.Dispatcher.BeginInvoke(() => StatusText = $"预览失败: {ex.Message}");
        }
        finally
        {
            Volatile.Write(ref _previewBusy, 0);
        }
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
            App.Current.Dispatcher.BeginInvoke(() =>
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
        App.Current.Dispatcher.BeginInvoke(() => AppendLog(entry));

    private void AppendLog(LogEntry entry)
    {
        Logs.Add(new LogLine(
            entry.Time.ToString("HH:mm:ss"), entry.Level.ToString(), entry.Message));
        while (Logs.Count > LogCapacity)
            Logs.RemoveAt(0);
    }

    public void Dispose()
    {
        _vision.FrameProcessed -= OnFrameProcessed;
        _sink.EntryAdded -= OnLogEntry;
        _previewTimer.Stop();
        _statusTimer.Stop();
    }
}
