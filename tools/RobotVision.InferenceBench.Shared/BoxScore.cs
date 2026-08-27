namespace RobotVision.InferenceBench;

public sealed class BoxScore
{
    public string Label { get; set; } = "";
    public double Score { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    public int W { get; set; }
    public int H { get; set; }

    public double Cx => X + W / 2.0;
    public double Cy => Y + H / 2.0;
}

public static class FingerprintCompare
{
    public static (double MaxScoreDelta, double MaxCenterPx, int Compared) Compare(
        IReadOnlyList<BoxScore> cpu, IReadOnlyList<BoxScore> other)
    {
        var n = Math.Min(cpu.Count, other.Count);
        if (n == 0)
            return (double.NaN, double.NaN, 0);

        var maxScore = 0.0;
        var maxCenter = 0.0;
        for (var i = 0; i < n; i++)
        {
            maxScore = Math.Max(maxScore, Math.Abs(cpu[i].Score - other[i].Score));
            var dx = cpu[i].Cx - other[i].Cx;
            var dy = cpu[i].Cy - other[i].Cy;
            maxCenter = Math.Max(maxCenter, Math.Sqrt(dx * dx + dy * dy));
        }

        return (maxScore, maxCenter, n);
    }
}
