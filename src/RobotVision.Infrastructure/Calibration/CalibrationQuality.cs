namespace RobotVision.Infrastructure.Calibration;

/// <summary>标定质量评估结果（与 README 验收参考对齐）。</summary>
public enum CalibrationQuality
{
    /// <summary>优于验收参考。</summary>
    Good,

    /// <summary>可用（参考区间内）。</summary>
    Fair,

    /// <summary>超标，建议重新标定。</summary>
    Poor,
}

/// <summary>工位坐标映射模式（管线分发用），优先级：多项式 &gt; 外参 &gt; 比例。
/// 见 <see cref="CalibrationManager.GetMappingMode"/>。</summary>
public enum StationMappingMode
{
    /// <summary>无映射档案：外参路径报 1004。</summary>
    None,

    /// <summary>多项式标定（单图模式：原图推理，像素→机器人/棋盘毫米系）。</summary>
    Polynomial,

    /// <summary>外参仿射（去畸变图像推理，像素→机器人系）。</summary>
    Extrinsic,

    /// <summary>比例标定（单图模式：原图推理，像素→图像平面毫米；无标定板工位的回退路径）。</summary>
    Scale,
}
