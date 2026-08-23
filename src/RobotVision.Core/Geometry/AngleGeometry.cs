using OpenCvSharp;

namespace RobotVision.Core.Geometry;

/// <summary>
/// 角度几何的纯函数集合（可独立单元测试）。
/// 约定：图像坐标系 y 轴向下，角度单位为度，逆时针为正（Atan2 语义）。
/// </summary>
public static class AngleGeometry
{
    /// <summary>
    /// 轮廓最小外接矩形的长边方向，归一化到 [0,180)。
    /// 注意：接近正方形的目标长宽可能互换，长宽比 &lt; 1.5 时不建议使用该模式。
    /// </summary>
    public static (Point2d Center, double AngleDeg) LongAxisFromMinAreaRect(IReadOnlyList<Point2f> contour)
    {
        var rect = Cv2.MinAreaRect(contour);

        // OpenCV >= 4.5 约定 angle ∈ (0, 90]，且不保证 Width 对应长边，
        // 统一换算为长边方向后再归一化。
        var deg = rect.Size.Width >= rect.Size.Height ? rect.Angle : rect.Angle + 90.0;

        return (new Point2d(rect.Center.X, rect.Center.Y), NormalizeDeg(deg));
    }

    /// <summary>两点连线角度（A→B 方向），归一化到 (-180, 180]。</summary>
    public static (Point2d Center, double AngleDeg) FromTwoPoints(Point2d a, Point2d b)
    {
        var center = new Point2d((a.X + b.X) / 2.0, (a.Y + b.Y) / 2.0);
        var deg = Math.Atan2(b.Y - a.Y, b.X - a.X) * 180.0 / Math.PI;
        return (center, NormalizeSignedDeg(deg));
    }

    /// <summary>归一化到 [0,180)：无方向语义的角度（最小外接矩形）。
    /// 取模对 NaN/±Infinity 得 NaN（无异常、不会死循环），语义上即"非有限值不入归一化区间"，
    /// 属可接受的防御行为。</summary>
    public static double NormalizeDeg(double deg)
    {
        var d = deg % 360.0;
        if (d < 0)
            d += 360.0;
        if (d >= 180.0)
            d -= 180.0;
        return d;
    }

    /// <summary>归一化到 (-180,180]：有方向语义的角度（连线方向）。
    /// 用单步数学归一化而非 while 循环——输入 ±Infinity 时循环永远不终止（死循环）；
    /// 非有限输入直接返回原值。公式原生产出 [-180,180)，把 -180 边界回映为 180，
    /// 保持既有 (-180,180] 约定（180 与 -180 代表同一方向，此处只是数值表示约定）。</summary>
    public static double NormalizeSignedDeg(double deg)
    {
        if (!double.IsFinite(deg))
            return deg;
        var d = ((deg + 180.0) % 360.0 + 360.0) % 360.0 - 180.0;
        return d == -180.0 ? 180.0 : d;
    }
}
