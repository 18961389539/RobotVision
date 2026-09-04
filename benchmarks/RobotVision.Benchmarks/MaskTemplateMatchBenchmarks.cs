using BenchmarkDotNet.Attributes;
using OpenCvSharp;
using RobotVision.Infrastructure.Inference.Strategies;
using RobotVision.Vision;

namespace RobotVision.Benchmarks;

/// <summary>
/// 模板匹配精修耗时：合成 1.3× 转正窗 + 带方向色标的模板，默认 ±5°。
/// 改 MatchBest 搜索策略后应用同一基准对比。
/// 运行：dotnet run -c Release --project benchmarks/RobotVision.Benchmarks -- --filter *MaskTemplateMatch*
/// </summary>
[MemoryDiagnoser]
[SimpleJob(warmupCount: 2, iterationCount: 8)]
public class MaskTemplateMatchBenchmarks
{
    private Mat _template = null!;
    private Mat _upright = null!;
    private Mat _uprightFlipped = null!;

    private RotatedTemplateBank _bank = null!;

    [GlobalSetup]
    public void Setup()
    {
        _template = PaintTemplate();
        _upright = MakeUpright(_template, objectDeg: 3.2);
        _uprightFlipped = MakeUpright(_template, objectDeg: 177.0);
        _bank = MaskTemplateMatcher.CreateRotationBank(_template, refineRangeDeg: 5);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _template.Dispose();
        _upright.Dispose();
        _uprightFlipped.Dispose();
        _bank.Dispose();
    }

    [Benchmark(Baseline = true)]
    public MaskTemplateMatchResult? MatchBest_Residual3Deg() =>
        MaskTemplateMatcher.MatchBest(_upright, _template, refineRangeDeg: 5, minScore: 0.3);

    [Benchmark]
    public MaskTemplateMatchResult? MatchBest_CachedResidual3Deg() =>
        MaskTemplateMatcher.MatchBest(_upright, _template, refineRangeDeg: 5, minScore: 0.3, _bank);

    [Benchmark]
    public MaskTemplateMatchResult? MatchBest_Flipped177Deg() =>
        MaskTemplateMatcher.MatchBest(_uprightFlipped, _template, refineRangeDeg: 5, minScore: 0.3);

    [Benchmark]
    public MaskTemplateMatchResult? MatchBestHybrid_Residual3Deg() =>
        MaskTemplateMatcher.MatchBestHybrid(_upright, _template, refineRangeDeg: 5, minScore: 0.2);

    private static Mat PaintTemplate()
    {
        const int w = 200, h = 80;
        var mat = new Mat(h, w, MatType.CV_8UC3, new Scalar(55, 55, 55));
        for (var x = 10; x < w - 10; x += 12)
            Cv2.Line(mat, new Point(x, 8), new Point(x, h - 8), new Scalar(150, 150, 150), 2);
        Cv2.Circle(mat, new Point(w - 24, h / 2), 12, new Scalar(40, 90, 220), -1);
        return mat;
    }

    private static Mat MakeUpright(Mat template, double objectDeg)
    {
        const int canvas = 480;
        using var full = new Mat(canvas, canvas, MatType.CV_8UC3, new Scalar(55, 55, 55));
        var px = (canvas - template.Width) / 2;
        var py = (canvas - template.Height) / 2;
        template.CopyTo(full[new Rect(px, py, template.Width, template.Height)]);
        using var rotated = new Mat();
        var center = new Point2f(canvas / 2f, canvas / 2f);
        using var m = Cv2.GetRotationMatrix2D(center, objectDeg, 1.0);
        Cv2.WarpAffine(full, rotated, m, new Size(canvas, canvas), InterpolationFlags.Linear,
            BorderTypes.Constant, new Scalar(55, 55, 55));
        var cropW = (int)Math.Ceiling(template.Width * 1.3);
        var cropH = (int)Math.Ceiling(template.Height * 1.3);
        var x = (canvas - cropW) / 2;
        var y = (canvas - cropH) / 2;
        return rotated[new Rect(x, y, cropW, cropH)].Clone();
    }
}
