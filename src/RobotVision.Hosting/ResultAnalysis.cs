using System.Globalization;

namespace RobotVision.Hosting;

/// <summary>角度直方图一根柱（分析页与对话工具共用）。</summary>
public sealed record ResultHistogramBar(string Label, int Count, double Ratio, double Start, double End);

/// <summary>位姿/耗时离散度（AVG 跳过 NULL，失败行通常无坐标）。</summary>
public sealed record ResultPoseSpread(
    double? MinX, double? MaxX, double? StdX,
    double? MinY, double? MaxY, double? StdY,
    double? MinAngle, double? MaxAngle, double? StdAngle,
    double? MinMs, double? MaxMs, double? StdMs,
    double? AvgConfidence);

/// <summary>按配方聚合（分析页「按配方」与 AI by_recipe）。</summary>
public sealed record ResultRecipeStat(
    string Recipe, long Total, long Ok, long Failed, double? AvgMs, double? AvgAngle);

/// <summary>XY 散点（分析页位姿图）。</summary>
public sealed record ResultXyPoint(double X, double Y, int Code);

/// <summary>时间桶（小时或日）。</summary>
public sealed record ResultTrendBucket(string Label, long Total, long Ok, long Failed);

/// <summary>分析页与对话共用的分箱、结果码文案、总体标准差。</summary>
public static class ResultAnalysis
{
    public const int DefaultHistogramBins = 12;

    public static IReadOnlyList<ResultHistogramBar> BuildHistogram(
        IReadOnlyList<double> values, int binCount = DefaultHistogramBins, string unit = "°")
    {
        if (values.Count == 0 || binCount < 1)
            return [];
        var min = values.Min();
        var max = values.Max();
        if (!double.IsFinite(min) || !double.IsFinite(max))
            return [];
        if (Math.Abs(max - min) < 1e-9)
        {
            var pad = Math.Max(0.05, Math.Abs(min) * 1e-3);
            return [new ResultHistogramBar(
                $"{min.ToString("0.###", CultureInfo.InvariantCulture)}{unit}",
                values.Count, 1, min - pad, min + pad)];
        }

        var span = max - min;
        var bins = new int[binCount];
        foreach (var value in values)
        {
            if (!double.IsFinite(value))
                continue;
            var index = (int)((value - min) / span * binCount);
            if (index >= binCount)
                index = binCount - 1;
            if (index < 0)
                index = 0;
            bins[index]++;
        }

        var peak = bins.Max();
        var bars = new List<ResultHistogramBar>(binCount);
        for (var i = 0; i < binCount; i++)
        {
            var start = min + span * i / binCount;
            var end = min + span * (i + 1) / binCount;
            var ratio = peak == 0 ? 0 : (double)bins[i] / peak;
            bars.Add(new ResultHistogramBar(
                $"{start.ToString("0.0", CultureInfo.InvariantCulture)}~{end.ToString("0.0", CultureInfo.InvariantCulture)}{unit}",
                bins[i], ratio, start, end));
        }
        return bars;
    }

    public static double? PopulationStd(double? avg, double? avgSquare)
    {
        if (avg is null || avgSquare is null)
            return null;
        var v = avgSquare.Value - avg.Value * avg.Value;
        if (v <= 1e-18)
            return 0;
        return Math.Sqrt(v);
    }

    public static string DescribeCode(int code) => code switch
    {
        0 => "合格",
        1000 => "1000 未知命令",
        1001 => "1001 未知配方",
        1002 => "1002 相机未注册",
        1003 => "1003 取图失败",
        1004 => "1004 未标定",
        1005 => "1005 模型不可用",
        1006 => "1006 光源未注册",
        1007 => "1007 未检出",
        1008 => "1008 处理超时",
        1009 => "1009 忙碌",
        1010 => "1010 排队超时",
        1011 => "1011 相机初始化失败",
        1012 => "1012 位姿不符",
        1013 => "1013 参数错误",
        1014 => "1014 需要位姿",
        1015 => "1015 配方停用",
        1016 => "1016 配方无效",
        1017 => "1017 资产不一致",
        1018 => "1018 过程联锁",
        1019 => "1019 精修失败",
        1020 => "1020 光源指令失败",
        1099 => "1099 内部错误",
        _ => code.ToString(CultureInfo.InvariantCulture),
    };
}
