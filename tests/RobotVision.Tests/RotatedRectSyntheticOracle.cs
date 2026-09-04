using OpenCvSharp;
using RobotVision.Vision;
using Xunit;

namespace RobotVision.Tests;

/// <summary>
/// 合成 ground truth 几何 Oracle（替代现场 HALCON 真值，用于数值 side-by-side）。
/// </summary>
internal static class RotatedRectSyntheticOracle
{
    public readonly record struct Truth(
        Point2d Center,
        double AngleDeg,
        double LongLen,
        double ShortLen);

    public readonly record struct Delta(
        double AngleDeg,
        double CenterPx,
        double LongPx,
        double ShortPx);

    public static Truth From(double cx, double cy, double angleDeg, double longLen, double shortLen) =>
        new(new Point2d(cx, cy), angleDeg, longLen, shortLen);

    public static Delta Compare(RotatedRectFitResult fit, Truth truth)
    {
        if (!fit.Ok)
            return new(double.NaN, double.NaN, double.NaN, double.NaN);

        var ang = UndirectedErr(fit.AngleDeg, truth.AngleDeg);
        var cx = fit.Center.X - truth.Center.X;
        var cy = fit.Center.Y - truth.Center.Y;
        var centerPx = Math.Sqrt(cx * cx + cy * cy);
        var longPx = Math.Min(Math.Abs(fit.LongLen - truth.LongLen), Math.Abs(fit.LongLen - truth.ShortLen));
        var shortPx = Math.Min(Math.Abs(fit.ShortLen - truth.ShortLen), Math.Abs(fit.ShortLen - truth.LongLen));
        return new(ang, centerPx, longPx, shortPx);
    }

    public static Delta Compare(RotatedRectSubpixel.Result fit, Truth truth)
    {
        var ang = UndirectedErr(fit.AngleDeg, truth.AngleDeg);
        var cx = fit.Center.X - truth.Center.X;
        var cy = fit.Center.Y - truth.Center.Y;
        var centerPx = Math.Sqrt(cx * cx + cy * cy);
        var longPx = Math.Min(Math.Abs(fit.LongLen - truth.LongLen), Math.Abs(fit.LongLen - truth.ShortLen));
        var shortPx = Math.Min(Math.Abs(fit.ShortLen - truth.ShortLen), Math.Abs(fit.ShortLen - truth.LongLen));
        return new(ang, centerPx, longPx, shortPx);
    }

    /// <summary>HALCON 级合成精度门槛（条纹矩形 + 亚像素）。</summary>
    public static void AssertHalconGrade(Delta d, string tag)
    {
        Assert.True(double.IsFinite(d.AngleDeg) && d.AngleDeg < RotatedRectHalconBenchGates.SpecAngleDeg,
            $"{tag} 角 Δ={d.AngleDeg:0.000}°");
        Assert.True(d.CenterPx < RotatedRectHalconBenchGates.SpecCenterPx, $"{tag} 中心 Δ={d.CenterPx:0.00}px");
        Assert.True(d.LongPx < 3.5 && d.ShortPx < 2.0, $"{tag} 尺寸 ΔL={d.LongPx:0.00} ΔS={d.ShortPx:0.00}px");
    }

    private static double UndirectedErr(double got, double truth)
    {
        var d = Math.Abs(got - truth);
        return Math.Min(d, 180 - d);
    }
}
