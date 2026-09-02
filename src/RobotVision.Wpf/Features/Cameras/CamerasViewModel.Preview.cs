using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RobotVision.Core.Abstractions;
using RobotVision.Core.Recipe;
using RobotVision.Hosting;
using RobotVision.Hosting.Cameras;
using RobotVision.WpfHost.Shared;

namespace RobotVision.WpfHost.Features.Cameras;
public partial class CamerasViewModel
{
    /// <summary>取图/调光操作未进行中（防抖，避免并发临时相机实例）。</summary>
    private bool CanOperate => !IsBusy;

    partial void OnIsBusyChanged(bool value)
    {
        TestGrabCommand.NotifyCanExecuteChanged();
        ApplyAndGrabCommand.NotifyCanExecuteChanged();
        TogglePreviewCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand(CanExecute = nameof(CanOperate))]
    private async Task TestGrabAsync()
    {
        var id = EditId.Trim();
        if (id.Length == 0)
        {
            Message = "测试取图：请先填写 Id";
            return;
        }

        CameraConfig entry;
        try
        {
            entry = BuildConfig(id);
        }
        catch (FormatException ex)
        {
            Message = $"测试取图: {ex.Message}";
            return;
        }

        IsBusy = true;
        try
        {
            var label = PreviewCameraLabel();
            Message = $"取图中 · {label}";
            var snap = await Task.Run(() => GrabFrameSnapshot(entry, id));
            ApplyPreviewSnapshot("测试取图", label, snap);
            Message = $"取图成功 · {label} · {snap.Width}×{snap.Height} · {snap.ElapsedMs:0} ms";
        }
        catch (Exception ex)
        {
            ClearPreview();
            Message = $"取图失败: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanOperate))]
    private void TogglePreview()
    {
        if (IsPreviewing)
            StopPreview();
        else
            StartPreview();
    }

    private void StartPreview()
    {
        var id = EditId.Trim();
        if (id.Length == 0)
        {
            Message = "实时预览：请先填写 Id";
            return;
        }

        EndPreviewSession(updateMessage: false);
        _previewCts = new CancellationTokenSource();
        IsPreviewing = true;
        _previewFrameTicks.Clear();
        Message = $"实时预览中 · {PreviewCameraLabel()}";
        _previewTimer.Start();
    }

    /// <summary>停止实时预览。页面离开时（Unloaded）也必须调用——定时器属于进程级单例
    /// ViewModel，不随页面销毁，不停就会在后台持续 Grab 占用相机。</summary>
    public void StopPreview() => EndPreviewSession(updateMessage: true);

    private void EndPreviewSession(bool updateMessage)
    {
        _previewTimer.Stop();
        var wasPreviewing = IsPreviewing;
        IsPreviewing = false;

        CancelPreviewCts();
        DisposePreviewSession();
        _previewTickBusy = false;
        _previewFrameTicks.Clear();

        if (updateMessage && wasPreviewing)
            Message = "已停止实时预览";
    }

    private void CancelPreviewCts()
    {
        if (_previewCts is null)
            return;
        try
        {
            _previewCts.Cancel();
        }
        catch (ObjectDisposedException)
        {
        }

        _previewCts.Dispose();
        _previewCts = null;
    }

    /// <summary>页面 Unload / 进程退出时停预览循环并释放会话相机。</summary>
    public void Dispose()
    {
        StopPreview();
    }

    private void DisposePreviewSession()
    {
        lock (_previewSessionLock)
        {
            DisposePreviewSessionCore();
        }
    }

    private void DisposePreviewSessionCore()
    {
        _previewSessionCamera?.Dispose();
        _previewSessionCamera = null;
        _previewSessionId = null;
        _previewSessionConfig = null;
    }

    /// <summary>定时取帧：已注册相机用运行时实例，未注册按编辑内容临时构造（与测试取图一致）。</summary>
    private void OnPreviewTick(object? sender, EventArgs e)
    {
        // 异常由 OnPreviewTickAsync 全捕获；未观察异常由 UiFireAndForget 记录。
        var tick = OnPreviewTickAsync();
        _previewInFlightTask = tick;
        UiFireAndForget.Run(tick, _log);
    }

    private async Task OnPreviewTickAsync()
    {
        // IsBusy（手动取图/调光进行中）或上一帧未完成时跳过，避免并发 Grab 与堆帧
        if (_previewTickBusy || IsBusy || !IsPreviewing)
            return;
        _previewTickBusy = true;
        try
        {
            var id = EditId.Trim();
            if (id.Length == 0)
            {
                StopPreview();
                Message = "实时预览：请先填写 Id";
                return;
            }

            CameraConfig entry;
            try
            {
                entry = BuildConfig(id);
            }
            catch (FormatException ex)
            {
                StopPreview();
                Message = $"实时预览: {ex.Message}";
                return;
            }

            var label = PreviewCameraLabel();
            var ct = _previewCts?.Token ?? CancellationToken.None;
            var snap = await Task.Run(() => GrabFrameSnapshot(entry, id, ct, reusePreviewSession: true), ct)
                .ConfigureAwait(true);
            if (ct.IsCancellationRequested || !IsPreviewing)
                return;

            RecordPreviewFrameTiming();
            var (fps, avgMs) = ComputePreviewRates();
            ApplyPreview(
                snap,
                FormatLivePreviewCaption(label, snap.Width, snap.Height, snap.ElapsedMs, fps, avgMs));
        }
        catch (OperationCanceledException) when (!IsPreviewing || _previewCts?.IsCancellationRequested == true)
        {
            // 用户停止预览或页面卸载：静默丢弃在途帧，不覆盖角标/预览图。
        }
        catch (Exception ex)
        {
            ClearPreview();
            StopPreview();
            Message = $"实时预览失败: {ex.Message}";
        }
        finally
        {
            _previewTickBusy = false;
        }
    }

    private void RecordPreviewFrameTiming()
    {
        _previewFrameTicks.Enqueue(Environment.TickCount64);
        while (_previewFrameTicks.Count > PreviewFpsSampleCount)
            _previewFrameTicks.Dequeue();
    }

    private (double? Fps, double? AvgMs) ComputePreviewRates()
    {
        if (_previewFrameTicks.Count < 2)
            return (null, null);

        var span = _previewFrameTicks.Last() - _previewFrameTicks.First();
        if (span <= 0)
            return (null, null);

        var intervals = _previewFrameTicks.Count - 1;
        return (intervals * 1000.0 / span, span / intervals);
    }

    private sealed record GrabSnapshot(
        BitmapSource Image,
        int Width,
        int Height,
        double ElapsedMs,
        DateTime CapturedAtLocal);

    private string PreviewCameraLabel()
    {
        var id = EditId.Trim();
        var name = EditName.Trim();
        return name.Length > 0 ? $"{name} ({id})" : id;
    }

    private GrabSnapshot GrabFrameSnapshot(
        CameraConfig entry,
        string id,
        CancellationToken ct = default,
        bool reusePreviewSession = false)
    {
        ct.ThrowIfCancellationRequested();
        var sw = Stopwatch.StartNew();
        if (_cameras.TryGet(id, out var existing) && existing is not null)
        {
            if (reusePreviewSession)
            {
                lock (_previewSessionLock)
                    DisposePreviewSessionCore();
            }
            using var registeredFrame = _cameras.Grab(id, ct);
            sw.Stop();
            return ToGrabSnapshot(registeredFrame, sw.Elapsed.TotalMilliseconds);
        }

        ICamera camera;
        var ownsSessionCamera = false;
        lock (_previewSessionLock)
        {
            camera = reusePreviewSession
                ? GetOrCreatePreviewSessionCameraCore(entry, id)
                : CreateCamera(entry);
            ownsSessionCamera = !reusePreviewSession;
        }

        try
        {
            using var tempFrame = _cameras.Grab(camera, ct);
            sw.Stop();
            return ToGrabSnapshot(tempFrame, sw.Elapsed.TotalMilliseconds);
        }
        finally
        {
            if (ownsSessionCamera)
                camera.Dispose();
        }
    }

    private ICamera GetOrCreatePreviewSessionCameraCore(CameraConfig entry, string id)
    {
        if (_previewSessionCamera is not null &&
            string.Equals(_previewSessionId, id, StringComparison.OrdinalIgnoreCase) &&
            _previewSessionConfig is not null &&
            SameCamera(_previewSessionConfig, entry))
            return _previewSessionCamera;

        DisposePreviewSessionCore();
        _previewSessionCamera = CreateCamera(entry);
        _previewSessionId = id;
        _previewSessionConfig = CloneCameraConfig(entry);
        return _previewSessionCamera;
    }

    private static CameraConfig CloneCameraConfig(CameraConfig source) => new()
    {
        Id = source.Id,
        Name = source.Name,
        Type = source.Type,
        Folder = source.Folder,
        IntervalMs = source.IntervalMs,
        DeviceId = source.DeviceId,
        ExposureTimeUs = source.ExposureTimeUs,
        Gain = source.Gain,
        GrabTimeoutMs = source.GrabTimeoutMs,
        Width = source.Width,
        Height = source.Height,
        Pattern = source.Pattern,
        NoiseSigma = source.NoiseSigma,
        ChessCellPx = source.ChessCellPx,
    };

    private static GrabSnapshot ToGrabSnapshot(CameraFrame frame, double elapsedMs)
    {
        var image = frame.Image;
        var source = ImageConverter.ToBitmapSource(image);
        return new GrabSnapshot(
            source,
            image.Width,
            image.Height,
            elapsedMs,
            frame.CapturedAtUtc.ToLocalTime());
    }

    private void ApplyPreviewSnapshot(string mode, string label, GrabSnapshot snap) =>
        ApplyPreview(snap, FormatSingleGrabCaption(mode, label, snap.Width, snap.Height, snap.ElapsedMs));

    private void ApplyPreview(GrabSnapshot snap, string caption)
    {
        PreviewImage = snap.Image;
        PreviewCaption = caption;
        PreviewToolTip = FormatCaptureToolTip(snap.CapturedAtLocal);
    }

    private void ClearPreview()
    {
        PreviewImage = null;
        PreviewCaption = "";
        PreviewToolTip = "";
    }

    private static string FormatSingleGrabCaption(string mode, string label, int width, int height, double elapsedMs) =>
        $"{mode} · {label} · {width}×{height} · {elapsedMs:0} ms";

    private static string FormatLivePreviewCaption(
        string label,
        int width,
        int height,
        double elapsedMs,
        double? fps,
        double? avgMs)
    {
        var parts = new List<string> { "实时预览", label, $"{width}×{height}" };
        if (fps is not null && avgMs is not null)
        {
            parts.Add($"{fps:0.#} fps");
            parts.Add($"{avgMs:0} ms/帧");
        }
        else
            parts.Add($"{elapsedMs:0} ms");

        return string.Join(" · ", parts);
    }

    private static string FormatCaptureToolTip(DateTime capturedAtLocal) =>
        $"采集时刻 {capturedAtLocal:yyyy-MM-dd HH:mm:ss.fff}";
}
