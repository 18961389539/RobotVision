using Microsoft.Extensions.Logging;

namespace RobotVision.Infrastructure.Cameras;

internal static partial class BaslerCameraLog
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Warning,
        Message = "Basler 相机 {Id} 采集失败（{Message}），尝试自动重连")]
    public static partial void GrabFailedRetry(ILogger logger, string id, string message);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Warning,
        Message = "Basler 相机 {Id} 采集异常，尝试自动重连")]
    public static partial void GrabExceptionRetry(ILogger logger, Exception ex, string id);

    [LoggerMessage(
        EventId = 3,
        Level = LogLevel.Information,
        Message = "Basler 相机 {Id} 已连接: SN={Sn} Name={Name}")]
    public static partial void Connected(ILogger logger, string id, string sn, string name);

    [LoggerMessage(
        EventId = 4,
        Level = LogLevel.Warning,
        Message = "Basler 相机 {Id} 连接失败")]
    public static partial void ConnectFailed(ILogger logger, Exception ex, string id);

    [LoggerMessage(
        EventId = 5,
        Level = LogLevel.Warning,
        Message = "Basler 相机 {Id} 按 {Key} 打开失败")]
    public static partial void OpenByKeyFailed(ILogger logger, Exception ex, string id, string key);

    [LoggerMessage(
        EventId = 6,
        Level = LogLevel.Debug,
        Message = "Basler 相机 {Id} 按临时 IP {Ip} 打开失败")]
    public static partial void OpenByTempIpFailed(ILogger logger, Exception ex, string id, string ip);

    [LoggerMessage(
        EventId = 7,
        Level = LogLevel.Information,
        Message = "Basler 相机 {Id} 未指定 DeviceId，现场仅一台，绑定 SN={Sn}")]
    public static partial void SingleDeviceBind(ILogger logger, string id, string sn);

    [LoggerMessage(
        EventId = 8,
        Level = LogLevel.Warning,
        Message = "Basler 相机 {Id} 网段对齐（FORCEIP）未成功，继续按原 IP 尝试")]
    public static partial void ForceIpAlignFailed(ILogger logger, Exception ex, string id);

    [LoggerMessage(
        EventId = 9,
        Level = LogLevel.Debug,
        Message = "Basler 相机 {Id} Close 跳过")]
    public static partial void CloseSkipped(ILogger logger, Exception ex, string id);

    [LoggerMessage(
        EventId = 10,
        Level = LogLevel.Debug,
        Message = "Basler 相机 {Id} Dispose 跳过")]
    public static partial void DisposeSkipped(ILogger logger, Exception ex, string id);

    [LoggerMessage(
        EventId = 11,
        Level = LogLevel.Debug,
        Message = "Basler 相机 {Id} TriggerSelector 设置跳过")]
    public static partial void TriggerSelectorSkipped(ILogger logger, Exception ex, string id);

    [LoggerMessage(
        EventId = 12,
        Level = LogLevel.Debug,
        Message = "Basler 相机 {Id} TriggerMode 设置跳过")]
    public static partial void TriggerModeSkipped(ILogger logger, Exception ex, string id);

    [LoggerMessage(
        EventId = 13,
        Level = LogLevel.Debug,
        Message = "Basler 相机 {Id} ExposureMode 设置跳过")]
    public static partial void ExposureModeSkipped(ILogger logger, Exception ex, string id);

    [LoggerMessage(
        EventId = 14,
        Level = LogLevel.Debug,
        Message = "Basler 相机 {Id} ExposureAuto 设置跳过")]
    public static partial void ExposureAutoSkipped(ILogger logger, Exception ex, string id);

    [LoggerMessage(
        EventId = 15,
        Level = LogLevel.Debug,
        Message = "Basler 相机 {Id} GainAuto 设置跳过")]
    public static partial void GainAutoSkipped(ILogger logger, Exception ex, string id);

    [LoggerMessage(
        EventId = 16,
        Level = LogLevel.Debug,
        Message = "Basler 相机 {Id} TLType 读取跳过")]
    public static partial void TlTypeReadSkipped(ILogger logger, Exception ex, string id);

    [LoggerMessage(
        EventId = 25,
        Level = LogLevel.Information,
        Message = "Basler 相机 {Id} 已启用 2×2 全图降采样 {W}x{H}（{Mode}）")]
    public static partial void Downsample2x2Applied(ILogger logger, string id, int w, int h, string mode);

    [LoggerMessage(
        EventId = 26,
        Level = LogLevel.Warning,
        Message = "Basler 相机 {Id} 无法启用 2×2 全图降采样（无 binning/decimation 或下发失败），将按全幅采集")]
    public static partial void Downsample2x2Failed(ILogger logger, string id);

    [LoggerMessage(
        EventId = 17,
        Level = LogLevel.Warning,
        Message = "Basler 相机 {Id} GigE 全幅 buffer underrun，已开 2×2 全图降采样 {W}x{H}（{Mode}；优化网卡后可重启再试全幅）")]
    public static partial void GigEUnderrunReducedResolution(ILogger logger, string id, int w, int h, string mode);

    [LoggerMessage(
        EventId = 18,
        Level = LogLevel.Warning,
        Message = "Basler 相机 {Id} 曝光 {Value} µs 下发失败（ExposureTimeAbs/ExposureTime 均不可写或超范围）")]
    public static partial void ExposureSetFailed(ILogger logger, string id, double value);

    [LoggerMessage(
        EventId = 19,
        Level = LogLevel.Warning,
        Message = "Basler 相机 {Id} 增益 {Value} 下发失败（Gain/GainAbs/GainRaw 均不可写或超范围）")]
    public static partial void GainSetFailed(ILogger logger, string id, double value);

    [LoggerMessage(
        EventId = 20,
        Level = LogLevel.Debug,
        Message = "Basler 相机 {Id} GainSelector 设置跳过")]
    public static partial void GainSelectorSkipped(ILogger logger, Exception ex, string id);

    [LoggerMessage(
        EventId = 21,
        Level = LogLevel.Warning,
        Message = "Basler 相机 {Id} 参数 {Param} 值 {Value} 超出范围 [{Min}, {Max}]，下发被拒绝")]
    public static partial void ParameterOutOfRange(ILogger logger, string id, string param, double value, double min, double max);

    [LoggerMessage(
        EventId = 22,
        Level = LogLevel.Warning,
        Message = "Basler 相机 {Id} 参数 {Param} 下发异常")]
    public static partial void ParameterSetException(ILogger logger, Exception ex, string id, string param);

    [LoggerMessage(
        EventId = 23,
        Level = LogLevel.Warning,
        Message = "Basler 相机 {Id} 参数 {Param} 读取异常")]
    public static partial void ParameterReadException(ILogger logger, Exception ex, string id, string param);

    [LoggerMessage(
        EventId = 24,
        Level = LogLevel.Warning,
        Message = "Basler 相机 {Id} 参数 {Param} 范围读取异常")]
    public static partial void ParameterRangeReadException(ILogger logger, Exception ex, string id, string param);
}
