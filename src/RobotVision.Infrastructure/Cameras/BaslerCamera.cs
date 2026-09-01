using System.Diagnostics.CodeAnalysis;
using Basler.Pylon;
using Microsoft.Extensions.Logging;
using OpenCvSharp;
using RobotVision.Core;
using RobotVision.Core.Abstractions;
using RobotVision.Core.Models;
using RobotVision.Infrastructure;
using System.Diagnostics;
using System.Net;
using GenICam.Net.GigEVision.Gvcp;
using ICamera = RobotVision.Core.Abstractions.ICamera;

namespace RobotVision.Infrastructure.Cameras;

/// <summary>
/// Basler 工业相机（pylon .NET API）。
/// 运行前提：目标机安装 pylon Camera Software Suite（版本与 Basler.Pylon.NET.x64 匹配）。
///
/// 设计要点：
/// - 懒连接：构造只校验 pylon 运行库（不枚举/打开设备）。<c>new Camera()</c> 在无设备时会抛异常，
///   因此推迟到首次 <see cref="ConnectCore"/>——启动时相机未上电/网络未就绪不阻断注册，
///   首次取图自动连接；
/// - 单帧采集用 <c>GrabOne</c>（内部 Start(1)+RetrieveResult+Stop）。pylon 约定
///   GrabOne 前置条件是「采集已停止」，因此连接阶段不得调用 <c>StreamGrabber.Start()</c>，
///   否则取图必失败，重连也会再次 Start 而无法自愈；
/// - 自动重连：单帧采集失败（连接中断类）后同请求内重连一次
///   （Close→Open→重发参数→再 GrabOne），仍失败才返回 1003；
/// - 软件取图：连接时把 TriggerMode 置 Off、关闭 ExposureAuto/GainAuto，避免相机 UserSet
///   残留硬触发导致 GrabOne 一直等到超时；
/// - 曝光/增益兼容 SFNC 1.x（ExposureTimeAbs / GainAbs / GainRaw）与 2.x+（ExposureTime / Gain）；
///   ace GigE 的 Gain 节点常不可写，须走 GainAbs（dB）或 GainRaw；写前切 GainSelector 并关 GainAuto；
/// - 调光能力经 <see cref="IExposureControl"/> 暴露。
/// 所有帧统一转为 BGR8 三通道 Mat，与 FileCamera（ImreadModes.Color）行为一致。
/// </summary>
public sealed class BaslerCamera : ICamera, IExposureControl
{
    private const int GigEFallbackWidth = 1280;
    private const int GigEFallbackHeight = 960;
    private const long GigEDefaultPacketSize = 1500;
    private const int GigEUnderrunErrorCode = -520093676;

    private Camera? _camera;

    // CA2213 误报：_converter 在 Dispose() 的 finally 块中被无条件释放，
    // 但分析器无法识别 try/finally 路径，仍判定"从未释放"。此处代码已正确，故抑制。
    [SuppressMessage("Usage", "CA2213:可释放字段应被释放",
        Justification = "Dispose() 的 finally 块中已无条件调用 _converter.Dispose()，分析器无法识别该路径。")]
    private readonly PixelDataConverter _converter;
    private readonly string _deviceId;
    private readonly int _grabTimeoutMs;
    private readonly double? _exposureTimeUs;
    private readonly double? _gain;
    private readonly ILogger? _log;
    private readonly object _grabLock = new();
    private volatile bool _disposed;
    private volatile bool _connected;
    /// <summary>最近一次连接/采集失败原因（写入最终 1003 异常，便于现场排查）。</summary>
    private string _lastFailureReason = "";
    /// <summary>本实例已因 GigE underrun 降为 1280×960（会话内保持，重启程序可再试全幅）。</summary>
    private bool _reducedResolution;
    private string _serialNumber = "";
    private string _friendlyName = "";

    static BaslerCamera()
    {
        // 从 VS 启动时未必带上 pylon Viewer 快捷方式里的 PATH/GENTL；
        // 原生库必须在首次 CameraFinder 调用前能被找到。
        try { EnsurePylonNativePath(); }
        catch { /* 无 pylon 的机器保持空枚举，不阻断进程启动 */ }
    }

    public string Id { get; }

    public CameraKind Kind => CameraKind.Real;

    /// <summary>相机序列号；未连接（未完成首次 Open）时为空串。</summary>
    public string SerialNumber => _serialNumber;

    /// <summary>相机友好名；未连接时为空串。</summary>
    public string FriendlyName => _friendlyName;

    public BaslerCamera(string id, string? deviceId = null, double? exposureTimeUs = null,
        double? gain = null, int grabTimeoutMs = 60_000, ILogger? log = null)
    {
        Id = id;
        _deviceId = deviceId ?? "";
        _exposureTimeUs = exposureTimeUs;
        _gain = gain;
        _grabTimeoutMs = grabTimeoutMs;
        _log = log;
        try
        {
            // 转换器会加载 pylon 原生库；不在构造期 Enumerate（注册多台相机时重复扫描阻塞 UI）。
            _converter = new PixelDataConverter { OutputPixelFormat = PixelType.BGR8packed };
        }
        catch (Exception ex)
        {
            throw new VisionException(VisionErrorCode.CameraInitFailed,
                $"Basler 相机 {id} 初始化失败（检查 pylon 运行库与相机连接）: {ex.Message}");
        }
    }

    /// <summary>
    /// 尝试连接一次（打开设备 + 参数下发，不启动连续采集）。成功返回 true；失败记日志并返回 false
    /// （相机保持注册状态，首次取图时再次自动连接）。用于启动时的连接诊断。
    /// </summary>
    public bool TryConnectOnce()
    {
        lock (_grabLock)
        {
            if (_disposed)
                return false;
            return ConnectCore();
        }
    }

    public CameraFrame Grab(CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ct.ThrowIfCancellationRequested();

        lock (_grabLock)
        {
            // 锁内复查：与 Dispose 的竞态窗口内，已释放按取图失败（1003）而非内部错误（1099）呈现
            if (_disposed)
                throw new VisionException(VisionErrorCode.CameraGrabFailed, $"Basler 相机 {Id} 已释放");

            for (var attempt = 0; attempt < 2; attempt++)
            {
                var needConnect = _camera is null || !_connected || attempt > 0;
                if (needConnect && !ConnectCore())
                {
                    if (attempt == 0)
                        continue;
                    break;
                }

                try
                {
                    return TryGrabWithUnderrunFallback();
                }
                catch (TimeoutException ex)
                {
                    _lastFailureReason = ex.Message;
                    // 相机在线但未出图（硬触发未到、曝光过长等）：重连无益，直接失败
                    throw new VisionException(VisionErrorCode.CameraGrabFailed,
                        $"Basler 相机 {Id} 采集超时（{_grabTimeoutMs}ms）: {ex.Message}");
                }
                catch (VisionException vex) when (attempt == 0)
                {
                    _lastFailureReason = vex.Message;
                    ReleaseDevice();
                    if (_log is { } log) BaslerCameraLog.GrabFailedRetry(log, Id, vex.Message);
                }
                catch (Exception ex) when (attempt == 0)
                {
                    _lastFailureReason = ex.Message;
                    ReleaseDevice();
                    if (_log is { } log) BaslerCameraLog.GrabExceptionRetry(log, ex, Id);
                }
                catch (VisionException vex) when (attempt == 1)
                {
                    throw new VisionException(VisionErrorCode.CameraGrabFailed,
                        $"Basler 相机 {Id} 重连后采集仍失败: {vex.Message}");
                }
                catch (Exception ex) when (attempt == 1)
                {
                    throw new VisionException(VisionErrorCode.CameraGrabFailed,
                        $"Basler 相机 {Id} 重连后采集异常: {ex.Message}", ex);
                }
            }

            var detail = string.IsNullOrWhiteSpace(_lastFailureReason)
                ? ""
                : $": {_lastFailureReason}";
            throw new VisionException(VisionErrorCode.CameraGrabFailed,
                $"Basler 相机 {Id} 采集失败且自动重连未恢复{detail}");
        }
    }

    /// <summary>创建设备对象（若需要）、打开、下发软件取图与光度参数。成功返回 true；失败记日志并置未连接。</summary>
    private bool ConnectCore()
    {
        try
        {
            _camera ??= CreateDevice();
            SafeStopGrabbing();
            if (_camera.IsOpen)
                _camera.Close();
            _camera.Open();

            var info = _camera.CameraInfo;
            _serialNumber = info is null ? _deviceId : info[CameraInfoKey.SerialNumber]!;
            _friendlyName = info is null ? "" : info[CameraInfoKey.FriendlyName]!;

            ApplySoftwareGrabDefaults();
            ApplyGigEStreamDefaults();
            if (_reducedResolution)
                ApplyReducedResolution();

            // 参数下发失败不阻断连接，但记日志（现场排查"图像太暗/太亮"的线索）
            if (_exposureTimeUs is > 0)
                TrySetExposureCore(_exposureTimeUs.Value);
            if (_gain is >= 0)
                TrySetGainCore(_gain.Value);

            _connected = true;
            _lastFailureReason = "";
            if (_log is { } log) BaslerCameraLog.Connected(log, Id, _serialNumber, _friendlyName);
            return true;
        }
        catch (Exception ex)
        {
            _lastFailureReason = ex.Message;
            ReleaseDevice();
            if (_log is { } log) BaslerCameraLog.ConnectFailed(log, ex, Id);
            return false;
        }
    }

    private Camera CreateDevice()
    {
        var forcedIp = TryForceGigEIpIntoNicSubnet();

        var devices = TryEnumeratePylon();
        if (devices.Count == 0)
        {
            // FORCEIP 后 pylon 发现有时慢一拍；空列表时再等一次，避免误报 No matching camera found
            Thread.Sleep(1000);
            devices = TryEnumeratePylon();
        }
        var specified = !string.IsNullOrWhiteSpace(_deviceId);
        var match = FindPylonDevice(devices, _deviceId)
                    ?? (!specified ? null : FindPylonDevice(devices, forcedIp ?? ""));
        if (match is not null)
            return new Camera(match);

        foreach (var key in new[] { _deviceId, specified ? forcedIp : null })
        {
            if (string.IsNullOrWhiteSpace(key))
                continue;
            try
            {
                return new Camera(key.Trim());
            }
            catch (Exception ex)
            {
                if (_log is { } log) BaslerCameraLog.OpenByKeyFailed(log, ex, Id, key);
            }
        }

        if (specified && !string.IsNullOrWhiteSpace(forcedIp))
        {
            try
            {
                return new Camera(new Dictionary<string, string>
                {
                    [CameraInfoKey.DeviceIpAddress] = forcedIp,
                }, CameraSelectionStrategy.Unambiguous);
            }
            catch (Exception ex)
            {
                if (_log is { } log) BaslerCameraLog.OpenByTempIpFailed(log, ex, Id, forcedIp);
            }
        }

        if (!specified && devices.Count == 1)
        {
            if (_log is { } log) BaslerCameraLog.SingleDeviceBind(log, Id, devices[0][CameraInfoKey.SerialNumber]!);
            return new Camera(devices[0]);
        }

        var available = string.Join("; ", devices.Select(FormatPylonDevice));
        if (devices.Count == 0)
        {
            throw new InvalidOperationException(
                "未发现可打开的 Basler 相机。请完全退出 pylon Viewer，确认网卡与相机 IP 同网段（169.254 与 192.168 不能直接互通），并检查防火墙。");
        }

        throw new InvalidOperationException(
            CameraDeviceSelection.UnresolvedMessage(Id, _deviceId, devices.Count, available));
    }

    private static string FormatPylonDevice(ICameraInfo device)
    {
        try
        {
            var sn = device[CameraInfoKey.SerialNumber] ?? "";
            var ip = device[CameraInfoKey.DeviceIpAddress] ?? "";
            return string.IsNullOrEmpty(ip) ? sn : $"{sn}/{ip}";
        }
        catch
        {
            return "?";
        }
    }

    private string? TryForceGigEIpIntoNicSubnet()
    {
        try
        {
            var cameras = GigEVisionCamera.DiscoverCameras();
            if (cameras.Count == 0)
                return null;
            // 两台都在 APIPA 时只对齐目标相机，pylon 仍可能扫不到；全部拉进网卡网段
            var aligned = GigEForceIp.EnsureAllReachable(cameras, _log);
            var target = SelectGigE(aligned, _deviceId);
            return target?.IpAddress.ToString();
        }
        catch (Exception ex)
        {
            if (_log is { } log) BaslerCameraLog.ForceIpAlignFailed(log, ex, Id);
            return null;
        }
    }

    private static GigECameraInfo? SelectGigE(IReadOnlyList<GigECameraInfo> cameras, string deviceId) =>
        CameraDeviceSelection.Resolve(cameras, deviceId, static (camera, needle) =>
            string.Equals(camera.SerialNumber, needle, StringComparison.OrdinalIgnoreCase)
            || string.Equals(camera.IpAddress.ToString(), needle, StringComparison.OrdinalIgnoreCase));

    private static ICameraInfo? FindPylonDevice(List<ICameraInfo> devices, string deviceId)
    {
        if (devices.Count == 0 || string.IsNullOrWhiteSpace(deviceId))
            return null;

        var needle = deviceId.Trim();
        foreach (var device in devices)
        {
            if (InfoEquals(device, CameraInfoKey.SerialNumber, needle)
                || InfoEquals(device, CameraInfoKey.DeviceIpAddress, needle)
                || InfoEquals(device, CameraInfoKey.UserDefinedName, needle)
                || InfoEquals(device, CameraInfoKey.DeviceMacAddress, needle))
                return device;
        }

        return null;
    }

    private static bool InfoEquals(ICameraInfo info, string key, string expected)
    {
        try
        {
            var value = info[key];
            return !string.IsNullOrEmpty(value)
                   && string.Equals(value, expected, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>释放 pylon 设备句柄。采集/连接失败后必须调用，否则 Close+Open 无法自愈。</summary>
    private void ReleaseDevice()
    {
        SafeStopGrabbing();
        try
        {
            if (_camera is { IsOpen: true })
                _camera.Close();
        }
        catch (Exception ex)
        {
            if (_log is { } log) BaslerCameraLog.CloseSkipped(log, ex, Id);
        }

        try
        {
            _camera?.Dispose();
        }
        catch (Exception ex)
        {
            if (_log is { } log) BaslerCameraLog.DisposeSkipped(log, ex, Id);
        }

        _camera = null;
        _connected = false;
    }

    /// <summary>
    /// 软件取图默认：关硬触发、关自动曝光/增益。失败忽略（机型无此参数时 TrySetValue 返回 false）。
    /// </summary>
    private void ApplySoftwareGrabDefaults()
    {
        var camera = _camera!;
        try { camera.Parameters[PLCamera.TriggerSelector].TrySetValue(PLCamera.TriggerSelector.FrameStart); }
        catch (Exception ex) { if (_log is { } log) BaslerCameraLog.TriggerSelectorSkipped(log, ex, Id); }
        try { camera.Parameters[PLCamera.TriggerMode].TrySetValue(PLCamera.TriggerMode.Off); }
        catch (Exception ex) { if (_log is { } log) BaslerCameraLog.TriggerModeSkipped(log, ex, Id); }
        try { camera.Parameters[PLCamera.ExposureMode].TrySetValue(PLCamera.ExposureMode.Timed); }
        catch (Exception ex) { if (_log is { } log) BaslerCameraLog.ExposureModeSkipped(log, ex, Id); }
        try { camera.Parameters[PLCamera.ExposureAuto].TrySetValue(PLCamera.ExposureAuto.Off); }
        catch (Exception ex) { if (_log is { } log) BaslerCameraLog.ExposureAutoSkipped(log, ex, Id); }
        TrySelectAnalogGain();
        try { camera.Parameters[PLCamera.GainAuto].TrySetValue(PLCamera.GainAuto.Off); }
        catch (Exception ex) { if (_log is { } log) BaslerCameraLog.GainAutoSkipped(log, ex, Id); }
    }

    /// <summary>
    /// GigE 收流默认：尽量用大包、适当包间延迟，减轻 5MP+ 全幅在普通网卡上的 buffer underrun。
    /// 仍失败时请运行 pylon GigE Configurator 优化网卡/防火墙。
    /// </summary>
    private void ApplyGigEStreamDefaults()
    {
        var camera = _camera!;
        try
        {
            // CameraInfo 可能为 null(pylon 可空标注),用 ?[ 安全访问,取不到按非 GEV 处理
            if (!string.Equals(camera.CameraInfo?[CameraInfoKey.TLType], "GEV", StringComparison.OrdinalIgnoreCase))
                return;
        }
        catch (Exception ex)
        {
            if (_log is { } log) BaslerCameraLog.TlTypeReadSkipped(log, ex, Id);
            return;
        }

        TrySetInteger(PLCamera.GevSCPSPacketSize, GetGigEPacketSize(camera));
        // 全幅 5472 宽在链路未优化时易 underrun；适度包间延迟换稳定性（机型上限通常 ≤1015）。
        TrySetInteger(PLCamera.GevSCPD, 1015);
    }

    private CameraFrame TryGrabWithUnderrunFallback()
    {
        try
        {
            return GrabOneFrame();
        }
        catch (VisionException vex) when (IsGrabUnderrun(vex) && TryApplyReducedResolutionFallback())
        {
            return GrabOneFrame();
        }
    }

    [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope",
        Justification = "VisionImage ownership transfers to CameraFrame.")]
    private CameraFrame GrabOneFrame()
    {
        var grabWatch = Stopwatch.StartNew();
        // StreamGrabber 在 pylon 中标注可空;相机已打开且开始采集时必非 null,这里用 ! 断言
        var result = _camera!.StreamGrabber!.GrabOne(_grabTimeoutMs, TimeoutHandling.ThrowException);
        var acquireMs = grabWatch.Elapsed.TotalMilliseconds;
        using (result)
        {
            if (result is null || !result.GrabSucceeded)
                throw new VisionException(VisionErrorCode.CameraGrabFailed,
                    $"Basler 相机 {Id} 采集失败: {result?.ErrorDescription ?? "无采集结果"} (code={result?.ErrorCode})");
            Mat? mat = ToMat(result);
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

    private bool TryApplyReducedResolutionFallback()
    {
        if (_reducedResolution || !IsGigECamera())
            return false;
        SafeStopGrabbing();
        ApplyReducedResolution();
        _reducedResolution = true;
        if (_log is { } log) BaslerCameraLog.GigEUnderrunReducedResolution(log, Id, GigEFallbackWidth, GigEFallbackHeight);
        return true;
    }

    private void ApplyReducedResolution()
    {
        TrySetInteger(PLCamera.OffsetX, 0);
        TrySetInteger(PLCamera.OffsetY, 0);
        TrySetInteger(PLCamera.Width, GigEFallbackWidth);
        TrySetInteger(PLCamera.Height, GigEFallbackHeight);
    }

    private bool IsGigECamera()
    {
        try
        {
            return string.Equals(_camera!.CameraInfo?[CameraInfoKey.TLType], "GEV",
                StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static bool IsGrabUnderrun(VisionException vex)
    {
        if (vex.Message.Contains("incompletely grabbed", StringComparison.OrdinalIgnoreCase)
            || vex.Message.Contains("Buffer underrun", StringComparison.OrdinalIgnoreCase))
            return true;
        return TryParseGrabErrorCode(vex.Message) == GigEUnderrunErrorCode;
    }

    private static int? TryParseGrabErrorCode(string message)
    {
        const string marker = "(code=";
        var start = message.LastIndexOf(marker, StringComparison.Ordinal);
        if (start < 0)
            return null;
        var end = message.IndexOf(')', start);
        if (end < 0)
            return null;
        return int.TryParse(message.AsSpan(start + marker.Length, end - start - marker.Length), out var code)
            ? code
            : null;
    }

    private static long GetGigEPacketSize(Camera camera)
    {
        try
        {
            var p = camera.Parameters[PLCamera.GevSCPSPacketSize];
            if (!p.IsEmpty && p.IsReadable)
            {
                // 未跑 GigE Configurator 时参数 Maximum 常为 8k+，超出普通网卡 MTU 易 underrun
                var max = p.GetMaximum();
                return Math.Min(max, GigEDefaultPacketSize);
            }
        }
        catch (Exception)
        {
            // 读失败时用常见安全值
        }

        return GigEDefaultPacketSize;
    }

    private void SafeStopGrabbing()
    {
        if (_camera is null)
            return;
        try
        {
           
            // StreamGrabber 可能为 null(pylon 可空标注);未就绪时视为未在采集
            if (_camera.IsOpen && _camera.StreamGrabber is { } grabber && grabber.IsGrabbing)
                grabber.Stop();
        }
        catch (Exception)
        {
            // 重连/关闭路径：Stop 失败继续 Close
        }
    }

    /// <summary>枚举本机可见的 Basler 相机（序列号 | 名称 | 型号）。未安装 pylon 或无相机时返回空列表。
    /// pylon 枚举为空时回退 GigE Vision 发现（Viewer 能看到、本进程却扫不到时的常见情况）。</summary>
    public static IReadOnlyList<string> EnumerateDevices()
    {
        var pylon = TryEnumeratePylon()
            .Select(i => $"{i[CameraInfoKey.SerialNumber]} | {i[CameraInfoKey.FriendlyName]} | {i[CameraInfoKey.ModelName]}")
            .ToList();
        if (pylon.Count > 0)
            return pylon;

        return GigEVisionCamera.EnumerateDevices();
    }

    private static List<ICameraInfo> TryEnumeratePylon()
    {
        try
        {
            return CameraFinder.Enumerate().ToList();
        }
        catch (Exception)
        {
            return [];
        }
    }

    private static string? FirstSerial(IReadOnlyList<string> lines)
    {
        foreach (var line in lines)
        {
            var sn = line.Split('|')[0].Trim();
            if (sn.Length > 0)
                return sn;
        }

        return null;
    }

    private static void EnsurePylonNativePath()
    {
        var runtime = FindPylonRuntimeDir();
        if (runtime is null)
            return;

        var path = Environment.GetEnvironmentVariable("PATH") ?? "";
        if (path.IndexOf(runtime, StringComparison.OrdinalIgnoreCase) < 0)
            Environment.SetEnvironmentVariable("PATH", runtime + Path.PathSeparator + path);

        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("GENICAM_GENTL64_PATH")))
            Environment.SetEnvironmentVariable("GENICAM_GENTL64_PATH", runtime);
    }

    private static string? FindPylonRuntimeDir()
    {
        foreach (var candidate in new[]
        {
            Environment.GetEnvironmentVariable("PYLON_ROOT"),
            @"D:\Program Files\Basler\pylon\Runtime\x64",
            @"C:\Program Files\Basler\pylon\Runtime\x64",
            @"D:\Program Files\Basler\pylon",
            @"C:\Program Files\Basler\pylon",
        })
        {
            var resolved = ResolveRuntimeDir(candidate);
            if (resolved is not null)
                return resolved;
        }

        // 注册表仅 Windows 可用:非 Windows 直接跳过(CA1416 平台守卫)
        if (!OperatingSystem.IsWindows())
            return null;
        try
        {
            using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Basler\pylon");
            return ResolveRuntimeDir(key?.GetValue("InstallationFolder") as string);
        }
        catch
        {
            return null;
        }
    }

    private static string? ResolveRuntimeDir(string? candidate)
    {
        if (string.IsNullOrWhiteSpace(candidate))
            return null;

        var dir = candidate.TrimEnd('\\', '/');
        if (IsPylonRuntimeDir(dir))
            return dir;

        var x64 = Path.Combine(dir, "Runtime", "x64");
        return IsPylonRuntimeDir(x64) ? x64 : null;
    }

    private static bool IsPylonRuntimeDir(string dir) =>
        Directory.Exists(dir) &&
        (File.Exists(Path.Combine(dir, "ProducerGEV.cti")) ||
         File.Exists(Path.Combine(dir, "PylonC_v10.dll")));

    // ---- IExposureControl（供 UI 调光；未连接时返回 null/false） ----

    /// <summary>读/写光度参数前按需 Open（注册阶段不连相机，与 Grab 一致）。</summary>
    private bool EnsureConnected()
    {
        if (_disposed)
            return false;
        if (_connected && _camera is not null)
            return true;
        return ConnectCore();
    }

    public bool TrySetExposureTimeUs(double value)
    {
        lock (_grabLock)
        {
            if (!EnsureConnected())
                return false;
            return TrySetExposureCore(value);
        }
    }

    public bool TrySetGain(double value)
    {
        lock (_grabLock)
        {
            if (!EnsureConnected())
                return false;
            return TrySetGainCore(value);
        }
    }

    public double? GetExposureTimeUs()
    {
        lock (_grabLock)
        {
            if (!EnsureConnected())
                return null;
            return TryGetFloat(PLCamera.ExposureTimeAbs) ?? TryGetFloat(PLCamera.ExposureTime);
        }
    }

    public double? GetGain()
    {
        lock (_grabLock)
        {
            if (!EnsureConnected())
                return null;
            return TryGetFloat(PLCamera.Gain)
                ?? TryGetFloat(PLCamera.GainAbs)
                ?? TryGetInteger(PLCamera.GainRaw);
        }
    }

    public (double Min, double Max)? GetExposureRange()
    {
        lock (_grabLock)
        {
            if (!EnsureConnected())
                return null;
            return GetFloatRange(PLCamera.ExposureTimeAbs) ?? GetFloatRange(PLCamera.ExposureTime);
        }
    }

    public (double Min, double Max)? GetGainRange()
    {
        lock (_grabLock)
        {
            if (!EnsureConnected())
                return null;
            return GetFloatRange(PLCamera.Gain)
                ?? GetFloatRange(PLCamera.GainAbs)
                ?? GetIntegerRange(PLCamera.GainRaw);
        }
    }

    private bool TrySetExposureCore(double valueUs)
    {
        if (TrySetFloat(PLCamera.ExposureTimeAbs, valueUs)
            || TrySetFloat(PLCamera.ExposureTime, valueUs))
            return true;
        if (_log is { } log) BaslerCameraLog.ExposureSetFailed(log, Id, valueUs);
        return false;
    }

    /// <summary>
    /// 增益按机型回退：SFNC 2 <c>Gain</c>（ace 2/USB/dart）→ SFNC 1 <c>GainAbs</c>（ace GigE，dB）
    /// → <c>GainRaw</c>（整数）。先切 AnalogAll/All 并关 GainAuto，否则 Gain 常为只读。
    /// </summary>
    private bool TrySetGainCore(double value)
    {
        TrySelectAnalogGain();
        try { _camera!.Parameters[PLCamera.GainAuto].TrySetValue(PLCamera.GainAuto.Off); }
        catch (Exception ex)
        {
            if (_log is { } log)
                BaslerCameraLog.GainAutoSkipped(log, ex, Id);
        }

        if (TrySetFloat(PLCamera.Gain, value)
            || TrySetFloat(PLCamera.GainAbs, value)
            || TrySetInteger(PLCamera.GainRaw, (long)Math.Round(value)))
            return true;
        {
            if (_log is { } log) BaslerCameraLog.GainSetFailed(log, Id, value);
        }
        return false;
    }

    /// <summary>
    /// ace GigE 用 AnalogAll，ace 2 / dart 用 All。选错通道时 Gain 节点存在但 IsWritable=false。
    /// </summary>
    private void TrySelectAnalogGain()
    {
        try
        {
            var sel = _camera!.Parameters[PLCamera.GainSelector];
            if (sel.IsEmpty || !sel.IsWritable)
                return;
            if (sel.TrySetValue(PLCamera.GainSelector.AnalogAll))
                return;
            sel.TrySetValue(PLCamera.GainSelector.All);
        }
        catch (Exception ex)
        {
            if (_log is { } log) BaslerCameraLog.GainSelectorSkipped(log, ex, Id);
        }
    }

    /// <summary>
    /// 转换到 Mat。pylon 转换器按紧凑行输出，而 OpenCV Mat 的 RowStep 按 4 字节对齐，
    /// 当 width*3 % 4 != 0 时直写 Mat.Data 会导致逐行错位（每行起始被上一行填充字节推偏），
    /// 因此仅在对齐一致时直写（省一次拷贝），否则先转换到紧凑缓冲再按行拷回。
    /// </summary>
    private unsafe Mat ToMat(IGrabResult result)
    {
        var mat = new Mat(result.Height, result.Width, MatType.CV_8UC3);
        try
        {
            var stride = result.Width * 3;
            if (stride % 4 == 0)
            {
                // 行字节数与 Mat.Step 一致（width*3 恰为 4 的倍数），可安全直写 Mat.Data
                _converter.Convert(mat.Data, mat.Total() * mat.ElemSize(), result);
                return mat;
            }

            // 紧凑行缓冲（无对齐填充），逐行拷回按 4 字节对齐的 Mat 行区
            var packed = new byte[result.Height * stride];
            fixed (byte* src = packed)
            {
                // pylon 无 byte[] 重载，经 fixed 固定后按 IntPtr 转换
                _converter.Convert((nint)src, packed.Length, result);
                var dst = (byte*)mat.Data;
                var step = (nint)mat.Step();
                for (var r = 0; r < result.Height; r++)
                    Buffer.MemoryCopy(src + r * stride, dst + r * step, stride, stride);
            }
            return mat;
        }
        catch
        {
            mat.Dispose();
            throw;
        }
    }

    private bool TrySetFloat(FloatName name, double value)
    {
        try
        {
            var p = _camera!.Parameters[name];
            // 空/只读是机型回退链上的正常探测（ace GigE 无 SFNC2 Gain），不要当失败报警
            if (p.IsEmpty || !p.IsWritable)
                return false;
            // 超界时拒绝下发并返回 false，让调用方知道值被改（静默 clamp 会让 UI 调光
            // 显示值与实际值不一致且无任何提示）
            var min = p.GetMinimum();
            var max = p.GetMaximum();
            if (value < min || value > max)
            {
                if (_log is { } log) BaslerCameraLog.ParameterOutOfRange(log, Id, name.Name, value, min, max);
                return false;
            }
            p.SetValue(value);
            return true;
        }
        catch (Exception ex)
        {
            if (_log is { } log) BaslerCameraLog.ParameterSetException(log, ex, Id, name.Name);
            return false;
        }
    }

    private bool TrySetInteger(IntegerName name, long value)
    {
        try
        {
            var p = _camera!.Parameters[name];
            if (p.IsEmpty || !p.IsWritable)
                return false;
            var min = p.GetMinimum();
            var max = p.GetMaximum();
            if (value < min || value > max)
            {
                if (_log is { } log) BaslerCameraLog.ParameterOutOfRange(log, Id, name.Name, value, min, max);
                return false;
            }
            p.SetValue(value);
            return true;
        }
        catch (Exception ex)
        {
            if (_log is { } log) BaslerCameraLog.ParameterSetException(log, ex, Id, name.Name);
            return false;
        }
    }

    private double? TryGetFloat(FloatName name)
    {
        try
        {
            var p = _camera!.Parameters[name];
            if (p.IsEmpty || !p.IsReadable)
                return null;
            return p.GetValue();
        }
        catch (Exception ex)
        {
            if (_log is { } log) BaslerCameraLog.ParameterReadException(log, ex, Id, name.Name);
            return null;
        }
    }

    private double? TryGetInteger(IntegerName name)
    {
        try
        {
            var p = _camera!.Parameters[name];
            if (p.IsEmpty || !p.IsReadable)
                return null;
            return p.GetValue();
        }
        catch (Exception ex)
        {
            if (_log is { } log) BaslerCameraLog.ParameterReadException(log, ex, Id, name.Name);
            return null;
        }
    }

    private (double Min, double Max)? GetFloatRange(FloatName name)
    {
        try
        {
            var p = _camera!.Parameters[name];
            if (p.IsEmpty || !p.IsReadable)
                return null;
            var min = p.GetMinimum();
            var max = p.GetMaximum();
            return max > min ? (min, max) : null;
        }
        catch (Exception ex)
        {
            if (_log is { } log) BaslerCameraLog.ParameterRangeReadException(log, ex, Id, name.Name);
            return null;
        }
    }

    private (double Min, double Max)? GetIntegerRange(IntegerName name)
    {
        try
        {
            var p = _camera!.Parameters[name];
            if (p.IsEmpty || !p.IsReadable)
                return null;
            var min = (double)p.GetMinimum();
            var max = (double)p.GetMaximum();
            return max > min ? (min, max) : null;
        }
        catch (Exception ex)
        {
            if (_log is { } log) BaslerCameraLog.ParameterRangeReadException(log, ex, Id, name.Name);
            return null;
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;

        try
        {
            lock (_grabLock)
            {
                ReleaseDevice();
            }
        }
        finally
        {
            // 必须无条件释放：ReleaseDevice 抛异常时若跳过这里，
            // PixelDataConverter 持有的非托管缓冲区会泄漏（CA2213）。
            _converter.Dispose();
        }
    }
}
