using OpenCvSharp;
using RobotVision.Vision;

namespace RobotVision.Tests.HalconBench;

/// <summary>HALCON rectangle2 对标夹具矩阵（与合成报告测试一致）。</summary>
internal static class RotatedRectHalconFixtureCatalog
{
    private static readonly RectFitOptions BenchOpt = new()
    {
        StripTabProtrusion = false,
        ClipEndPoints = 2,
    };

    /// <summary>对标 <c>bench_rectangle2.hdev</c> 中 <c>fit_rectangle2_contour_xld</c>（ClippingEndPoints=0）。</summary>
    internal static readonly RectFitOptions HalconEngineOpt = BenchOpt with { ClipEndPoints = 0 };

    /// <summary>保留夹具边缘模式（如 Fuzzy），仅将轮廓 clip 对齐 HALCON（ClippingEndPoints=0）。</summary>
    internal static RectFitOptions HalconProfileOptions(RotatedRectHalconFixture fx) =>
        fx.Options with { ClipEndPoints = 0 };

    static Mat BenchImage(double deg) =>
        RotatedRectBenchSynth.Rectangle(
            RotatedRectBenchSynth.Cx, RotatedRectBenchSynth.Cy, deg,
            RotatedRectBenchSynth.Long / 2, RotatedRectBenchSynth.Short / 2);

    public static IReadOnlyList<RotatedRectHalconFixture> All()
    {
        var list = new List<RotatedRectHalconFixture>();

        foreach (var deg in new[] { -18.0, 0.0, 22.0, 45.0, 88.0, 135.0 })
        {
            var id = $"standard_{deg:0}";
            list.Add(new(
                id,
                "standard",
                deg,
                deg,
                BenchOpt,
                () => BenchImage(deg),
                () => RotatedRectBenchSynth.RectContour(
                    RotatedRectBenchSynth.Cx, RotatedRectBenchSynth.Cy, deg,
                    RotatedRectBenchSynth.Long, RotatedRectBenchSynth.Short, jitter: 0.6)));
        }

        foreach (var blurK in new[] { 3, 5 })
        {
            var id = $"blur_k{blurK}_fuzzy";
            list.Add(new(
                id,
                $"blur_k={blurK}_fuzzy",
                22.0,
                22.0,
                BenchOpt with { EdgeMeasureMode = RectEdgeMeasureMode.Fuzzy },
                () =>
                {
                    using var sharp = BenchImage(22.0);
                    var blurred = new Mat();
                    Cv2.GaussianBlur(sharp, blurred, new Size(blurK | 1, blurK | 1), 0);
                    return blurred;
                },
                () => RotatedRectBenchSynth.RectContour(
                    RotatedRectBenchSynth.Cx, RotatedRectBenchSynth.Cy, 22.0,
                    RotatedRectBenchSynth.Long, RotatedRectBenchSynth.Short, jitter: 0.4)));
        }

        list.Add(new(
            "partial_edge",
            "partial_edge",
            22.0,
            22.0,
            BenchOpt,
            () => BenchImage(22.0),
            () => RotatedRectBenchSynth.PartialEdgeContour(
                RotatedRectBenchSynth.Cx, RotatedRectBenchSynth.Cy, 22.0,
                RotatedRectBenchSynth.Long, RotatedRectBenchSynth.Short, jitter: 0.6)));

        foreach (var jitter in new[] { 0.8, 1.5, 2.5 })
        {
            var id = $"noise_j{jitter:0.0}";
            list.Add(new(
                id,
                $"noise_jitter={jitter:0.0}",
                22.0,
                22.0,
                BenchOpt,
                () => BenchImage(22.0),
                () => RotatedRectBenchSynth.RectContour(
                    RotatedRectBenchSynth.Cx, RotatedRectBenchSynth.Cy, 22.0,
                    RotatedRectBenchSynth.Long, RotatedRectBenchSynth.Short, jitter)));
        }

        return list;
    }
}
