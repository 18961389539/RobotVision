namespace RobotVision.InferenceBench;

public readonly record struct Percentiles(double P50, double P95, double P99, double Mean, double Min, double Max);

public static class Stats
{
    /// <summary>线性插值分位数；空序列返回 NaN。</summary>
    public static Percentiles Summarize(IReadOnlyList<double> samples)
    {
        if (samples.Count == 0)
            return new Percentiles(double.NaN, double.NaN, double.NaN, double.NaN, double.NaN, double.NaN);

        var sorted = samples.ToArray();
        Array.Sort(sorted);
        var mean = sorted.Average();
        return new Percentiles(
            Percentile(sorted, 50),
            Percentile(sorted, 95),
            Percentile(sorted, 99),
            mean,
            sorted[0],
            sorted[^1]);
    }

    /// <param name="sorted">非空、已升序。</param>
    public static double Percentile(double[] sorted, double p)
    {
        if (sorted.Length == 0)
            return double.NaN;
        if (sorted.Length == 1 || p <= 0)
            return sorted[0];
        if (p >= 100)
            return sorted[^1];

        var index = (p / 100.0) * (sorted.Length - 1);
        var lo = (int)Math.Floor(index);
        var hi = (int)Math.Ceiling(index);
        if (lo == hi)
            return sorted[lo];
        var t = index - lo;
        return sorted[lo] * (1 - t) + sorted[hi] * t;
    }

    public static string Fmt(double ms) =>
        double.IsNaN(ms) ? "n/a" : $"{ms:F2}";
}
