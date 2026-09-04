using OpenCvSharp;
using RobotVision.Core.Models;
using RobotVision.Core.Recipe;
using RobotVision.Infrastructure.Inference.Strategies;
using RobotVision.Vision;
using Xunit;
namespace RobotVision.Tests;

/// <summary>LineFit 运行时走 JLVision 轮廓直线拟合（失败再 measure）。</summary>
public sealed class LineFitSegmentRefineRuntimeTests
{
    [Fact]
    public void ContourLineFit_RecoversStripeAngle()
    {
        const double trueDeg = 33.0;
        var center = new Point2d(200, 150);
        using var gray = Stripe(400, 300, center, trueDeg, halfShort: 26);
        var contour = RectContour(center, trueDeg, 180, 52, jitter: 0.5);

        var recipe = new RecipeConfig
        {
            Name = "LineFitTest",
            Template = new TemplateOptions
            {
                RefineMethod = SegmentRefineMethod.LineFit,
                HousingEdgePolarity = HousingEdgePolarity.Auto,
            },
        };

        using var roi = new Mat();
        gray.CopyTo(roi);
        var request = new SegmentRefineRequest
        {
            RoiView = roi,
            Points = contour,
            SegmentConfidence = 0.9,
            Recipe = recipe,
        };

        var hit = new LineFitSegmentRefineRuntime().Refine(request);
        Assert.NotNull(hit.Pose);
        Assert.True(hit.Pose!.Usable);
        Assert.True(UndirectedErr(hit.Pose.AngleDeg, trueDeg) < 0.25);
        Assert.True(Math.Abs(hit.Pose.Cx - center.X) < 2 && Math.Abs(hit.Pose.Cy - center.Y) < 2);
        Assert.Contains("JLVision line", hit.QualityNote ?? "", StringComparison.Ordinal);
    }

    [Fact]
    public void Subpixel_FixAngleOff_AllowsAngleRefinement()
    {
        const double trueDeg = 33.0;
        var center = new Point2d(200, 150);
        using var gray = Stripe(400, 300, center, trueDeg, halfShort: 26);
        var contour = RectContour(center, trueDeg, 180, 52, jitter: 0.5);

        var recipeFixed = new RecipeConfig
        {
            Name = "Fixed",
            Template = new TemplateOptions
            {
                RefineMethod = SegmentRefineMethod.LineFit,
                LineFitSubpixel = true,
                LineFitFixAngleDuringSubpixel = true,
            },
        };
        var recipeFree = new RecipeConfig
        {
            Name = "Free",
            Template = new TemplateOptions
            {
                RefineMethod = SegmentRefineMethod.LineFit,
                LineFitSubpixel = true,
                LineFitFixAngleDuringSubpixel = false,
            },
        };

        using var roi = new Mat();
        gray.CopyTo(roi);
        var fixedHit = new LineFitSegmentRefineRuntime().Refine(new SegmentRefineRequest
        {
            RoiView = roi, Points = contour, SegmentConfidence = 0.9, Recipe = recipeFixed,
        });
        var freeHit = new LineFitSegmentRefineRuntime().Refine(new SegmentRefineRequest
        {
            RoiView = roi, Points = contour, SegmentConfidence = 0.9, Recipe = recipeFree,
        });

        Assert.True(fixedHit.Pose?.Usable == true && freeHit.Pose?.Usable == true);
        // 两种模式均应接近真值；自由角模式在亚像素阶段可微调
        Assert.True(UndirectedErr(fixedHit.Pose!.AngleDeg, trueDeg) < 0.3);
        Assert.True(UndirectedErr(freeHit.Pose!.AngleDeg, trueDeg) < 0.3);
    }

    [Fact]
    public void ConstrainTeachSize_LocksSubpixelDimensions()
    {
        const double trueDeg = 22.0;
        var center = new Point2d(200, 150);
        using var gray = Stripe(400, 300, center, trueDeg, halfShort: 26);
        var contour = RectContour(center, trueDeg, 180, 52, jitter: 0.5);

        const double teachArea = 180.0 * 52.0;
        const double teachAspect = 180.0 / 52.0;
        var (teachLong, teachShort) = InstanceGeometry.DeriveRectangleSides(teachArea, teachAspect);

        var recipe = new RecipeConfig
        {
            Name = "TeachSize",
            Template = new TemplateOptions
            {
                RefineMethod = SegmentRefineMethod.LineFit,
                LineFitSubpixel = true,
                LineFitConstrainTeachSize = true,
                TeachAreaPx = teachArea,
                TeachAspect = teachAspect,
            },
        };

        using var roi = new Mat();
        gray.CopyTo(roi);
        var hit = new LineFitSegmentRefineRuntime().Refine(new SegmentRefineRequest
        {
            RoiView = roi,
            Points = contour,
            SegmentConfidence = 0.9,
            Recipe = recipe,
        });

        Assert.True(hit.Pose?.Usable == true);
        var housing = MaskHousing.Fit(contour);
        var opts = RectFitOptions.ForLineFit(recipe.Template);
        var full = RotatedRectPipeline.Fit(contour, gray, housing.LongAxisDeg, opts);
        Assert.True(full.Ok);
        Assert.Equal(teachLong, full.LongLen, 1.5);
        Assert.Equal(teachShort, full.ShortLen, 1.0);
    }

    [Fact]
    public void MetrologyLock_FixedAngleAndSize_RefinesCenterOnly()
    {
        const double trueDeg = 33.0;
        var center = new Point2d(200, 150);
        using var gray = Stripe(400, 300, center, trueDeg, halfShort: 26);
        var contour = RectContour(center, trueDeg, 180, 52, jitter: 0.4);

        var opts = new RectFitOptions
        {
            EdgePolarity = RectEdgePolarity.Any,
            Constraints = new RectFitConstraints(
                FixedAngleDeg: trueDeg,
                FixedLongLenPx: 180,
                FixedShortLenPx: 52),
        };
        var contourFit = RotatedRectFitter.Fit(contour, trueDeg, opts);
        Assert.True(contourFit.Ok);

        var sub = RotatedRectSubpixel.Refine(
            gray, contourFit.Center, contourFit.LongLen, contourFit.ShortLen, contourFit.AngleDeg, opts);
        Assert.NotNull(sub);
        Assert.Equal(trueDeg, sub.Value.AngleDeg, 0.05);
        Assert.Equal(180, sub.Value.LongLen, 0.5);
        Assert.Equal(52, sub.Value.ShortLen, 0.5);
        Assert.True(Math.Abs(sub.Value.Center.X - center.X) < 1.5);
        Assert.True(Math.Abs(sub.Value.Center.Y - center.Y) < 1.5);
    }

    [Fact]
    public void FuzzySubpixel_RunsOnBlurredStripe()
    {
        const double trueDeg = 20.0;
        var center = new Point2d(200, 150);
        using var sharp = Stripe(400, 300, center, trueDeg, halfShort: 28);
        using var gray = new Mat();
        Cv2.GaussianBlur(sharp, gray, new Size(5, 5), 0);
        var contour = RectContour(center, trueDeg, 170, 56, jitter: 0.4);

        var recipe = new RecipeConfig
        {
            Name = "Fuzzy",
            Template = new TemplateOptions
            {
                RefineMethod = SegmentRefineMethod.LineFit,
                LineFitSubpixel = true,
                LineFitFuzzyMeasure = true,
            },
        };

        using var roi = new Mat();
        gray.CopyTo(roi);
        var hit = new LineFitSegmentRefineRuntime().Refine(new SegmentRefineRequest
        {
            RoiView = roi,
            Points = contour,
            SegmentConfidence = 0.9,
            Recipe = recipe,
        });

        Assert.True(hit.Pose?.Usable == true);
        Assert.Contains("JLVision line", hit.QualityNote ?? "", StringComparison.Ordinal);
        Assert.True(UndirectedErr(hit.Pose!.AngleDeg, trueDeg) < 0.5);
    }

    private static double UndirectedErr(double got, double truth)
    {
        var d = Math.Abs(got - truth);
        return Math.Min(d, 180 - d);
    }

    private static Mat Stripe(int w, int h, Point2d c, double trueDeg, double halfShort)
    {
        var mat = new Mat(h, w, MatType.CV_8UC1, new Scalar(20));
        var rad = trueDeg * Math.PI / 180.0;
        var nx = -Math.Sin(rad);
        var ny = Math.Cos(rad);
        const double ramp = 3.0;
        for (var y = 0; y < h; y++)
        for (var x = 0; x < w; x++)
        {
            var across = Math.Abs((x - c.X) * nx + (y - c.Y) * ny);
            var t = Math.Clamp((halfShort - across) / ramp + 0.5, 0, 1);
            mat.Set(y, x, (byte)Math.Round(20 + 180 * t));
        }
        return mat;
    }

    private static Point2f[] RectContour(Point2d center, double deg, double longLen, double shortLen, double jitter)
    {
        var rng = new Random(13);
        var hw = longLen / 2.0;
        var hh = shortLen / 2.0;
        var corners = new[]
        {
            new Point2d(center.X - hw, center.Y - hh), new Point2d(center.X + hw, center.Y - hh),
            new Point2d(center.X + hw, center.Y + hh), new Point2d(center.X - hw, center.Y + hh),
        };
        var rad = deg * Math.PI / 180.0;
        var cos = Math.Cos(rad);
        var sin = Math.Sin(rad);
        Point2d Rot(Point2d p) => new(
            center.X + (p.X - center.X) * cos - (p.Y - center.Y) * sin,
            center.Y + (p.X - center.X) * sin + (p.Y - center.Y) * cos);
        var rotated = corners.Select(Rot).ToArray();
        var pts = new List<Point2f>();
        for (var e = 0; e < 4; e++)
        {
            var a = rotated[e];
            var b = rotated[(e + 1) % 4];
            for (var s = 0; s < 40; s++)
            {
                var t = (double)s / 40;
                pts.Add(new Point2f(
                    (float)(a.X + (b.X - a.X) * t + Gaussian(rng) * jitter),
                    (float)(a.Y + (b.Y - a.Y) * t + Gaussian(rng) * jitter)));
            }
        }
        return pts.ToArray();
    }

    private static double Gaussian(Random rng)
    {
        var u1 = 1.0 - rng.NextDouble();
        var u2 = rng.NextDouble();
        return Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2);
    }
}
