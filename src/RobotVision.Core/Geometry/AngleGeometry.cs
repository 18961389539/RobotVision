using RobotVision.Core.Models;

namespace RobotVision.Core.Geometry;

/// <summary>
/// 角度几何的纯函数集合（可独立单元测试，无第三方库）。
/// 约定：图像坐标系 y 轴向下，角度单位为度，逆时针为正（Atan2 语义）。
/// </summary>
public static class AngleGeometry
{
    /// <summary>两点连线角度（A→B 方向），归一化到 (-180, 180]。</summary>
    public static (ImagePoint Center, double AngleDeg) FromTwoPoints(ImagePoint a, ImagePoint b) =>
        FromTwoPoints(a.X, a.Y, b.X, b.Y);

    /// <summary>两点连线角度（A→B 方向），归一化到 (-180, 180]。</summary>
    public static (ImagePoint Center, double AngleDeg) FromTwoPoints(double ax, double ay, double bx, double by)
    {
        var center = new ImagePoint((ax + bx) / 2.0, (ay + by) / 2.0);
        var deg = Math.Atan2(by - ay, bx - ax) * 180.0 / Math.PI;
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
