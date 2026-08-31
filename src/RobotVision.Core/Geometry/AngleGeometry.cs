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

    /// <summary>
    /// 几何无向角 [0,180) 与纹理/极性有向角融合：短轴中心/长边来自几何，头尾来自 signedDeg。
    /// 若 signed 与无向角相差 ≥90°，视为翻面，输出 geo+180。
    /// </summary>
    public static double FuseDirected(double undirectedDeg, double signedDeg)
    {
        var geo = NormalizeDeg(undirectedDeg);
        var tpl = NormalizeSignedDeg(signedDeg);
        var delta = Math.Abs(NormalizeSignedDeg(tpl - geo));
        return delta >= 90.0 ? NormalizeSignedDeg(geo + 180.0) : NormalizeSignedDeg(geo);
    }

    /// <summary>两无向角的最小夹角，落在 [0,90]。</summary>
    public static double UndirectedDeltaDeg(double a, double b)
    {
        var d = Math.Abs(NormalizeDeg(a) - NormalizeDeg(b));
        return d > 90.0 ? 180.0 - d : d;
    }

    /// <summary>
    /// 圆标准差（度）。<paramref name="period"/> 为 360（有向）或 180（无向）。
    /// 有效样本少于 2 返回 0。
    /// </summary>
    public static double CircularStdDeg(IReadOnlyList<double> degrees, double period = 360)
    {
        if (degrees.Count < 2 || period <= 0)
            return 0;
        var radPer = 2 * Math.PI / period;
        var x = 0.0;
        var y = 0.0;
        var n = 0;
        foreach (var d in degrees)
        {
            if (!double.IsFinite(d))
                continue;
            var a = d * radPer;
            x += Math.Cos(a);
            y += Math.Sin(a);
            n++;
        }

        if (n < 2)
            return 0;
        var r = Math.Clamp(Math.Sqrt(x * x + y * y) / n, 1e-12, 1);
        return Math.Sqrt(-2 * Math.Log(r)) / radPer;
    }
}
