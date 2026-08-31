namespace RobotVision.Core.Models;

/// <summary>像素坐标系下的检测结果（图像坐标，y 轴向下）。</summary>
/// <param name="Cx">中心 x（px）。</param>
/// <param name="Cy">中心 y（px）。</param>
/// <param name="AngleDeg">角度（度）。</param>
/// <param name="Score">置信度。</param>
public sealed record PixelPose(double Cx, double Cy, double AngleDeg, double Score)
{
    /// <summary>检测叠加数据（可选）：mask 轮廓/检测框/关键点，全图像素坐标。
    /// 仅供画面绘制（OverlayDrawer），不参与位姿计算与标定变换。</summary>
    public PoseOverlay? Overlay { get; init; }

    /// <summary>分割实例置信度；非分割策略为 null。对外 <see cref="Score"/> 在精修模式下是精修质量。</summary>
    public double? SegmentScore { get; init; }

    /// <summary>
    /// false = 分割到了但精修未过门，不得输出给机器人（画面仍可画，箭头仅供对照）。
    /// 默认 true，其它角度模式行为不变。
    /// </summary>
    public bool Usable { get; init; } = true;
}

/// <summary>生产输出门：快照可含不可用位姿，机器人只收 Usable。</summary>
public static class PixelPoseOutput
{
    /// <summary>无目标 → 1007；有分割但无一精修过门 → 1019；否则可输出。</summary>
    public static VisionErrorCode? RejectReason(IReadOnlyList<PixelPose> poses)
    {
        if (poses.Count == 0)
            return VisionErrorCode.NoTargetFound;
        for (var i = 0; i < poses.Count; i++)
        {
            if (poses[i].Usable)
                return null;
        }

        return VisionErrorCode.RefineFailed;
    }

    public static List<PixelPose> UsableOnly(IReadOnlyList<PixelPose> poses)
    {
        var list = new List<PixelPose>(poses.Count);
        for (var i = 0; i < poses.Count; i++)
        {
            if (poses[i].Usable)
                list.Add(poses[i]);
        }

        return list;
    }

    /// <summary>期望件数 &gt;0 且过门件数不符时，全部标不可用（TRIGGER 1019，画面仍能看到多检）。</summary>
    public static void EnforceExpectedCount(IList<PixelPose> poses, int expectedCount)
    {
        if (expectedCount <= 0 || poses.Count == 0)
            return;
        var usable = 0;
        for (var i = 0; i < poses.Count; i++)
        {
            if (poses[i].Usable)
                usable++;
        }

        if (usable == expectedCount)
            return;
        for (var i = 0; i < poses.Count; i++)
            poses[i] = poses[i] with { Usable = false };
    }
}

/// <summary>全图像素坐标点。</summary>
public readonly record struct PixelPoint(double X, double Y);

/// <summary>全图像素坐标矩形。</summary>
public readonly record struct PixelRect(double X, double Y, double Width, double Height);

/// <summary>
/// 单个目标的检测叠加数据（全图像素坐标系）：附着于 <see cref="PixelPose"/> 随快照发布，
/// 仅供画面绘制，不参与位姿计算与标定变换。
/// 各字段按策略类型填充：分割/BLOB 给轮廓，检测给框（主/次各一），姿态给关键点。
/// </summary>
public sealed record PoseOverlay
{
    /// <summary>目标轮廓（闭合折线）；分割/BLOB 模式提供。</summary>
    public IReadOnlyList<PixelPoint>? Contour { get; init; }

    /// <summary>检测框（单模型 1 个，双模型主/次 2 个）；检测/姿态模式提供。</summary>
    public IReadOnlyList<PixelRect>? Boxes { get; init; }

    /// <summary>关键点（与 <see cref="KeyPointConfidences"/> 等长）；姿态模式提供。</summary>
    public IReadOnlyList<PixelPoint>? KeyPoints { get; init; }

    /// <summary>关键点置信度；低置信关键点绘制时灰显。可为 null。</summary>
    public IReadOnlyList<double>? KeyPointConfidences { get; init; }

    /// <summary>角度基线（恰好 2 点：起点=主特征中心，终点=次特征中心）；
    /// 双特征模式（双模型/双BLOB）提供，用于画面上验证配对关系。
    /// 卡尺+凸起时为中心 → 暗凸起一侧。</summary>
    public IReadOnlyList<PixelPoint>? Baseline { get; init; }

    /// <summary>精修调试线段（卡尺搜索条、拟合边、无效探针）；仅画面用。</summary>
    public IReadOnlyList<OverlayLine>? DebugLines { get; init; }

    /// <summary>精修调试点（抓边内点/剔点）；仅画面用，不连成骨架。</summary>
    public IReadOnlyList<OverlayDot>? DebugDots { get; init; }

    /// <summary>
    /// 运行时匹配窗四角（示教模板矩形绕匹配峰按输出角旋转）。
    /// 与配方「特征」橙框分开：橙框是示教裁剪、全图固定；本窗跟峰走。
    /// </summary>
    public IReadOnlyList<PixelPoint>? MatchWindow { get; init; }

    /// <summary>类别标签（模型输出）；纯图像处理模式为 null。</summary>
    public string? Label { get; init; }

    /// <summary>分割位掩码（bbox 尺寸、LSB-first）；试触发赛马孔槽用，可空。</summary>
    public byte[]? BitPackedMask { get; init; }

    public int MaskWidth { get; init; }

    public int MaskHeight { get; init; }

    /// <summary>
    /// 示教模板矩形绕匹配峰旋转后的四角（与 AngleGeometry 同口径：y 向下，逆时针为正）。
    /// 宽=模板列数，高=模板行数（示教时已转正，宽沿长边）。
    /// </summary>
    public static PixelPoint[] TemplateMatchWindow(
        double cx, double cy, double angleDeg, double widthPx, double heightPx)
    {
        var hw = widthPx / 2.0;
        var hh = heightPx / 2.0;
        var rad = angleDeg * Math.PI / 180.0;
        var cos = Math.Cos(rad);
        var sin = Math.Sin(rad);
        PixelPoint Map(double x, double y) =>
            new(cx + x * cos - y * sin, cy + x * sin + y * cos);
        return [Map(-hw, -hh), Map(hw, -hh), Map(hw, hh), Map(-hw, hh)];
    }
}

/// <summary>叠加调试线段。</summary>
public readonly record struct OverlayLine(PixelPoint From, PixelPoint To, OverlayLineKind Kind);

/// <summary>叠加调试点。</summary>
public readonly record struct OverlayDot(PixelPoint At, OverlayDotKind Kind);

public enum OverlayLineKind
{
    Caliper,
    FittedEdge,
    InvalidCaliper,
}

public enum OverlayDotKind
{
    Inlier,
    Rejected,
}

/// <summary>机器人坐标系下的输出位姿。</summary>
public sealed record RobotPose(double X, double Y, double AngleDeg);
