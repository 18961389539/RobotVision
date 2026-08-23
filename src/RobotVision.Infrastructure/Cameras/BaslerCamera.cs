using Basler.Pylon;
using Microsoft.Extensions.Logging;
using OpenCvSharp;
using RobotVision.Core;
using RobotVision.Core.Abstractions;
using RobotVision.Core.Models;
using ICamera = RobotVision.Core.Abstractions.ICamera;

namespace RobotVision.Infrastructure.Cameras;

/// <summary>
/// Basler 工业相机（pylon .NET API）。
/// 运行前提：目标机安装 pylon Camera Software Suite（版本与 Basler.Pylon.NET.x64 匹配）。
///
/// 设计要点：
/// - 懒连接：构造函数只校验 pylon 运行库可用，Open/Start 推迟到首次 Grab——
///   启动时相机未上电/网络未就绪不再阻断服务，首次取图自动连接；
/// - 自动重连：单帧采集失败（连接中断类）后同请求内重连一次
///   （Close→Open→重发参数→Start→再取一帧），仍失败才返回 1003；断线后可自愈，无需重启服务；
/// - 调光能力经 <see cref="IExposureControl"/> 暴露，UI 按接口查询，不依赖具体品牌；
/// - 曝光/增益仅在配置了数值时下发（兼容 SFNC 2.x 的 ExposureTimeAbs 与 3.x 的 ExposureTime）。
/// 所有帧统一转为 BGR8 三通道 Mat，与 FileCamera（ImreadModes.Color）行为一致。
/// </summary>
public sealed class BaslerCamera : ICamera, IExposureControl
{
    private readonly Camera _camera;
    private readonly PixelDataConverter _converter;
    private readonly string _deviceId;
    private readonly int _grabTimeoutMs;
    private readonly double? _exposureTimeUs;
    private readonly double? _gain;
    private readonly ILogger? _log;
    private readonly object _grabLock = new();
    private volatile bool _disposed;
    private volatile bool _connected;
    private string _serialNumber = "";
    private string _friendlyName = "";

    public string Id { get; }

    public CameraKind Kind => CameraKind.Real;

    /// <summary>相机序列号；未连接（未完成首次 Open）时为空串。</summary>
    public string SerialNumber => _serialNumber;

    /// <summary>相机友好名；未连接时为空串。</summary>
    public string FriendlyName => _friendlyName;

    public BaslerCamera(string id, string? deviceId = null, double? exposureTimeUs = null,
        double? gain = null, int grabTimeoutMs = 3000, ILogger? log = null)
    {
        Id = id;
        _deviceId = deviceId ?? "";
        _exposureTimeUs = exposureTimeUs;
        _gain = gain;
        _grabTimeoutMs = grabTimeoutMs;
        _log = log;
        try
        {
            // 转换器/相机对象在访问原生 pylon 时即会失败（未装 pylon），
            // 放在 try 内包装为带排查指引的 VisionException（1011 初始化失败）。
            // Open/Start 推迟到首次 Grab（懒连接），此处不接触设备。
            _converter = new PixelDataConverter { OutputPixelFormat = PixelType.BGR8packed };
            _camera = string.IsNullOrWhiteSpace(deviceId) ? new Camera() : new Camera(deviceId.Trim());
        }
        catch (Exception ex)
        {
            throw new VisionException(VisionErrorCode.CameraInitFailed,
                $"Basler 相机 {id} 初始化失败（检查 pylon 运行库与相机连接）: {ex.Message}");
        }
    }

    /// <summary>
    /// 尝试连接一次（Open + 参数下发 + 开始采集）。成功返回 true；失败记日志并返回 false
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
                if (attempt > 0 && !ConnectCore())
                    break; // 重连失败，直接报错

                try
                {
                    var result = _camera.StreamGrabber.GrabOne(_grabTimeoutMs, TimeoutHandling.ThrowException);
                    using (result)
                    {
                        if (result is null || !result.GrabSucceeded)
                            throw new VisionException(VisionErrorCode.CameraGrabFailed,
                                $"Basler 相机 {Id} 采集失败: {result?.ErrorDescription ?? "无采集结果"} (code={result?.ErrorCode})");
                        return new CameraFrame(ToMat(result), DateTime.UtcNow);
                    }
                }
                catch (TimeoutException ex)
                {
                    // 相机在线但未触发（未上曝光等）：重连无益，直接失败
                    throw new VisionException(VisionErrorCode.CameraGrabFailed,
                        $"Basler 相机 {Id} 采集超时（{_grabTimeoutMs}ms）: {ex.Message}");
                }
                catch (VisionException vex) when (attempt == 0)
                {
                    _log?.LogWarning("Basler 相机 {Id} 采集失败（{Message}），尝试自动重连", Id, vex.Message);
                    continue;
                }
                catch (Exception ex) when (attempt == 0)
                {
                    // pylon 运行时异常（连接中断类）
                    _log?.LogWarning(ex, "Basler 相机 {Id} 采集异常，尝试自动重连", Id);
                    continue;
                }
                catch (VisionException vex) when (attempt == 1)
                {
                    // 重连后仍失败：统一包装为 1003，避免抛出其他错误码被上层误判
                    throw new VisionException(VisionErrorCode.CameraGrabFailed,
                        $"Basler 相机 {Id} 重连后采集仍失败: {vex.Message}");
                }
                catch (Exception ex) when (attempt == 1)
                {
                    // 重连后第二次取图的 pylon 裸异常：同样包装为 1003，避免冒成 1099
                    throw new VisionException(VisionErrorCode.CameraGrabFailed,
                        $"Basler 相机 {Id} 重连后采集异常: {ex.Message}", ex);
                }
            }

            throw new VisionException(VisionErrorCode.CameraGrabFailed,
                $"Basler 相机 {Id} 采集失败且自动重连未恢复");
        }
    }

    /// <summary>打开相机、下发曝光/增益、开始采集。成功返回 true；失败记日志并置未连接。</summary>
    private bool ConnectCore()
    {
        try
        {
            if (_camera.IsOpen)
                _camera.Close();
            _camera.Open();

            var info = _camera.CameraInfo;
            _serialNumber = info is null ? _deviceId : info[CameraInfoKey.SerialNumber];
            _friendlyName = info is null ? "" : info[CameraInfoKey.FriendlyName];

            // 参数下发失败不阻断连接，但记日志（现场排查"图像太暗/太亮"的线索）
            if (_exposureTimeUs is > 0 && !TrySetFloat(PLCamera.ExposureTimeAbs, _exposureTimeUs.Value))
                TrySetFloat(PLCamera.ExposureTime, _exposureTimeUs.Value);
            if (_gain is >= 0)
                TrySetFloat(PLCamera.Gain, _gain.Value);

            _camera.StreamGrabber.Start();
            _connected = true;
            _log?.LogInformation("Basler 相机 {Id} 已连接: SN={Sn} Name={Name}", Id, _serialNumber, _friendlyName);
            return true;
        }
        catch (Exception ex)
        {
            _connected = false;
            _log?.LogWarning(ex, "Basler 相机 {Id} 连接失败", Id);
            return false;
        }
    }

    /// <summary>枚举本机可见的 Basler 相机（序列号 | 名称 | 型号）。未安装 pylon 或无相机时返回空列表。</summary>
    public static IReadOnlyList<string> EnumerateDevices()
    {
        try
        {
            return CameraFinder.Enumerate()
                .Select(i => $"{i[CameraInfoKey.SerialNumber]} | {i[CameraInfoKey.FriendlyName]} | {i[CameraInfoKey.ModelName]}")
                .ToList();
        }
        catch (Exception)
        {
            return [];
        }
    }

    // ---- IExposureControl（供 UI 调光；未连接时返回 null/false） ----

    public bool TrySetExposureTimeUs(double value)
    {
        lock (_grabLock)
        {
            if (_disposed || !_connected)
                return false;
            return TrySetFloat(PLCamera.ExposureTimeAbs, value)
                   || TrySetFloat(PLCamera.ExposureTime, value);
        }
    }

    public bool TrySetGain(double value)
    {
        lock (_grabLock)
        {
            if (_disposed || !_connected)
                return false;
            return TrySetFloat(PLCamera.Gain, value);
        }
    }

    public double? GetExposureTimeUs()
    {
        lock (_grabLock)
        {
            if (_disposed || !_connected)
                return null;
            return TryGetFloat(PLCamera.ExposureTimeAbs) ?? TryGetFloat(PLCamera.ExposureTime);
        }
    }

    public double? GetGain()
    {
        lock (_grabLock)
        {
            if (_disposed || !_connected)
                return null;
            return TryGetFloat(PLCamera.Gain);
        }
    }

    public (double Min, double Max)? GetExposureRange()
    {
        lock (_grabLock)
        {
            if (_disposed || !_connected)
                return null;
            return GetRange(PLCamera.ExposureTimeAbs) ?? GetRange(PLCamera.ExposureTime);
        }
    }

    public (double Min, double Max)? GetGainRange()
    {
        lock (_grabLock)
        {
            if (_disposed || !_connected)
                return null;
            return GetRange(PLCamera.Gain);
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
            var p = _camera.Parameters[name];
            if (p.IsEmpty || !p.IsWritable)
            {
                _log?.LogWarning("Basler 相机 {Id} 参数 {Param} 不可写，下发失败", Id, name.Name);
                return false;
            }
            // 超界时拒绝下发并返回 false，让调用方知道值被改（静默 clamp 会让 UI 调光
            // 显示值与实际值不一致且无任何提示）
            var min = p.GetMinimum();
            var max = p.GetMaximum();
            if (value < min || value > max)
            {
                _log?.LogWarning("Basler 相机 {Id} 参数 {Param} 值 {Value} 超出范围 [{Min}, {Max}]，下发被拒绝",
                    Id, name.Name, value, min, max);
                return false;
            }
            p.SetValue(value);
            return true;
        }
        catch (Exception ex)
        {
            _log?.LogWarning(ex, "Basler 相机 {Id} 参数 {Param} 下发异常", Id, name.Name);
            return false;
        }
    }

    private double? TryGetFloat(FloatName name)
    {
        try
        {
            var p = _camera.Parameters[name];
            if (p.IsEmpty || !p.IsReadable)
                return null;
            return p.GetValue();
        }
        catch (Exception ex)
        {
            _log?.LogWarning(ex, "Basler 相机 {Id} 参数 {Param} 读取异常", Id, name.Name);
            return null;
        }
    }

    private (double Min, double Max)? GetRange(FloatName name)
    {
        try
        {
            var p = _camera.Parameters[name];
            if (p.IsEmpty || !p.IsReadable)
                return null;
            var min = p.GetMinimum();
            var max = p.GetMaximum();
            return max > min ? (min, max) : null;
        }
        catch (Exception ex)
        {
            _log?.LogWarning(ex, "Basler 相机 {Id} 参数 {Param} 范围读取异常", Id, name.Name);
            return null;
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;

        lock (_grabLock)
        {
            try
            {
                if (_camera.IsOpen && _camera.StreamGrabber.IsGrabbing)
                    _camera.StreamGrabber.Stop();
            }
            catch (Exception)
            {
                // 关闭阶段的异常不再向上抛
            }

            try
            {
                if (_camera.IsOpen)
                    _camera.Close();
            }
            catch (Exception)
            {
            }
        }

        _camera.Dispose();
        _converter.Dispose();
    }
}
