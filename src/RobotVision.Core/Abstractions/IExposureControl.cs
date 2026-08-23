namespace RobotVision.Core.Abstractions;

/// <summary>
/// 曝光/增益运行时调光能力（由支持该能力的相机实现，如 BaslerCamera）。
/// UI 按接口查询而非强转具体品牌：接入海康等其他品牌时若实现本接口，调光 UI 自动可用。
/// 未连接/不支持时方法返回 null/false，不抛异常。
/// </summary>
public interface IExposureControl
{
    bool TrySetExposureTimeUs(double value);

    bool TrySetGain(double value);

    double? GetExposureTimeUs();

    double? GetGain();

    (double Min, double Max)? GetExposureRange();

    (double Min, double Max)? GetGainRange();
}
