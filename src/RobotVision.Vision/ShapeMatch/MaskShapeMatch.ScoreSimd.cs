using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using OpenCvSharp;

namespace RobotVision.Vision;

/// <summary>Chamfer 打分热点：示教边点旋转投影 + 距离场双线性采样（AVX2 批处理）。</summary>
internal static class MaskShapeMatchScoreSimd
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void ProjectModelPoints(
        ReadOnlySpan<Point2f> pts, double ax, double ay,
        double cos, double sin, double mx, double my, Span<double> xs, Span<double> ys)
    {
        var n = pts.Length;
        var i = 0;
        if (Avx2.IsSupported && n >= 4)
        {
            var vCos = Vector256.Create(cos);
            var vSin = Vector256.Create(sin);
            var vMx = Vector256.Create(mx);
            var vMy = Vector256.Create(my);
            for (; i <= n - 4; i += 4)
            {
                var px = Vector256.Create(
                    pts[i].X * ax, pts[i + 1].X * ax, pts[i + 2].X * ax, pts[i + 3].X * ax);
                var py = Vector256.Create(
                    pts[i].Y * ay, pts[i + 1].Y * ay, pts[i + 2].Y * ay, pts[i + 3].Y * ay);
                var x = Avx.Add(Avx.Subtract(Avx.Multiply(px, vCos), Avx.Multiply(py, vSin)), vMx);
                var y = Avx.Add(Avx.Add(Avx.Multiply(px, vSin), Avx.Multiply(py, vCos)), vMy);
                xs[i] = x.GetElement(0);
                xs[i + 1] = x.GetElement(1);
                xs[i + 2] = x.GetElement(2);
                xs[i + 3] = x.GetElement(3);
                ys[i] = y.GetElement(0);
                ys[i + 1] = y.GetElement(1);
                ys[i + 2] = y.GetElement(2);
                ys[i + 3] = y.GetElement(3);
            }
        }

        for (; i < n; i++)
        {
            var ppx = pts[i].X * ax;
            var ppy = pts[i].Y * ay;
            xs[i] = mx + ppx * cos - ppy * sin;
            ys[i] = my + ppx * sin + ppy * cos;
        }
    }

    /// <summary>无向 DT 双线性累加（粗搜快路径：无梯度混合/有向法向搜索）。</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void AccumulateUndirectedDt(
        ReadOnlySpan<float> dt, int w, int h,
        ReadOnlySpan<double> xs, ReadOnlySpan<double> ys,
        ReadOnlySpan<double> weights,
        double hitDistPx, double oobPenalty,
        ref double sum, ref double wsum, ref int hit)
    {
        var n = xs.Length;
        var i = 0;
        var wBound = w - 2;
        var hBound = h - 2;
        for (; i <= n - 4; i += 4)
        {
            AccumulateOne(dt, w, wBound, hBound, xs, ys, weights, hitDistPx, oobPenalty, i, ref sum, ref wsum, ref hit);
            AccumulateOne(dt, w, wBound, hBound, xs, ys, weights, hitDistPx, oobPenalty, i + 1, ref sum, ref wsum, ref hit);
            AccumulateOne(dt, w, wBound, hBound, xs, ys, weights, hitDistPx, oobPenalty, i + 2, ref sum, ref wsum, ref hit);
            AccumulateOne(dt, w, wBound, hBound, xs, ys, weights, hitDistPx, oobPenalty, i + 3, ref sum, ref wsum, ref hit);
        }

        for (; i < n; i++)
            AccumulateOne(dt, w, wBound, hBound, xs, ys, weights, hitDistPx, oobPenalty, i, ref sum, ref wsum, ref hit);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void AccumulateOne(
        ReadOnlySpan<float> dt, int w, int wBound, int hBound,
        ReadOnlySpan<double> xs, ReadOnlySpan<double> ys, ReadOnlySpan<double> weights,
        double hitDistPx, double oobPenalty, int i,
        ref double sum, ref double wsum, ref int hit)
    {
        var wt = weights[i];
        wsum += wt;
        var x = xs[i];
        var y = ys[i];
        if (x < 1 || y < 1 || x >= wBound || y >= hBound)
        {
            sum += oobPenalty * wt;
            return;
        }

        var x0 = (int)Math.Floor(x);
        var y0 = (int)Math.Floor(y);
        var dx = (float)(x - x0);
        var dy = (float)(y - y0);
        var row = y0 * w;
        var i00 = row + x0;
        var v00 = dt[i00];
        var v10 = dt[i00 + 1];
        var v01 = dt[i00 + w];
        var v11 = dt[i00 + w + 1];
        var d = (1 - dx) * (1 - dy) * v00 + dx * (1 - dy) * v10 + (1 - dx) * dy * v01 + dx * dy * v11;
        sum += d * wt;
        if (d <= hitDistPx)
            hit++;
    }
}
