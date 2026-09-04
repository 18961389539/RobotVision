using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using GenICam.Net.GenApi;
using GenICam.Net.GigEVision.Gvcp;
using GenICam.Net.GigEVision.Gvsp;
using Microsoft.Extensions.Logging;
using OpenCvSharp;
using RobotVision.Core;
using RobotVision.Core.Abstractions;
using RobotVision.Core.Models;
using RobotVision.Infrastructure;
using ICamera = RobotVision.Core.Abstractions.ICamera;

namespace RobotVision.Infrastructure.Cameras;

/// <summary>
/// 开源 GigE Vision 相机（GigEVision.Net：GVCP 控制 + GVSP 收流）。
/// 不依赖 pylon；本机防火墙需放行 UDP 3956（控制）与流端口。
/// DeviceId 可填序列号、IP 或 MAC；留空仅当发现恰好一台时绑定，多台必须填写。
/// 连接后、开流前下发 2×2 全图降采样（binning，否则 decimation）。
/// </summary>
public sealed class GigEVisionCamera : ICamera, IExposureControl, IHardware2x2Output
{
    private static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(15);

    private readonly string _deviceId;
    private readonly int _grabTimeoutMs;
    private readonly double? _exposureTimeUs;
    private readonly double? _gain;
    private readonly ILogger? _log;
    private readonly object _grabLock = new();
    private readonly GvspDisplayConverter _display = new();

    private IGigECameraSession? _session;
    private GvspStreamSession? _stream;
    private TaskCompletionSource<GvspFrame>? _pendingFrame;
    private volatile bool _disposed;
    private volatile bool _connected;
    private bool _hardware2x2;
    private int _outputWidth;
    private int _outputHeight;
    private string _serialNumber = "";
    private string _friendlyName = "";

    public string Id { get; }

    public CameraKind Kind => CameraKind.Real;

    bool IHardware2x2Output.HasHardware2x2 => _hardware2x2;

    int IHardware2x2Output.ExpectedWidth => _outputWidth;

    int IHardware2x2Output.ExpectedHeight => _outputHeight;

    public string SerialNumber => _serialNumber;

    public string FriendlyName => _friendlyName;

    public GigEVisionCamera(string id, string? deviceId = null, double? exposureTimeUs = null,
        double? gain = null, int grabTimeoutMs = 60_000, ILogger? log = null)
    {
        Id = id;
        _deviceId = deviceId?.Trim() ?? "";
        _exposureTimeUs = exposureTimeUs;
        _gain = gain;
        _grabTimeoutMs = grabTimeoutMs > 0 ? grabTimeoutMs : 60_000;
        _log = log;
    }

    public bool TryConnectOnce()
    {
        lock (_grabLock)
        {
            if (_disposed)
                return false;
            return ConnectCore();
        }
    }

    [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope",
        Justification = "VisionImage ownership transfers to CameraFrame.")]
    public CameraFrame Grab(CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ct.ThrowIfCancellationRequested();

        for (var attempt = 0; attempt < 2; attempt++)
        {
            lock (_grabLock)
            {
                if (_disposed)
                    throw new VisionException(VisionErrorCode.CameraGrabFailed, $"GigE Vision 相机 {Id} 已释放");

                var needConnect = !_connected || _session is null || _stream is null || attempt > 0;
                if (needConnect && !ConnectCore())
                {
                    if (attempt == 0)
                        continue;
                    break;
                }
            }

            try
            {
                var grabWatch = Stopwatch.StartNew();
                // 等帧不占 _grabLock：取消/Dispose/调光可在超时预算内插进，不必等满 grabTimeout。
                var frame = WaitForFrame(ct);
                var acquireMs = grabWatch.Elapsed.TotalMilliseconds;
                lock (_grabLock)
                {
                    if (_disposed)
                        throw new VisionException(VisionErrorCode.CameraGrabFailed, $"GigE Vision 相机 {Id} 已释放");
                    Mat? mat = ToMat(frame);
                    try
                    {
                        var image = VisionImageCv.Adopt(mat);
                        mat = null;
                        return new CameraFrame(image, DateTime.UtcNow, acquireMs,
                            grabWatch.Elapsed.TotalMilliseconds - acquireMs);
                    }
                    finally
                    {
                        mat?.Dispose();
                    }
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (TimeoutException ex)
            {
                throw new VisionException(VisionErrorCode.CameraGrabFailed,
                    $"GigE Vision 相机 {Id} 采集超时（{_grabTimeoutMs}ms）: {ex.Message}");
            }
            catch (VisionException vex) when (attempt == 0)
            {
                lock (_grabLock)
                    _connected = false;
                if (_log is { } log)
                    GigEVisionCameraLog.GrabFailedRetry(log, Id, vex.Message);
            }
            catch (Exception ex) when (attempt == 0)
            {
                lock (_grabLock)
                    _connected = false;
                if (_log is { } log)
                    GigEVisionCameraLog.GrabExceptionRetry(log, ex, Id);
            }
            catch (VisionException vex) when (attempt == 1)
            {
                throw new VisionException(VisionErrorCode.CameraGrabFailed,
                    $"GigE Vision 相机 {Id} 重连后采集仍失败: {vex.Message}");
            }
            catch (Exception ex) when (attempt == 1)
            {
                throw new VisionException(VisionErrorCode.CameraGrabFailed,
                    $"GigE Vision 相机 {Id} 重连后采集异常: {ex.Message}", ex);
            }
        }

        throw new VisionException(VisionErrorCode.CameraGrabFailed,
            $"GigE Vision 相机 {Id} 采集失败且自动重连未恢复");
    }

    /// <summary>网口广播发现（含不同网段的 APIPA 相机）。</summary>
    public static IReadOnlyList<GigECameraInfo> DiscoverCameras() => GigECameraDiscovery.DiscoverCameras();

    /// <summary>枚举网口可见的 GigE Vision 相机。失败返回空列表，不抛异常。</summary>
    public static IReadOnlyList<string> EnumerateDevices() => GigECameraDiscovery.EnumerateDevices();

    public bool TrySetExposureTimeUs(double value)
    {
        lock (_grabLock)
            return EnsureConnected() && TrySetExposureCore(value);
    }

    public bool TrySetGain(double value)
    {
        lock (_grabLock)
            return EnsureConnected() && TrySetGainCore(value);
    }

    public double? GetExposureTimeUs()
    {
        lock (_grabLock)
            return EnsureConnected()
                ? TryGetFloat("ExposureTime") ?? TryGetFloat("ExposureTimeAbs") ?? TryGetInteger("ExposureTime")
                : null;
    }

    public double? GetGain()
    {
        lock (_grabLock)
            return EnsureConnected()
                ? TryGetFloat("Gain") ?? TryGetFloat("GainAbs") ?? TryGetInteger("GainRaw")
                : null;
    }

    public (double Min, double Max)? GetExposureRange()
    {
        lock (_grabLock)
            return EnsureConnected()
                ? GetFloatRange("ExposureTime") ?? GetFloatRange("ExposureTimeAbs") ?? GetIntegerRange("ExposureTime")
                : null;
    }

    public (double Min, double Max)? GetGainRange()
    {
        lock (_grabLock)
            return EnsureConnected()
                ? GetFloatRange("Gain") ?? GetFloatRange("GainAbs") ?? GetIntegerRange("GainRaw")
                : null;
    }

    /// <summary>读/写光度参数前按需连接（注册阶段不连相机，与 Grab 一致）。</summary>
    private bool EnsureConnected()
    {
        if (_disposed)
            return false;
        if (_connected && _session is not null && _stream is not null)
            return true;
        return ConnectCore();
    }

    private bool ConnectCore()
    {
        try
        {
            DisconnectCore();

            GigECameraInfo info;
            if (GigECameraDiscovery.TryParseIpv4(_deviceId, out var ip) && GigEForceIp.IsOnLocalFixedSubnet(ip))
            {
                info = new GigECameraInfo { IpAddress = ip };
            }
            else
            {
                var cameras = GigECameraDiscovery.DiscoverCameras();
                if (cameras.Count == 0)
                    throw new InvalidOperationException("网口上未发现 GigE Vision 相机（检查网线、IP 网段、网卡与 UDP 3956 防火墙）");

                info = GigECameraDiscovery.SelectDevice(cameras, _deviceId)
                    ?? throw new InvalidOperationException(
                        CameraDeviceSelection.UnresolvedMessage(
                            Id, _deviceId, cameras.Count, string.Join("; ", cameras.Select(GigECameraDiscovery.FormatDevice))));
                info = GigEForceIp.EnsureReachable(info, _log);
            }

            var factory = new GigECameraSessionFactory();
            _session = RunSync(ct => factory.ConnectAsync(info, cancellationToken: ct), ConnectTimeout);

            _serialNumber = info.SerialNumber;
            _friendlyName = string.Join(" ", new[] { info.ManufacturerName, info.ModelName }
                .Where(s => !string.IsNullOrWhiteSpace(s)));
            if (_friendlyName.Length == 0)
                _friendlyName = info.IpAddress.ToString();

            // 与 Basler 对齐：关硬触发（相机 UserSet 若存了硬触发档，自由跑收流会一直等触发，
            // Grab 表现为超时）。须在 StartAcquisition 前设置。
            TrySetEnumeration("TriggerSelector", "FrameStart");
            TrySetEnumeration("TriggerMode", "Off");
            TryEnable2x2FullFrameDownsample();

            _stream = new GvspStreamSession();
            _stream.FrameReceived += OnFrameReceived;
            var localPort = _stream.Start(0);
            RunSync(ct => _session.StartAcquisitionAsync(localPort, ct), ConnectTimeout);

            // 库在 StartAcquisition 时可能打开 ExposureAuto/GainAuto；软件取图改为手动。
            TrySetEnumeration("ExposureAuto", "Off");
            if (!TrySetEnumeration("GainSelector", "AnalogAll"))
                TrySetEnumeration("GainSelector", "All");
            TrySetEnumeration("GainAuto", "Off");
            if (_exposureTimeUs is > 0)
                TrySetExposureCore(_exposureTimeUs.Value);
            if (_gain is >= 0)
                TrySetGainCore(_gain.Value);

            _connected = true;
            if (_log is { } log)
                GigEVisionCameraLog.Connected(log, Id, _serialNumber, info.IpAddress.ToString(), _friendlyName);
            return true;
        }
        catch (Exception ex)
        {
            _connected = false;
            if (_log is { } log)
                GigEVisionCameraLog.ConnectFailed(log, ex, Id);
            DisconnectCore();
            return false;
        }
    }

    private GvspFrame WaitForFrame(CancellationToken ct)
    {
        var tcs = new TaskCompletionSource<GvspFrame>(TaskCreationOptions.RunContinuationsAsynchronously);
        Interlocked.Exchange(ref _pendingFrame, tcs);

        using var timeout = new CancellationTokenSource(_grabTimeoutMs);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, timeout.Token);
        using var reg = linked.Token.Register(() => tcs.TrySetCanceled(linked.Token));

        try
        {
            return tcs.Task.GetAwaiter().GetResult();
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            throw new TimeoutException($"等待 GVSP 帧超时（{_grabTimeoutMs}ms）");
        }
        finally
        {
            Interlocked.CompareExchange(ref _pendingFrame, null, tcs);
        }
    }

    private void OnFrameReceived(object? sender, GvspFrame frame)
    {
        if (frame.SizeX == 0 || frame.SizeY == 0 || frame.Data.Length == 0)
            return;

        var pending = Interlocked.Exchange(ref _pendingFrame, null);
        if (pending is null)
            return;

        // 深拷贝像素后再交付：库在收下一帧时可能复用/回写同一 Data 缓冲，
        // 若直接交付原帧，消费侧 ToMat 拷贝期间会被流线程并发改写（花屏/错图）
        var copy = new byte[frame.Data.Length];
        Buffer.BlockCopy(frame.Data, 0, copy, 0, copy.Length);
        pending.TrySetResult(new GvspFrame
        {
            FrameId = frame.FrameId,
            PayloadType = frame.PayloadType,
            PixelFormat = frame.PixelFormat,
            SizeX = frame.SizeX,
            SizeY = frame.SizeY,
            OffsetX = frame.OffsetX,
            OffsetY = frame.OffsetY,
            PaddingX = frame.PaddingX,
            PaddingY = frame.PaddingY,
            Timestamp = frame.Timestamp,
            Data = copy,
        });
    }

    private Mat ToMat(GvspFrame frame)
    {
        if (TryBayerToBgr(frame, out var bayer))
            return bayer;

        if (!_display.TryConvert(frame, out var display) || display.Data.Length == 0)
            throw new VisionException(VisionErrorCode.CameraGrabFailed,
                $"GigE Vision 相机 {Id} 像素格式无法转换: 0x{frame.PixelFormat:X8}");

        return display.DisplayFormat switch
        {
            DisplayPixelFormat.Bgr24 => CopyToMat(display.Height, display.Width, 3, display.Stride, display.Data),
            DisplayPixelFormat.Bgr32 => BgraToBgr(display.Height, display.Width, display.Stride, display.Data),
            _ => GrayToBgr(display.Height, display.Width, display.Stride, display.Data),
        };
    }

    private static bool TryBayerToBgr(GvspFrame frame, out Mat bgr)
    {
        bgr = null!;
        var code = frame.PixelFormat switch
        {
            0x01080008 => ColorConversionCodes.BayerGR2BGR,
            0x01080009 => ColorConversionCodes.BayerRG2BGR,
            0x0108000A => ColorConversionCodes.BayerGB2BGR,
            0x0108000B => ColorConversionCodes.BayerBG2BGR,
            _ => (ColorConversionCodes)(-1),
        };
        if ((int)code < 0)
            return false;

        var width = (int)frame.SizeX;
        var height = (int)frame.SizeY;
        if (frame.Data.Length < width * height)
            return false;

        var gray = new Mat(height, width, MatType.CV_8UC1);
        try
        {
            Marshal.Copy(frame.Data, 0, gray.Data, width * height);
            bgr = new Mat();
            Cv2.CvtColor(gray, bgr, code);
            return true;
        }
        catch
        {
            bgr?.Dispose();
            bgr = null!;
            return false;
        }
        finally
        {
            gray.Dispose();
        }
    }

    private static Mat CopyToMat(int height, int width, int channels, int stride, byte[] data)
    {
        var type = channels switch
        {
            4 => MatType.CV_8UC4,
            3 => MatType.CV_8UC3,
            _ => MatType.CV_8UC1,
        };
        var mat = new Mat(height, width, type);
        try
        {
            var dstStride = (int)mat.Step();
            if (stride == dstStride && data.Length >= height * stride)
            {
                Marshal.Copy(data, 0, mat.Data, height * stride);
                return mat;
            }

            unsafe
            {
                var dst = (byte*)mat.Data;
                for (var r = 0; r < height; r++)
                {
                    var count = Math.Min(width * channels, stride);
                    Marshal.Copy(data, r * stride, (nint)(dst + r * dstStride), count);
                }
            }
            return mat;
        }
        catch
        {
            mat.Dispose();
            throw;
        }
    }

    private static Mat BgraToBgr(int height, int width, int stride, byte[] data)
    {
        using var bgra = CopyToMat(height, width, 4, stride, data);
        var bgr = new Mat();
        Cv2.CvtColor(bgra, bgr, ColorConversionCodes.BGRA2BGR);
        return bgr;
    }

    private static Mat GrayToBgr(int height, int width, int stride, byte[] data)
    {
        using var gray = CopyToMat(height, width, 1, stride, data);
        var bgr = new Mat();
        Cv2.CvtColor(gray, bgr, ColorConversionCodes.GRAY2BGR);
        return bgr;
    }

    private void TryEnable2x2FullFrameDownsample()
    {
        if (TryApply2x2FullFrameDownsample(out var width, out var height, out var mode))
        {
            _hardware2x2 = true;
            _outputWidth = width;
            _outputHeight = height;
            if (_log is { } log)
                GigEVisionCameraLog.Downsample2x2Applied(log, Id, width, height, mode);
            return;
        }

        if (_log is { } failedLog)
            GigEVisionCameraLog.Downsample2x2Failed(failedLog, Id);
    }

    private bool TryApply2x2FullFrameDownsample(out int width, out int height, out string mode)
    {
        width = 0;
        height = 0;
        mode = "";
        TrySetInteger("OffsetX", 0);
        TrySetInteger("OffsetY", 0);
        if (TrySetPairToTwo("BinningHorizontal", "BinningVertical"))
            mode = "binning";
        else if (TrySetPairToTwo("DecimationHorizontal", "DecimationVertical"))
            mode = "decimation";
        else
            return false;

        TrySetIntegerToMaximum("Width");
        TrySetIntegerToMaximum("Height");
        width = (int)(TryGetInteger("Width") ?? 0);
        height = (int)(TryGetInteger("Height") ?? 0);
        return width > 0 && height > 0;
    }

    private bool TrySetPairToTwo(string horizontal, string vertical)
    {
        if (TrySetInteger(horizontal, 2) && TrySetInteger(vertical, 2)
            && IntegerAtLeast(horizontal, 2) && IntegerAtLeast(vertical, 2))
            return true;
        TrySetInteger(horizontal, 1);
        TrySetInteger(vertical, 1);
        return false;
    }

    private bool IntegerAtLeast(string name, long min) =>
        TryGetInteger(name) is { } value && value >= min;

    private bool TrySetIntegerToMaximum(string name)
    {
        try
        {
            if (_session?.NodeMap.GetNode(name) is not IInteger node)
                return false;
            if (!IsWritable(node.AccessMode))
                return true;
            var max = node.Max;
            if (node.Increment > 1)
                max = node.Min + (max - node.Min) / node.Increment * node.Increment;
            node.Value = max;
            return true;
        }
        catch (Exception ex)
        {
            if (_log is { } log)
                GigEVisionCameraLog.WriteFailed(log, ex, Id, name);
            return false;
        }
    }

    private bool TrySetExposureCore(double valueUs) =>
        TrySetFloat("ExposureTime", valueUs)
        || TrySetFloat("ExposureTimeAbs", valueUs)
        || TrySetInteger("ExposureTime", (long)Math.Round(valueUs));

    private bool TrySetGainCore(double value)
    {
        if (!TrySetEnumeration("GainSelector", "AnalogAll"))
            TrySetEnumeration("GainSelector", "All");
        TrySetEnumeration("GainAuto", "Off");
        return TrySetFloat("Gain", value)
            || TrySetFloat("GainAbs", value)
            || TrySetInteger("GainRaw", (long)Math.Round(value));
    }

    private bool TrySetFloat(string name, double value)
    {
        try
        {
            if (_session?.NodeMap.GetNode(name) is not IFloat node || !IsWritable(node.AccessMode))
                return false;
            if (value < node.Min || value > node.Max)
            {
                if (_log is { } log)
                    GigEVisionCameraLog.ParameterOutOfRange(log, Id, name, (long)value, (long)node.Min, (long)node.Max);
                return false;
            }
            node.Value = value;
            return true;
        }
        catch (Exception ex)
        {
            if (_log is { } log)
                GigEVisionCameraLog.WriteFailed(log, ex, Id, name);
            return false;
        }
    }

    private bool TrySetInteger(string name, long value)
    {
        try
        {
            if (_session?.NodeMap.GetNode(name) is not IInteger node || !IsWritable(node.AccessMode))
                return false;
            var clamped = Math.Min(Math.Max(value, node.Min), node.Max);
            if (node.Increment > 1)
                clamped = node.Min + (clamped - node.Min) / node.Increment * node.Increment;
            node.Value = clamped;
            return true;
        }
        catch (Exception ex)
        {
            if (_log is { } log)
                GigEVisionCameraLog.WriteFailed(log, ex, Id, name);
            return false;
        }
    }

    private bool TrySetEnumeration(string name, string symbolic)
    {
        try
        {
            if (_session?.NodeMap.GetNode(name) is not IEnumeration node || !IsWritable(node.AccessMode))
                return false;
            if (node.GetEntryByName(symbolic) is null
                && !node.Entries.Any(e => string.Equals(e.Symbolic, symbolic, StringComparison.OrdinalIgnoreCase)))
                return false;
            node.Value = symbolic;
            return true;
        }
        catch (Exception ex)
        {
            if (_log is { } log)
                GigEVisionCameraLog.EnumSkipped(log, ex, Id, name, symbolic);
            return false;
        }
    }

    private double? TryGetFloat(string name)
    {
        try
        {
            return _session?.NodeMap.GetNode(name) is IFloat node && IsReadable(node.AccessMode)
                ? node.Value
                : null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private double? TryGetInteger(string name)
    {
        try
        {
            return _session?.NodeMap.GetNode(name) is IInteger node && IsReadable(node.AccessMode)
                ? node.Value
                : null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private (double Min, double Max)? GetFloatRange(string name)
    {
        try
        {
            if (_session?.NodeMap.GetNode(name) is not IFloat node || !IsReadable(node.AccessMode))
                return null;
            return node.Max > node.Min ? (node.Min, node.Max) : null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private (double Min, double Max)? GetIntegerRange(string name)
    {
        try
        {
            if (_session?.NodeMap.GetNode(name) is not IInteger node || !IsReadable(node.AccessMode))
                return null;
            return node.Max > node.Min ? (node.Min, node.Max) : null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static bool IsWritable(AccessMode mode) => mode is AccessMode.RW or AccessMode.WO;

    private static bool IsReadable(AccessMode mode) => mode is AccessMode.RO or AccessMode.RW;

    private void DisconnectCore()
    {
        Interlocked.Exchange(ref _pendingFrame, null)?.TrySetCanceled();
        _connected = false;
        _hardware2x2 = false;
        _outputWidth = 0;
        _outputHeight = 0;

        // 断连阶段任何一步失败都不得阻断后续清理，但每条失败都要留痕，
        // 否则现场"相机断连后资源没释放干净"这类问题无从追查。
        if (_session is not null)
        {
            try { RunSync(ct => _session.StopAcquisitionAsync(ct), TimeSpan.FromSeconds(3)); }
            catch (Exception ex)
            {
                if (_log is { } log)
                    GigEVisionCameraLog.DisconnectStopAcquisitionFailed(log, ex, Id);
            }
        }

        if (_stream is not null)
        {
            _stream.FrameReceived -= OnFrameReceived;
            try
            {
                _stream.Stop();
                _stream.Dispose();
            }
            catch (Exception ex)
            {
                if (_log is { } log)
                    GigEVisionCameraLog.DisconnectStreamCleanupFailed(log, ex, Id);
            }

            _stream = null;
        }

        try { _session?.Dispose(); }
        catch (Exception ex)
        {
            if (_log is { } log)
                GigEVisionCameraLog.DisconnectSessionDisposeFailed(log, ex, Id);
        }

        _session = null;
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        lock (_grabLock)
            DisconnectCore();
    }

    private static T RunSync<T>(Func<CancellationToken, Task<T>> work, TimeSpan timeout)
    {
        using var cts = new CancellationTokenSource(timeout);
        return work(cts.Token).ConfigureAwait(false).GetAwaiter().GetResult();
    }

    private static void RunSync(Func<CancellationToken, Task> work, TimeSpan timeout)
    {
        using var cts = new CancellationTokenSource(timeout);
        work(cts.Token).ConfigureAwait(false).GetAwaiter().GetResult();
    }
}
