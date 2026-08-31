using OpenCvSharp;
using RobotVision.Core.Recipe;
using RobotVision.Infrastructure.Inference.Strategies;
using Xunit;

namespace RobotVision.Tests;

/// <summary>
/// 分割+精修模板匹配（MatchBest / MatchBestHybrid）正确性钉：
/// 改搜索策略（粗到细、180° 后置）不得改变角度/头尾/阈值语义。
/// 合成件：右侧有色标的矩形，0° 与 180° 可分。
/// </summary>
public sealed class MaskTemplateMatchTests : IDisposable
{
    private const int TemplateW = 160;
    private const int TemplateH = 64;
    private readonly Mat _template;

    public MaskTemplateMatchTests() => _template = PaintTemplate();

    public void Dispose() => _template.Dispose();

    [Fact]
    public void MatchBest_Aligned_ReturnsNearZero()
    {
        using var upright = MakeUpright(_template, objectDeg: 0);
        var match = MaskTemplateMatcher.MatchBest(upright, _template, refineRangeDeg: 5, minScore: 0.4);
        Assert.NotNull(match);
        Assert.InRange(Signed(match.RotationDeg), -1.0, 1.0);
        Assert.True(match.Score >= 0.4);
        Assert.True(MaskTemplateMatcher.LastDebug.PeakSharpness >= 0.02,
            $"对齐模板主峰应有锐度，实际 {MaskTemplateMatcher.LastDebug.PeakSharpness:0.000}");
    }

    [Theory]
    [InlineData(2.0)]
    [InlineData(-3.5)]
    [InlineData(4.6)]
    public void MatchBest_ResidualWithinRange_RecoversAngle(double residualDeg)
    {
        using var upright = MakeUpright(_template, residualDeg);
        var match = MaskTemplateMatcher.MatchBest(upright, _template, refineRangeDeg: 5, minScore: 0.3);
        Assert.NotNull(match);
        // 现算法在竖条纹+插值偏置下约 ±1°；钉住的是「收回残差」而非「贴 0°」。
        Assert.InRange(Signed(match.RotationDeg - residualDeg), -1.0, 1.0);
        Assert.True(Math.Abs(Signed(match.RotationDeg)) < 90.0, "未翻转时应走 0° 分支");
    }

    [Fact]
    public void MatchBest_Flipped_Picks180Branch()
    {
        using var upright = MakeUpright(_template, objectDeg: 180);
        var match = MaskTemplateMatcher.MatchBest(upright, _template, refineRangeDeg: 5, minScore: 0.3);
        Assert.NotNull(match);
        Assert.True(Math.Abs(Signed(match.RotationDeg)) > 90.0, "头尾翻转应落在 180° 分支");
        var err = Math.Min(
            Math.Abs(Signed(match.RotationDeg - 180)),
            Math.Abs(Signed(match.RotationDeg + 180)));
        Assert.True(err < 1.0, $"180° 对齐误差 {err:0.00}° 过大（得 {match.RotationDeg:0.00}）");
    }

    [Theory]
    [InlineData(177.0)]
    [InlineData(-176.5)]
    public void MatchBest_FlippedWithResidual_RecoversAngle(double objectDeg)
    {
        using var upright = MakeUpright(_template, objectDeg);
        var match = MaskTemplateMatcher.MatchBest(upright, _template, refineRangeDeg: 5, minScore: 0.3);
        Assert.NotNull(match);
        Assert.True(Math.Abs(Signed(match.RotationDeg)) > 90.0);
        var err = Math.Min(
            Math.Abs(Signed(match.RotationDeg - objectDeg)),
            Math.Abs(Signed(match.RotationDeg - objectDeg + 360)));
        Assert.True(err < 1.0, $"翻转+残差误差 {err:0.00}°（得 {match.RotationDeg:0.00}，目标 {objectDeg}）");
    }

    [Fact]
    public void MatchBest_SubDegree_InterpolatesBetweenOneDegreeSteps()
    {
        const double residual = 2.4;
        using var upright = MakeUpright(_template, residual);
        var match = MaskTemplateMatcher.MatchBest(upright, _template, refineRangeDeg: 5, minScore: 0.3);
        Assert.NotNull(match);
        var err = Math.Abs(Signed(match.RotationDeg - residual));
        Assert.True(err < 0.7, $"亚度插值应贴近 {residual}°，实际 {match.RotationDeg:0.00}（误差 {err:0.00}）");
        Assert.True(Math.Abs(Signed(match.RotationDeg)) > 1.0, "不得贴回 0°");
    }

    [Fact]
    public void MatchBest_BelowThreshold_ReturnsNull()
    {
        using var upright = new Mat(120, 240, MatType.CV_8UC3, new Scalar(200, 200, 200));
        var match = MaskTemplateMatcher.MatchBest(upright, _template, refineRangeDeg: 5, minScore: 0.85);
        Assert.Null(match);
    }

    [Fact]
    public void MatchBest_TemplateLargerThanUpright_ReturnsNull()
    {
        using var tiny = new Mat(20, 20, MatType.CV_8UC3, Scalar.All(80));
        var match = MaskTemplateMatcher.MatchBest(tiny, _template, refineRangeDeg: 5, minScore: 0);
        Assert.Null(match);
    }

    [Fact]
    public void MatchBestHybrid_ResidualWithinRange_RecoversAngle()
    {
        const double residual = -3.2;
        using var upright = MakeUpright(_template, residual);
        var match = MaskTemplateMatcher.MatchBestHybrid(upright, _template, refineRangeDeg: 5, minScore: 0.2);
        Assert.NotNull(match);
        Assert.InRange(Signed(match.RotationDeg - residual), -1.0, 1.0);
    }

    [Fact]
    public void SearchCacheDegrees_Range5_CoversFineGridAnd180()
    {
        var deg = MaskTemplateMatcher.SearchCacheDegrees(5);
        Assert.Equal(22, deg.Count);
        Assert.Contains(0, deg);
        Assert.Contains(-5, deg);
        Assert.Contains(5, deg);
        Assert.Contains(180, deg);
        Assert.Contains(175, deg);
        Assert.Contains(185, deg);
        Assert.DoesNotContain(6, deg);
        Assert.DoesNotContain(174, deg);
    }

    [Fact]
    public void MatchBest_WithRotationBank_MatchesLiveRotate()
    {
        using var upright = MakeUpright(_template, objectDeg: 3.2);
        using var bank = MaskTemplateMatcher.CreateRotationBank(_template, 5);
        var live = MaskTemplateMatcher.MatchBest(upright, _template, 5, 0.3);
        var cached = MaskTemplateMatcher.MatchBest(upright, _template, 5, 0.3, bank);
        Assert.NotNull(live);
        Assert.NotNull(cached);
        Assert.Equal(live.RotationDeg, cached.RotationDeg, 6);
        Assert.Equal(live.Score, cached.Score, 6);
    }

    [Fact]
    public void MatchBest_WithRotationBank_FlippedMatchesLive()
    {
        using var upright = MakeUpright(_template, objectDeg: 177);
        using var bank = MaskTemplateMatcher.CreateRotationBank(_template, 5);
        var live = MaskTemplateMatcher.MatchBest(upright, _template, 5, 0.3);
        var cached = MaskTemplateMatcher.MatchBest(upright, _template, 5, 0.3, bank);
        Assert.NotNull(live);
        Assert.NotNull(cached);
        Assert.Equal(live.RotationDeg, cached.RotationDeg, 6);
        Assert.Equal(live.Score, cached.Score, 6);
    }

    [Fact]
    public void RotationCache_Warm_ThenGetOrCreate_ReusesBank()
    {
        var b64 = MaskTemplateMatcher.EncodeTemplatePng(_template);
        var recipe = new RecipeConfig
        {
            Name = "TmplCache",
            CameraId = "cam",
            AngleMode = AngleMode.MaskTemplate,
            Models = ["m.onnx"],
            Template = new TemplateOptions
            {
                TemplateImageBase64 = b64,
                RefineRangeDeg = 5,
            },
        };
        using var cache = new MaskTemplateRotationCache();
        cache.Warm(recipe);
        var a = cache.GetOrCreate(recipe);
        var b = cache.GetOrCreate(recipe);
        Assert.NotNull(a);
        Assert.Same(a, b);
        Assert.Equal(22, a.Gray.Count);
        Assert.Null(a.Edge);

        recipe.Template.UseEdgeMatch = true;
        var edged = cache.GetOrCreate(recipe);
        Assert.NotNull(edged);
        Assert.NotSame(a, edged);
        Assert.NotNull(edged.Edge);
        Assert.Equal(22, edged.Edge.Count);
    }

    [Fact]
    public void RotationCache_Remove_WhileLeased_DoesNotDisposeUntilRelease()
    {
        var recipe = new RecipeConfig
        {
            Name = "Lease",
            CameraId = "cam",
            AngleMode = AngleMode.MaskTemplate,
            Models = ["m.onnx"],
            Template = new TemplateOptions
            {
                TemplateImageBase64 = MaskTemplateMatcher.EncodeTemplatePng(_template),
                RefineRangeDeg = 5,
            },
        };
        using var cache = new MaskTemplateRotationCache();
        var pack = cache.GetOrCreate(recipe);
        Assert.NotNull(pack);
        cache.Remove(recipe.Name);
        Assert.False(pack.Gray.Source.IsDisposed);
        Assert.Equal(22, pack.Gray.Count);

        cache.Release(pack);
        Assert.True(pack.Gray.Source.IsDisposed);
    }

    [Fact]
    public void RotationCache_DisposeWhileLeased_DoesNotDisposeUntilRelease()
    {
        var recipe = new RecipeConfig
        {
            Name = "LeaseDispose",
            CameraId = "cam",
            AngleMode = AngleMode.MaskTemplate,
            Models = ["m.onnx"],
            Template = new TemplateOptions
            {
                TemplateImageBase64 = MaskTemplateMatcher.EncodeTemplatePng(_template),
                RefineRangeDeg = 5,
            },
        };
        var cache = new MaskTemplateRotationCache();
        var pack = cache.GetOrCreate(recipe);
        Assert.NotNull(pack);

        cache.Dispose();
        Assert.False(pack.Gray.Source.IsDisposed);

        cache.Release(pack);
        Assert.True(pack.Gray.Source.IsDisposed);
    }

    [Fact]
    public void PreferOrientationBranch_RealOsdpTemplate_FlippedIs180()
    {
        var path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,
            "..", "..", "..", "..", "..", "data", "replay", "osdp-template.png"));
        if (!File.Exists(path))
            return;
        using var tab = Cv2.ImRead(path, ImreadModes.Color);
        Assert.False(tab.Empty());
        using var flipped = new Mat();
        Cv2.Rotate(tab, flipped, RotateFlags.Rotate180);
        Assert.Equal(0, MaskTemplateMatcher.PreferOrientationBranch(tab, tab));
        Assert.Equal(180, MaskTemplateMatcher.PreferOrientationBranch(tab, flipped));
    }

    [Fact]
    public void PreferOrientationBranch_TabTemplate_SameIsZero_FlippedIs180()
    {
        using var tab = PaintTabTemplate();
        using var upright = MakeUpright(tab, 0, new Scalar(240, 240, 240));
        using var flipped = MakeUpright(tab, 180, new Scalar(240, 240, 240));
        Assert.Equal(0, MaskTemplateMatcher.PreferOrientationBranch(tab, upright));
        Assert.Equal(180, MaskTemplateMatcher.PreferOrientationBranch(tab, flipped));
    }

    [Fact]
    public void MatchBest_NearSymmetricTab_StaysOnZeroWhenUprightMatches()
    {
        using var tab = PaintTabTemplate();
        using var upright = MakeUpright(tab, 0, new Scalar(240, 240, 240));
        var match = MaskTemplateMatcher.MatchBest(upright, tab, refineRangeDeg: 5, minScore: 0.3);
        Assert.NotNull(match);
        Assert.True(Math.Abs(Signed(match.RotationDeg)) < 90.0, $"应走 0° 支，实际 {match.RotationDeg:0.00}");
        Assert.InRange(Signed(match.RotationDeg), -2.0, 2.0);
    }

    [Fact]
    public void MatchBest_NearSymmetricTab_Picks180WhenUprightFlipped()
    {
        using var tab = PaintTabTemplate();
        using var upright = MakeUpright(tab, 180, new Scalar(240, 240, 240));
        var match = MaskTemplateMatcher.MatchBest(upright, tab, refineRangeDeg: 5, minScore: 0.3);
        Assert.NotNull(match);
        Assert.True(Math.Abs(Signed(match.RotationDeg)) > 90.0, $"应走 180° 支，实际 {match.RotationDeg:0.00}");
        var err = Math.Min(
            Math.Abs(Signed(match.RotationDeg - 180)),
            Math.Abs(Signed(match.RotationDeg + 180)));
        Assert.True(err < 2.0, $"180° 对齐误差 {err:0.00}°（得 {match.RotationDeg:0.00}）");
    }

    [Fact]
    public void MatchBest_LargeModuleCrop_IgnoresCenteredBody_UsesTabPolarity()
    {
        var (template, upright, flipped) = PaintOsdpStyleModule();
        using (template)
        using (upright)
        using (flipped)
        {
            var match0 = MaskTemplateMatcher.MatchBest(upright, template, refineRangeDeg: 5, minScore: 0.3);
            Assert.NotNull(match0);
            Assert.True(Math.Abs(Signed(match0.RotationDeg)) < 90.0, $"整机裁剪应走 0° 支，实际 {match0.RotationDeg:0.00}");

            var match180 = MaskTemplateMatcher.MatchBest(flipped, template, refineRangeDeg: 5, minScore: 0.3);
            Assert.NotNull(match180);
            Assert.True(Math.Abs(Signed(match180.RotationDeg)) > 90.0, $"翻转整机应走 180° 支，实际 {match180.RotationDeg:0.00}");
        }
    }

    /// <summary>
    /// 模拟 OSDP 运行时：分割转正窗是整机（大面积暗区几乎居中），示教模板只含底边凸起。
    /// 匹配必须在模板尺度的匹配窗上判头尾，不能被整机质心带偏。
    /// </summary>
    private static (Mat Template, Mat Upright, Mat Flipped) PaintOsdpStyleModule()
    {
        var template = PaintTabTemplate();
        using var inner0 = MakeUpright(template, 0, new Scalar(240, 240, 240));
        using var inner180 = MakeUpright(template, 180, new Scalar(240, 240, 240));
        return (template, PadDarkModule(inner0), PadDarkModule(inner180));
    }

    private static Mat PadDarkModule(Mat inner)
    {
        const int w = 500, h = 400;
        var outer = new Mat(h, w, MatType.CV_8UC3, new Scalar(80, 80, 80));
        var x = (w - inner.Width) / 2;
        var y = (h - inner.Height) / 2;
        inner.CopyTo(outer[new Rect(x, y, inner.Width, inner.Height)]);
        return outer;
    }

    [Fact]
    public void MatchBestHybrid_NearSymmetricTab_Picks180WhenUprightFlipped()
    {
        using var tab = PaintTabTemplate();
        using var upright = MakeUpright(tab, 180, new Scalar(240, 240, 240));
        var match = MaskTemplateMatcher.MatchBestHybrid(upright, tab, refineRangeDeg: 5, minScore: 0.2);
        Assert.NotNull(match);
        Assert.True(Math.Abs(Signed(match.RotationDeg)) > 90.0, $"混合判决应走 180° 支，实际 {match.RotationDeg:0.00}");
    }

    /// <summary>OSDP 类：亮底 + 横条 + 下方暗凸起。近 180° 对称，NCC 不能稳判头尾。</summary>
    private static Mat PaintTabTemplate()
    {
        var mat = new Mat(48, 200, MatType.CV_8UC3, new Scalar(240, 240, 240));
        Cv2.Rectangle(mat, new Point(10, 14), new Point(190, 28), new Scalar(200, 200, 200), -1);
        Cv2.Rectangle(mat, new Point(88, 28), new Point(112, 44), new Scalar(25, 25, 25), -1);
        return mat;
    }

    [Fact]
    public void RefineByLineFit_TooFewPoints_NotFitted()
    {
        Point2f[] contour =
        [
            new(10, 10), new(80, 12), new(78, 40), new(12, 38),
        ];
        var (_, _, fitted) = MaskTemplateMatcher.RefineByLineFit(contour, 0);
        Assert.False(fitted);
    }

    [Fact]
    public void RotationCache_SkipLineFitAndEmptyTemplate()
    {
        using var cache = new MaskTemplateRotationCache();
        var lineFit = new RecipeConfig
        {
            Name = "LF",
            CameraId = "cam",
            AngleMode = AngleMode.MaskTemplate,
            Models = ["m.onnx"],
            Template = new TemplateOptions { RefineMethod = SegmentRefineMethod.LineFit },
        };
        cache.Warm(lineFit);
        Assert.Null(cache.GetOrCreate(lineFit));

        var caliper = new RecipeConfig
        {
            Name = "CT",
            CameraId = "cam",
            AngleMode = AngleMode.MaskTemplate,
            Models = ["m.onnx"],
            Template = new TemplateOptions { RefineMethod = SegmentRefineMethod.CaliperTab },
        };
        cache.Warm(caliper);
        Assert.Null(cache.GetOrCreate(caliper));
    }

    [Fact]
    public void RotationCache_SkipWhenUprightCropOff()
    {
        using var cache = new MaskTemplateRotationCache();
        var recipe = new RecipeConfig
        {
            Name = "NoUpright",
            CameraId = "cam",
            AngleMode = AngleMode.MaskTemplate,
            Models = ["m.onnx"],
            Template = new TemplateOptions
            {
                RefineMethod = SegmentRefineMethod.Template,
                TemplateImageBase64 = MaskTemplateMatcher.EncodeTemplatePng(_template),
                UseUprightCrop = false,
            },
        };
        cache.Warm(recipe);
        Assert.Null(cache.GetOrCreate(recipe));
    }

    private static Mat MakeUpright(Mat template, double objectDeg) =>
        MakeUpright(template, objectDeg, new Scalar(55, 55, 55));

    /// <summary>大画布上旋转目标再裁 1.3× 窗（与运行时 UprightCrop 同结构），避免小图整幅旋转后 0° 模板虚高。</summary>
    private static Mat MakeUpright(Mat template, double objectDeg, Scalar canvasColor)
    {
        const int canvas = 400;
        using var full = new Mat(canvas, canvas, MatType.CV_8UC3, canvasColor);
        var px = (canvas - template.Width) / 2;
        var py = (canvas - template.Height) / 2;
        template.CopyTo(full[new Rect(px, py, template.Width, template.Height)]);

        using var rotated = new Mat();
        if (Math.Abs(objectDeg) < 1e-9)
            full.CopyTo(rotated);
        else
        {
            var center = new Point2f(canvas / 2f, canvas / 2f);
            using var m = Cv2.GetRotationMatrix2D(center, objectDeg, 1.0);
            Cv2.WarpAffine(full, rotated, m, new Size(canvas, canvas), InterpolationFlags.Linear,
                BorderTypes.Constant, canvasColor);
        }

        var cropW = (int)Math.Ceiling(template.Width * 1.3);
        var cropH = (int)Math.Ceiling(template.Height * 1.3);
        var x = (canvas - cropW) / 2;
        var y = (canvas - cropH) / 2;
        return rotated[new Rect(x, y, cropW, cropH)].Clone();
    }

    private static Mat PaintTemplate()
    {
        var mat = new Mat(TemplateH, TemplateW, MatType.CV_8UC3, new Scalar(55, 55, 55));
        for (var x = 10; x < TemplateW - 10; x += 12)
            Cv2.Line(mat, new Point(x, 8), new Point(x, TemplateH - 8), new Scalar(150, 150, 150), 2);
        Cv2.Circle(mat, new Point(TemplateW - 22, TemplateH / 2), 10, new Scalar(40, 90, 220), -1);
        Cv2.Rectangle(mat, new Point(6, 6), new Point(28, TemplateH - 6), new Scalar(30, 30, 30), -1);
        return mat;
    }

    private static double Signed(double deg)
    {
        var d = ((deg + 180.0) % 360.0 + 360.0) % 360.0 - 180.0;
        return d == -180.0 ? 180.0 : d;
    }
}
