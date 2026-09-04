using OpenCvSharp;

namespace RobotVision.Vision;

/// <summary>卡尺梯度极性（对标 HALCON measure_pos 的 transition）。</summary>
public enum RectEdgePolarity
{
    /// <summary>取最强梯度幅值（默认）。</summary>
    Any = 0,

    /// <summary>由暗到亮（正梯度）。</summary>
    DarkToBright = 1,

    /// <summary>由亮到暗（负梯度）。</summary>
    BrightToDark = 2,
}

/// <summary>原图 1D 剖面亚像素取边（双线性采样 + 锐边/模糊边）。</summary>
internal static class RectEdgeSampler
{
    private const double MinGradient = 10.0;
    private const int SamplingHalfWidth = 2;
    private const double FuzzySoftness = 4.0;
    private const double FuzzyMinMembership = 0.12;

    public static bool TryMeasure(
        Mat gray, double bx, double by,
        double alongX, double alongY, double nrmX, double nrmY,
        double nominal, int search, RectEdgePolarity polarity,
        RectEdgeMeasureMode mode, out double offset)
    {
        offset = 0;
        var n = 2 * search + 1;
        Span<double> profile = n <= 256 ? stackalloc double[n] : new double[n];
        for (var i = 0; i < n; i++)
        {
            var r = i - search;
            profile[i] = SampleAveraged(gray, bx + nrmX * (nominal + r), by + nrmY * (nominal + r), alongX, alongY);
        }

        if (mode != RectEdgeMeasureMode.Fuzzy)
            SmoothProfile3Tap(profile);
        return mode == RectEdgeMeasureMode.Fuzzy
            ? TryMeasureFuzzy(profile, search, polarity, out offset)
            : TryMeasureSharp(profile, search, polarity, out offset);
    }

    /// <summary>
    /// 对标 HALCON <c>measure_pairs</c>：单次剖面同时取两侧边，弱边/模糊时比双次单点量测更稳。
    /// 返回沿法向相对探针中心的两侧亚像素偏移（负侧、正侧）。
    /// </summary>
    public static bool TryMeasurePair(
        Mat gray, double bx, double by,
        double alongX, double alongY, double nrmX, double nrmY,
        double halfSpan, int search, RectEdgePolarity polarity,
        RectEdgeMeasureMode mode, out double offsetNeg, out double offsetPos)
    {
        offsetNeg = offsetPos = 0;
        if (halfSpan < 4 || search < 2)
            return false;

        var halfI = (int)Math.Ceiling(halfSpan);
        var span = halfI + search;
        var n = 2 * span + 1;
        Span<double> profile = n <= 512 ? stackalloc double[n] : new double[n];
        for (var i = 0; i < n; i++)
        {
            var r = i - span;
            profile[i] = SampleAveraged(gray, bx + nrmX * r, by + nrmY * r, alongX, alongY);
        }

        if (mode != RectEdgeMeasureMode.Fuzzy)
            SmoothProfile3Tap(profile);

        Span<(int Index, double Offset, double Score)> candidates = stackalloc (int, double, double)[64];
        var candCount = CollectEdgeCandidates(profile, span, polarity, mode, candidates);
        if (candCount < 2)
            return false;

        var expectWidth = 2.0 * halfSpan;
        if (TrySelectDirectedPair(profile, span, mode, expectWidth, out offsetNeg, out offsetPos))
            return true;

        var bestScore = 0.0;
        var bestNeg = 0.0;
        var bestPos = 0.0;
        for (var i = 0; i < candCount; i++)
        {
            var a = candidates[i];
            if (a.Offset >= -0.5)
                continue;
            for (var j = i + 1; j < candCount; j++)
            {
                var b = candidates[j];
                if (b.Offset <= 0.5)
                    continue;
                var width = b.Offset - a.Offset;
                var ratio = width / expectWidth;
                if (ratio < 0.55 || ratio > 1.65)
                    continue;
                var pairScore = a.Score + b.Score;
                if (pairScore > bestScore)
                {
                    bestScore = pairScore;
                    bestNeg = a.Offset;
                    bestPos = b.Offset;
                }
            }
        }

        if (bestScore < MinGradient * 1.2)
            return false;

        offsetNeg = bestNeg;
        offsetPos = bestPos;
        return true;
    }

    private static int CollectEdgeCandidates(
        Span<double> profile, int centerIndex, RectEdgePolarity polarity,
        RectEdgeMeasureMode mode, Span<(int Index, double Offset, double Score)> candidates)
    {
        var n = profile.Length;
        var count = 0;
        for (var i = 1; i < n - 1; i++)
        {
            if (!Finite(profile, i))
                continue;
            double score;
            if (mode == RectEdgeMeasureMode.Fuzzy)
            {
                var g = SignedGrad(profile, i);
                var mag = ScoreGrad(g, polarity);
                if (mag < MinGradient * 0.35)
                    continue;
                var member = 1.0 / (1.0 + Math.Exp(-(mag - MinGradient) / FuzzySoftness));
                if (member < FuzzyMinMembership)
                    continue;
                score = mag * member;
            }
            else
            {
                score = ScoreGrad(SignedGrad(profile, i), polarity);
                if (score < MinGradient)
                    continue;
            }

            var isPeak = true;
            for (var k = -1; k <= 1; k++)
            {
                if (k == 0)
                    continue;
                var neighbor = i + k;
                if (neighbor < 1 || neighbor >= n - 1)
                    continue;
                var ns = ScoreGrad(SignedGrad(profile, neighbor), polarity);
                if (ns > score)
                {
                    isPeak = false;
                    break;
                }
            }
            if (!isPeak || count >= candidates.Length)
                continue;

            var subpix = 0.0;
            if (i - 1 >= 1 && i + 1 <= n - 2)
            {
                var prev = Math.Abs(SignedGrad(profile, i - 1));
                var cur = Math.Abs(SignedGrad(profile, i));
                var next = Math.Abs(SignedGrad(profile, i + 1));
                var denom = prev - 2 * cur + next;
                if (Math.Abs(denom) > 1e-9)
                    subpix = Math.Clamp(0.5 * (prev - next) / denom, -0.5, 0.5);
            }

            candidates[count++] = (i, (i - centerIndex) + subpix, score);
        }

        // 负侧用反向极性再扫一遍（对标 measure_pairs 内外沿 transition 相反）
        var negPolarity = FlipPolarity(polarity);
        for (var i = 1; i < n - 1; i++)
        {
            if (!Finite(profile, i))
                continue;
            var offset = i - centerIndex;
            if (offset >= 0)
                continue;
            var score = mode == RectEdgeMeasureMode.Fuzzy
                ? FuzzyScore(profile, i, negPolarity)
                : ScoreGrad(SignedGrad(profile, i), negPolarity);
            if (score < MinGradient * 0.85)
                continue;
            var dup = false;
            for (var c = 0; c < count; c++)
            {
                if (Math.Abs(candidates[c].Offset - offset) < 0.75)
                {
                    if (score > candidates[c].Score)
                        candidates[c] = (i, offset, score);
                    dup = true;
                    break;
                }
            }
            if (!dup && count < candidates.Length)
                candidates[count++] = (i, offset, score);
        }

        return count;
    }

    /// <summary>亮目标剖面：负侧上升沿 + 正侧下降沿成对（对标 measure_pairs transition 组合）。</summary>
    private static bool TrySelectDirectedPair(
        Span<double> profile, int centerIndex, RectEdgeMeasureMode mode,
        double expectWidth, out double offsetNeg, out double offsetPos)
    {
        offsetNeg = offsetPos = 0;
        Span<(double Offset, double Score)> rising = stackalloc (double, double)[24];
        Span<(double Offset, double Score)> falling = stackalloc (double, double)[24];
        var riseCount = 0;
        var fallCount = 0;
        var n = profile.Length;

        for (var i = 1; i < n - 1; i++)
        {
            if (!Finite(profile, i))
                continue;
            var offset = (i - centerIndex) + SubpixOffset(profile, i);
            var g = SignedGrad(profile, i);
            var rise = ScoreGrad(g, RectEdgePolarity.DarkToBright);
            var fall = ScoreGrad(g, RectEdgePolarity.BrightToDark);
            if (mode == RectEdgeMeasureMode.Fuzzy)
            {
                rise = FuzzyScore(profile, i, RectEdgePolarity.DarkToBright);
                fall = FuzzyScore(profile, i, RectEdgePolarity.BrightToDark);
            }
            else
            {
                if (rise >= MinGradient && IsLocalPeak(profile, i, RectEdgePolarity.DarkToBright) && riseCount < rising.Length)
                    rising[riseCount++] = (offset, rise);
                if (fall >= MinGradient && IsLocalPeak(profile, i, RectEdgePolarity.BrightToDark) && fallCount < falling.Length)
                    falling[fallCount++] = (offset, fall);
                continue;
            }
            if (rise >= MinGradient * 0.35 && riseCount < rising.Length)
                rising[riseCount++] = (offset, rise);
            if (fall >= MinGradient * 0.35 && fallCount < falling.Length)
                falling[fallCount++] = (offset, fall);
        }

        var bestScore = 0.0;
        var bestNeg = 0.0;
        var bestPos = 0.0;
        for (var ri = 0; ri < riseCount; ri++)
        {
            var r = rising[ri];
            if (r.Offset >= -0.5)
                continue;
            for (var fi = 0; fi < fallCount; fi++)
            {
                var f = falling[fi];
                if (f.Offset <= 0.5)
                    continue;
                var ratio = (f.Offset - r.Offset) / expectWidth;
                if (ratio < 0.55 || ratio > 1.65)
                    continue;
                var pairScore = r.Score + f.Score;
                if (pairScore > bestScore)
                {
                    bestScore = pairScore;
                    bestNeg = r.Offset;
                    bestPos = f.Offset;
                }
            }
        }

        if (bestScore < MinGradient * 1.2)
            return false;
        offsetNeg = bestNeg;
        offsetPos = bestPos;
        return true;
    }

    private static bool IsLocalPeak(Span<double> profile, int i, RectEdgePolarity polarity)
    {
        var score = ScoreGrad(SignedGrad(profile, i), polarity);
        for (var k = -1; k <= 1; k++)
        {
            if (k == 0)
                continue;
            var j = i + k;
            if (j < 1 || j >= profile.Length - 1)
                continue;
            if (ScoreGrad(SignedGrad(profile, j), polarity) > score)
                return false;
        }
        return true;
    }

    private static double SubpixOffset(Span<double> profile, int i)
    {
        if (i - 1 < 1 || i + 1 > profile.Length - 2)
            return 0;
        var prev = Math.Abs(SignedGrad(profile, i - 1));
        var cur = Math.Abs(SignedGrad(profile, i));
        var next = Math.Abs(SignedGrad(profile, i + 1));
        var denom = prev - 2 * cur + next;
        return Math.Abs(denom) > 1e-9 ? Math.Clamp(0.5 * (prev - next) / denom, -0.5, 0.5) : 0;
    }

    private static double FuzzyScore(Span<double> profile, int i, RectEdgePolarity polarity)
    {
        var g = SignedGrad(profile, i);
        var mag = ScoreGrad(g, polarity);
        if (mag < MinGradient * 0.35)
            return 0;
        var member = 1.0 / (1.0 + Math.Exp(-(mag - MinGradient) / FuzzySoftness));
        return mag * member;
    }

    private static RectEdgePolarity FlipPolarity(RectEdgePolarity p) => p switch
    {
        RectEdgePolarity.DarkToBright => RectEdgePolarity.BrightToDark,
        RectEdgePolarity.BrightToDark => RectEdgePolarity.DarkToBright,
        _ => RectEdgePolarity.Any,
    };

    private static bool TryMeasureSharp(Span<double> profile, int search, RectEdgePolarity polarity, out double offset)
    {
        offset = 0;
        var n = profile.Length;
        var bestI = -1;
        var bestScore = MinGradient;
        for (var i = 1; i < n - 1; i++)
        {
            if (!Finite(profile, i))
                continue;
            var score = ScoreGrad(SignedGrad(profile, i), polarity);
            if (score >= bestScore)
            {
                bestScore = score;
                bestI = i;
            }
        }
        if (bestI < 0)
            return false;

        var subpix = 0.0;
        if (bestI - 1 >= 1 && bestI + 1 <= n - 2)
        {
            var prev = Math.Abs(SignedGrad(profile, bestI - 1));
            var cur = Math.Abs(SignedGrad(profile, bestI));
            var next = Math.Abs(SignedGrad(profile, bestI + 1));
            var denom = prev - 2 * cur + next;
            if (Math.Abs(denom) > 1e-9)
                subpix = Math.Clamp(0.5 * (prev - next) / denom, -0.5, 0.5);
        }

        offset = (bestI - search) + subpix;
        return Math.Abs(offset) <= search + 0.5;
    }

    /// <summary>模糊边：3-tap 平滑 + 梯度 S 形隶属度加权重心（对标 fuzzy_measure 软阈值）。</summary>
    private static bool TryMeasureFuzzy(Span<double> profile, int search, RectEdgePolarity polarity, out double offset)
    {
        offset = 0;
        var n = profile.Length;
        Span<double> smooth = n <= 256 ? stackalloc double[n] : new double[n];
        for (var i = 0; i < n; i++)
        {
            if (i == 0 || i == n - 1)
                smooth[i] = profile[i];
            else
                smooth[i] = 0.25 * profile[i - 1] + 0.5 * profile[i] + 0.25 * profile[i + 1];
        }

        var weightSum = 0.0;
        var posSum = 0.0;
        for (var i = 1; i < n - 1; i++)
        {
            if (!Finite(smooth, i))
                continue;
            var g = SignedGrad(smooth, i);
            var mag = ScoreGrad(g, polarity);
            if (mag < MinGradient * 0.35)
                continue;
            var member = 1.0 / (1.0 + Math.Exp(-(mag - MinGradient) / FuzzySoftness));
            if (member < FuzzyMinMembership)
                continue;
            weightSum += member;
            posSum += member * (i - search);
        }
        if (weightSum < 1e-6)
            return false;

        offset = posSum / weightSum;
        return Math.Abs(offset) <= search + 0.5;
    }

    private static double ScoreGrad(double g, RectEdgePolarity polarity) => polarity switch
    {
        RectEdgePolarity.DarkToBright => g > 0 ? g : 0,
        RectEdgePolarity.BrightToDark => g < 0 ? -g : 0,
        _ => Math.Abs(g),
    };

    /// <summary>对标 HALCON measure sigma≈1：三点高斯平滑后再取梯度峰。</summary>
    private static void SmoothProfile3Tap(Span<double> profile)
    {
        var n = profile.Length;
        if (n < 3)
            return;
        Span<double> tmp = n <= 512 ? stackalloc double[n] : new double[n];
        profile.CopyTo(tmp);
        for (var i = 1; i < n - 1; i++)
            profile[i] = 0.25 * tmp[i - 1] + 0.5 * tmp[i] + 0.25 * tmp[i + 1];
    }

    private static double SignedGrad(Span<double> p, int k) => p[k + 1] - p[k - 1];

    private static bool Finite(Span<double> p, int i) =>
        double.IsFinite(p[i - 1]) && double.IsFinite(p[i]) && double.IsFinite(p[i + 1]);

    private static double SampleAveraged(Mat gray, double x, double y, double dx, double dy)
    {
        var sum = 0.0;
        var count = 0;
        for (var k = -SamplingHalfWidth; k <= SamplingHalfWidth; k++)
        {
            var v = SampleBilinear(gray, x + k * dx, y + k * dy);
            if (double.IsFinite(v))
            {
                sum += v;
                count++;
            }
        }
        return count == 0 ? double.NaN : sum / count;
    }

    private static double SampleBilinear(Mat gray, double x, double y)
    {
        var w = gray.Width;
        var h = gray.Height;
        if (x < 0 || y < 0 || x >= w - 1 || y >= h - 1)
            return double.NaN;
        var x0 = (int)Math.Floor(x);
        var y0 = (int)Math.Floor(y);
        var fx = x - x0;
        var fy = y - y0;
        return (1 - fx) * (1 - fy) * gray.At<byte>(y0, x0)
             + fx * (1 - fy) * gray.At<byte>(y0, x0 + 1)
             + (1 - fx) * fy * gray.At<byte>(y0 + 1, x0)
             + fx * fy * gray.At<byte>(y0 + 1, x0 + 1);
    }
}
