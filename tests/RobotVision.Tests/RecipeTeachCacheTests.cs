using OpenCvSharp;
using RobotVision.Core.Recipe;
using RobotVision.Infrastructure.Inference.Strategies;
using Xunit;

namespace RobotVision.Tests;

/// <summary>示教缓存租约：旋转 / SIFT / 形状三条路径共用 <see cref="RecipeTeachCache{TValue}"/>。</summary>
public sealed class RecipeTeachCacheTests : IDisposable
{
    private readonly Mat _shapeTemplate;
    private readonly Mat _siftTemplate;

    public RecipeTeachCacheTests()
    {
        _shapeTemplate = PaintShapeTemplate();
        _siftTemplate = PaintSiftTemplate();
    }

    public void Dispose()
    {
        _shapeTemplate.Dispose();
        _siftTemplate.Dispose();
    }

    [Fact]
    public void SiftCache_Warm_ThenGetOrCreate_ReusesModel()
    {
        var recipe = SiftRecipe("SiftReuse");

        MaskSiftRefine.Warm(recipe);
        var a = MaskSiftRefine.GetOrCreate(recipe);
        var b = MaskSiftRefine.GetOrCreate(recipe);
        Assert.NotNull(a);
        Assert.Same(a, b);
        Assert.True(a.KeypointCount >= 16);

        MaskSiftRefine.Release(a);
        MaskSiftRefine.Release(b);
        MaskSiftRefine.Remove(recipe.Name);
    }

    [Fact]
    public void SiftCache_Remove_WhileLeased_DoesNotDisposeUntilRelease()
    {
        var recipe = SiftRecipe("SiftLease");

        var teach = MaskSiftRefine.GetOrCreate(recipe);
        Assert.NotNull(teach);
        MaskSiftRefine.Remove(recipe.Name);
        Assert.False(teach.Descriptors.IsDisposed);

        MaskSiftRefine.Release(teach);
        Assert.True(teach.Descriptors.IsDisposed);
    }

    [Fact]
    public void ShapeCache_Warm_ThenGetOrCreate_ReusesModel()
    {
        var recipe = ShapeRecipe("ShapeReuse");

        MaskShapeMatch.Warm(recipe);
        var a = MaskShapeMatch.GetOrCreate(recipe);
        var b = MaskShapeMatch.GetOrCreate(recipe);
        Assert.NotNull(a);
        Assert.Same(a, b);
        Assert.True(a.PointCount >= 24);

        MaskShapeMatch.Release(a);
        MaskShapeMatch.Release(b);
        MaskShapeMatch.Remove(recipe.Name);
    }

    [Fact]
    public void ShapeCache_Remove_WhileLeased_AllowsReleaseWithoutThrow()
    {
        var recipe = ShapeRecipe("ShapeLease");

        var model = MaskShapeMatch.GetOrCreate(recipe);
        Assert.NotNull(model);
        MaskShapeMatch.Remove(recipe.Name);
        MaskShapeMatch.Release(model);
    }

    private RecipeConfig ShapeRecipe(string name) => new()
    {
        Name = name,
        CameraId = "cam",
        AngleMode = AngleMode.MaskTemplate,
        Models = ["m.onnx"],
        Template = new TemplateOptions
        {
            RefineMethod = SegmentRefineMethod.ShapeMatch,
            TemplateImageBase64 = MaskTemplateMatcher.EncodeTemplatePng(_shapeTemplate),
        },
    };

    private RecipeConfig SiftRecipe(string name)
    {
        var recipe = ShapeRecipe(name);
        recipe.Template.RefineMethod = SegmentRefineMethod.Sift;
        recipe.Template.TemplateImageBase64 = MaskTemplateMatcher.EncodeTemplatePng(_siftTemplate);
        return recipe;
    }

    private static Mat PaintShapeTemplate()
    {
        var m = new Mat(64, 160, MatType.CV_8UC3, Scalar.All(40));
        Cv2.Rectangle(m, new Rect(20, 10, 100, 44), new Scalar(210, 90, 30), -1);
        Cv2.Rectangle(m, new Rect(120, 18, 18, 28), new Scalar(20, 220, 60), -1);
        return m;
    }

    private static Mat PaintSiftTemplate()
    {
        const int w = 480;
        const int h = 360;
        var img = new Mat(h, w, MatType.CV_8UC1, new Scalar(24));
        Cv2.FillConvexPoly(img, RectCorners(w / 2.0, h / 2.0, 110, 28, 0), new Scalar(210));
        Cv2.FillConvexPoly(img, RectCorners(w / 2.0 - 70, h / 2.0 - 18, 18, 12, 0), new Scalar(24));
        Cv2.Circle(img, new Point(w / 2 + 78, h / 2 + 18), 11, new Scalar(210), -1);
        Cv2.Circle(img, new Point(w / 2 + 40, h / 2 - 8), 6, new Scalar(40), -1);
        Cv2.GaussianBlur(img, img, new Size(3, 3), 0.4);
        return img;
    }

    private static Point[] RectCorners(double cx, double cy, double hw, double hh, double deg)
    {
        var a = RotatePoint(cx - hw, cy - hh, deg);
        var b = RotatePoint(cx + hw, cy - hh, deg);
        var c = RotatePoint(cx + hw, cy + hh, deg);
        var d = RotatePoint(cx - hw, cy + hh, deg);
        return [a, b, c, d];
    }

    private static Point RotatePoint(double x, double y, double deg)
    {
        const double ox = 240;
        const double oy = 180;
        var rad = deg * Math.PI / 180.0;
        var dx = x - ox;
        var dy = y - oy;
        return new Point(
            (int)Math.Round(ox + dx * Math.Cos(rad) - dy * Math.Sin(rad)),
            (int)Math.Round(oy + dx * Math.Sin(rad) + dy * Math.Cos(rad)));
    }
}
