namespace RobotVision.Tests;

/// <summary>
/// 旋转矩形对标门槛。产品规格：全链路位置 ≤ 0.1 px、角度 ≤ 0.5°（无向）。
/// 尺寸/RMS 仍作回归防退化；非真机规格书。
/// </summary>
internal static class RotatedRectHalconBenchGates
{
    /// <summary>产品规格：角度上限（°，无向）。</summary>
    public const double SpecAngleDeg = 0.5;

    /// <summary>产品规格：中心位置上限（px，全链路）。</summary>
    public const double SpecCenterPx = 0.1;

    /// <summary>轮廓拟合角误差上限（°，无向）。</summary>
    public const double ContourAngleDeg = SpecAngleDeg;

    /// <summary>全链路（轮廓+亚像素）角误差上限（°）。</summary>
    public const double FullAngleDeg = SpecAngleDeg;

    /// <summary>全链路中心误差上限（px）。</summary>
    public const double CenterPx = SpecCenterPx;

    /// <summary>全链路质量分下限。</summary>
    public const double QualityMin = 0.65;

    /// <summary>全链路 RMS 相对轮廓拟合允许劣化（px）。</summary>
    public const double RmsSlackPx = 0.25;

    /// <summary>高 jitter 轮廓角误差（有种子）。</summary>
    public const double HighJitterContourAngleDeg = SpecAngleDeg;

    /// <summary>缺边轮廓角误差。</summary>
    public const double PartialEdgeAngleDeg = SpecAngleDeg;

    /// <summary>非对称模糊全链路角误差。</summary>
    public const double AsymmetricBlurAngleDeg = SpecAngleDeg;

    /// <summary>现场亚像素 RMS/短边 P50 上限（归一化残差）。</summary>
    public const double FieldNormRmsP50 = 0.06;

    /// <summary>现场亚像素 RMS/短边 P90 上限。</summary>
    public const double FieldNormRmsP90 = 0.15;

    /// <summary>HALCON 引擎 profile（clip=0）轮廓角差 P90（°，standard 矩阵）。</summary>
    public const double TruthContourAngleP90HalconClip = SpecAngleDeg;

    /// <summary>HALCON↔RV 引擎角差上限（°，合成夹具 side-by-side）。</summary>
    public const double EngineAngleGapDeg = SpecAngleDeg;

    /// <summary>HALCON↔RV 引擎中心差上限（px）。</summary>
    public const double EngineCenterGapPx = SpecCenterPx;

    /// <summary>HALCON↔RV 引擎长边差上限（px）。</summary>
    public const double EngineLongGapPx = 0.5;

    /// <summary>HALCON↔RV 引擎短边差上限（px）。</summary>
    public const double EngineShortGapPx = 0.3;

    /// <summary>合成全链路真值角差 P90（°，truth_gaps.csv）。</summary>
    public const double TruthFullAngleP90 = SpecAngleDeg;

    /// <summary>合成全链路真值中心差 P90（px）。</summary>
    public const double TruthFullCenterP90 = SpecCenterPx;

    /// <summary>合成全链路归一化 RMS P90（×短边）。</summary>
    public const double TruthNormRmsP90 = 0.001;

    /// <summary>合成全链路真值长边差 P90（px）。</summary>
    public const double TruthFullLongP90 = 0.05;

    /// <summary>合成全链路真值短边差 P90（px）。</summary>
    public const double TruthFullShortP90 = 0.02;

    /// <summary>合成轮廓级真值长边差 P90（px，standard 矩阵）。</summary>
    public const double TruthContourLongP90 = 0.06;

    /// <summary>合成轮廓级真值短边差 P90（px，standard 矩阵）。</summary>
    public const double TruthContourShortP90 = 0.08;

    /// <summary>HALCON clip=0 profile 下 standard_-18 轮廓长边差上限（px，防回归）。</summary>
    public const double TruthContourLongMinus18HalconClip = 0.10;

    /// <summary>HALCON 引擎 profile（clip=0）轮廓长边 P90（px，standard 矩阵）。</summary>
    public const double TruthContourLongP90HalconClip = 0.06;

    /// <summary>HALCON 引擎 profile（clip=0）轮廓长边最大值（px，standard 六角，避免 P90 因 n=6 取整漏掉最差角）。</summary>
    public const double TruthContourLongMaxHalconClip = 0.06;

    /// <summary>HALCON clip=0 profile 下 standard_135 轮廓长边差上限（px）。</summary>
    public const double TruthContourLong135HalconClip = 0.06;

    /// <summary>HALCON clip=0 高 jitter 轮廓长边差上限（px）。</summary>
    public const double TruthContourLongNoiseHalconClip = 0.08;

    /// <summary>HALCON clip=0 高 jitter 轮廓短边差上限（px）。</summary>
    public const double TruthContourShortNoiseHalconClip = 0.08;

    /// <summary>HALCON clip=0 缺边轮廓长边差上限（px）。</summary>
    public const double TruthContourLongPartialHalconClip = 0.08;

    /// <summary>HALCON 引擎 profile（clip=0）轮廓短边 P90（px，standard 矩阵）。</summary>
    public const double TruthContourShortP90HalconClip = 0.08;
}
