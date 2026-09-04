namespace RobotVision.Tests.HalconBench;

/// <summary>形状匹配 HALCON vs RobotVision 引擎 parity 门槛（转正窗内精修）。</summary>
internal static class ShapeMatchHalconBenchGates
{
    /// <summary>同场景两引擎输出角差上限（°）。</summary>
    public const double EngineAngleGapDeg = 0.5;

    /// <summary>同场景两引擎中心距上限（px，转正窗坐标）。</summary>
    public const double EngineCenterGapPx = 8.0;
}
