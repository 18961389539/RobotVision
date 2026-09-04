using OpenCvSharp;
using RobotVision.Core.Recipe;
using RobotVision.Infrastructure.Inference.Strategies;
using RobotVision.Vision;
using RobotVision.Teach;
using Xunit;

namespace RobotVision.Tests;

public sealed class ScenePlaybookTests
{
    [Fact]
    public void TabPart_Describe_HousingWithTab()
    {
        using var img = new Mat(360, 480, MatType.CV_8UC3, new Scalar(240, 240, 240));
        Cv2.Rectangle(img, new Point(130, 152), new Point(350, 208), new Scalar(80, 80, 80), -1);
        Cv2.Rectangle(img, new Point(220, 208), new Point(260, 226), new Scalar(30, 30, 30), -1);
        using var mask = new Mat(360, 480, MatType.CV_8UC1, Scalar.All(0));
        Cv2.Rectangle(mask, new Point(130, 152), new Point(350, 208), Scalar.All(255), -1);
        Cv2.Rectangle(mask, new Point(220, 208), new Point(260, 226), Scalar.All(255), -1);
        Cv2.FindContours(mask, out var contours, out _, RetrievalModes.External, ContourApproximationModes.ApproxNone);
        var contour = contours.OrderByDescending(c => Cv2.ContourArea(c)).First()
            .Select(p => new Point2f(p.X, p.Y)).ToArray();

        var scene = ScenePlaybook.Describe(img, contour);
        Assert.True(
            scene.Kind is SceneKind.HousingWithTab or SceneKind.Silhouette or SceneKind.WeakTextureBar,
            $"凸起细长件应归壳体类，实际 {scene.Kind}：{scene.Why}");
        Assert.True(scene.Aspect >= 1.5, $"轴比 {scene.Aspect:0.00}");
    }

    [Fact]
    public void SmoothRectangle_Describe_NotPrintedTexture()
    {
        using var img = new Mat(200, 320, MatType.CV_8UC3, new Scalar(200, 200, 200));
        Cv2.Rectangle(img, new Point(40, 70), new Point(280, 130), new Scalar(190, 190, 190), -1);
        Point2f[] contour =
        [
            new(40, 70), new(280, 70), new(280, 130), new(40, 130),
        ];
        var scene = ScenePlaybook.Describe(img, contour);
        Assert.NotEqual(SceneKind.PrintedTexture, scene.Kind);
        Assert.True(
            scene.Kind is SceneKind.WeakTextureBar or SceneKind.HousingWithTab or SceneKind.Silhouette
                or SceneKind.Unknown,
            $"弱纹理矩形不应判成丝印件，实际 {scene.Kind}");
    }

    [Fact]
    public void TwoLandmarks_RecommendsDualCenterLine()
    {
        var advice = ScenePlaybook.Recommend(new TaskConstraints(HasTwoLandmarks: true));
        Assert.Equal(AngleMode.DualCenterLine, advice.Primary.AngleMode);
        Assert.Null(advice.Primary.Refine);
        Assert.Contains("连线", advice.Summary, StringComparison.Ordinal);
    }

    [Fact]
    public void BlobsWithoutModel_RecommendsDualBlob()
    {
        var advice = ScenePlaybook.Recommend(new TaskConstraints(UseBlobsWithoutModel: true));
        Assert.Equal(AngleMode.DualBlobCenterLine, advice.Primary.AngleMode);
        Assert.Contains(advice.Alternatives, a => a.AngleMode == AngleMode.DualTemplateCenterLine);
    }

    [Fact]
    public void NoTeach_DoesNotRecommendTaughtMethods()
    {
        var scene = new SceneDescriptor(SceneKind.PrintedTexture, LightingClass.BrightField,
            2.0, 0.3, 5.0, 0.12, false, 0, 1000, "纹理");
        var advice = ScenePlaybook.Recommend(new TaskConstraints(TeachAllowed: false), scene);
        Assert.NotNull(advice.Primary.Refine);
        Assert.False(TemplateOptions.NeedsTaughtImage(advice.Primary.Refine!.Value));
    }

    [Fact]
    public void NoDirected_PrefersUndirectedMode()
    {
        var scene = new SceneDescriptor(SceneKind.WeakTextureBar, LightingClass.Unknown,
            3.0, 0.2, 3.5, 0.02, false, 0, 800, "弱纹理");
        var advice = ScenePlaybook.Recommend(new TaskConstraints(NeedDirectedAngle: false), scene);
        Assert.True(
            advice.Primary.AngleMode is AngleMode.MaskMinAreaRect
                || advice.Primary.Refine == SegmentRefineMethod.LineFit,
            $"无头尾应走外接矩形或直线，实际 {advice.Primary.Title}");
    }

    [Fact]
    public void AppearanceVaries_SkipsGrayTemplate()
    {
        var scene = new SceneDescriptor(SceneKind.PrintedTexture, LightingClass.BrightField,
            1.8, 0.3, 5.2, 0.15, false, 0, 900, "纹理");
        var advice = ScenePlaybook.Recommend(new TaskConstraints(AppearanceVaries: true), scene);
        Assert.NotEqual(SegmentRefineMethod.Template, advice.Primary.Refine);
    }

    [Fact]
    public void PickWinnerForTask_DropsLineFitWhenDirectedRequired()
    {
        var candidates = new[]
        {
            new SegmentRefineCandidate(SegmentRefineMethod.LineFit, true, false, 0.95, "line"),
            new SegmentRefineCandidate(SegmentRefineMethod.CaliperTab, true, true, 0.70, "cal"),
        };
        var winner = ScenePlaybook.PickWinnerForTask(candidates, new TaskConstraints(NeedDirectedAngle: true));
        Assert.Equal(SegmentRefineMethod.CaliperTab, winner!.Method);
    }

    [Fact]
    public void HousingWithHole_RecommendsHoleLine()
    {
        var scene = new SceneDescriptor(SceneKind.HousingWithHole, LightingClass.DarkField,
            2.2, 0.4, 4.0, 0.05, true, 4, 1200, "有孔");
        var advice = ScenePlaybook.Recommend(new TaskConstraints(), scene);
        Assert.Equal(AngleMode.MaskTemplate, advice.Primary.AngleMode);
        Assert.Equal(SegmentRefineMethod.CentroidHoleLine, advice.Primary.Refine);
    }

    [Fact]
    public void UntaughtBakeOff_DoesNotOverridePrintedTextureHeuristic()
    {
        var scene = new SceneDescriptor(SceneKind.PrintedTexture, LightingClass.BrightField,
            1.8, 0.3, 5.2, 0.15, false, 0, 900, "纹理");
        IReadOnlyList<SegmentRefineCandidate> bakeoff =
        [
            new(SegmentRefineMethod.CaliperTab, true, true, 0.80, "cal"),
            new(SegmentRefineMethod.LineFit, true, false, 0.95, "line"),
            new(SegmentRefineMethod.Template, false, true, 0, "未示教", Skipped: true),
            new(SegmentRefineMethod.ShapeMatch, false, true, 0, "未示教", Skipped: true),
            new(SegmentRefineMethod.Sift, false, true, 0, "未示教", Skipped: true),
        ];
        var advice = ScenePlaybook.Recommend(new TaskConstraints(), scene, bakeoff);
        Assert.Equal(SegmentRefineMethod.Template, advice.Primary.Refine);
        Assert.Contains("尚未示教", advice.Summary, StringComparison.Ordinal);
    }

    [Fact]
    public void TaughtBakeOff_CanOverridePrintedTextureHeuristic()
    {
        var scene = new SceneDescriptor(SceneKind.PrintedTexture, LightingClass.BrightField,
            1.8, 0.3, 5.2, 0.15, false, 0, 900, "纹理");
        IReadOnlyList<SegmentRefineCandidate> bakeoff =
        [
            new(SegmentRefineMethod.CaliperTab, true, true, 0.92, "cal"),
            new(SegmentRefineMethod.Template, true, true, 0.50, "tpl"),
        ];
        var advice = ScenePlaybook.Recommend(new TaskConstraints(), scene, bakeoff);
        Assert.Equal(SegmentRefineMethod.CaliperTab, advice.Primary.Refine);
    }

    [Fact]
    public void DarkFieldNearCircular_SkipsGrayTemplate()
    {
        var scene = new SceneDescriptor(SceneKind.NearCircular, LightingClass.DarkField,
            1.1, 0.85, 4.5, 0.03, false, 0, 900, "近圆");
        var advice = ScenePlaybook.Recommend(new TaskConstraints(), scene);
        Assert.Equal(SegmentRefineMethod.ShapeMatch, advice.Primary.Refine);
        Assert.Contains("暗场", advice.Summary, StringComparison.Ordinal);
    }

    [Fact]
    public void BrightFieldTab_MentionsCaliperPolarity()
    {
        var scene = new SceneDescriptor(SceneKind.HousingWithTab, LightingClass.BrightField,
            2.4, 0.35, 4.0, 0.04, false, 12, 1100, "凸起");
        var advice = ScenePlaybook.Recommend(new TaskConstraints(), scene);
        Assert.Equal(SegmentRefineMethod.CaliperTab, advice.Primary.Refine);
        Assert.Contains("亮场", advice.Primary.Why, StringComparison.Ordinal);
    }

    [Fact]
    public void DarkField_DisqualifiesGrayTemplateInBakeOff()
    {
        var scene = new SceneDescriptor(SceneKind.Silhouette, LightingClass.DarkField,
            2.0, 0.3, 3.6, 0.04, false, 0, 800, "剪影");
        var winner = ScenePlaybook.PickWinnerForTask(
        [
            new(SegmentRefineMethod.Template, true, true, 0.95, "tpl"),
            new(SegmentRefineMethod.ShapeMatch, true, true, 0.70, "shape"),
        ], new TaskConstraints(), scene);
        Assert.Equal(SegmentRefineMethod.ShapeMatch, winner!.Method);
    }

    [Fact]
    public void FromRecipe_DualCenterLine_PrefillsLandmarks()
    {
        var recipe = new RecipeConfig { AngleMode = AngleMode.DualCenterLine };
        var task = ScenePlaybook.FromRecipe(recipe);
        Assert.True(task.HasTwoLandmarks);
        Assert.True(task.NeedDirectedAngle);
        Assert.False(task.UseBlobsWithoutModel);
    }

    [Fact]
    public void FromRecipe_KeepsExpectedCountZero()
    {
        var recipe = new RecipeConfig { AngleMode = AngleMode.MaskTemplate };
        recipe.Template.ExpectedCount = 0;
        recipe.Template.RefineMethod = SegmentRefineMethod.CaliperTab;
        var task = ScenePlaybook.FromRecipe(recipe);
        Assert.Equal(0, task.ExpectedCount);
        Assert.False(task.TeachAllowed);
    }

    [Fact]
    public void FromRecipe_MinAreaRect_UndirectedUnlessEccentric()
    {
        var recipe = new RecipeConfig { AngleMode = AngleMode.MaskMinAreaRect };
        Assert.False(ScenePlaybook.FromRecipe(recipe).NeedDirectedAngle);
        recipe.RotationCompensation = RotationCompensationMode.EccentricTool;
        Assert.True(ScenePlaybook.FromRecipe(recipe).NeedDirectedAngle);
    }

    [Fact]
    public void FromRecipe_TaughtTemplate_AllowsTeach()
    {
        var recipe = new RecipeConfig { AngleMode = AngleMode.MaskTemplate };
        recipe.Template.RefineMethod = SegmentRefineMethod.Template;
        recipe.Template.TemplateImageBase64 = "x";
        Assert.True(ScenePlaybook.FromRecipe(recipe).TeachAllowed);
        Assert.True(ScenePlaybook.FromRecipe(recipe).NeedDirectedAngle);
    }

    [Fact]
    public void ScoreKinds_HoleDoesNotVetoPrintedTexture()
    {
        var votes = ScenePlaybook.ScoreKinds(
            holeOk: true, protrusion: 0, shortLen: 40, separability: 0.16, aspect: 1.8,
            entropy: 5.5, circularity: 0.4, relativeEntropy: 1.2, holeQuality: 0.85);
        Assert.Contains(votes, v => v.Kind == SceneKind.HousingWithHole && v.Score >= 0.4);
        Assert.Contains(votes, v => v.Kind == SceneKind.PrintedTexture && v.Score >= 0.45);
        var (kind, _, rival) = (votes[0].Kind, votes[0].Score, votes.Count > 1 ? votes[1].Kind : (SceneKind?)null);
        Assert.True(
            kind is SceneKind.PrintedTexture or SceneKind.HousingWithHole,
            $"有孔且强纹理不应被孔独占成其它类，实际 {kind}");
        if (kind == SceneKind.HousingWithHole)
            Assert.Equal(SceneKind.PrintedTexture, rival);
    }

    [Fact]
    public void Recommend_ExposesConfidenceAndUncertainWhenUntaught()
    {
        var scene = new SceneDescriptor(SceneKind.PrintedTexture, LightingClass.BrightField,
            1.8, 0.3, 5.2, 0.15, false, 0, 900, "纹理")
        {
            KindConfidence = 0.6,
        };
        IReadOnlyList<SegmentRefineCandidate> bakeoff =
        [
            new(SegmentRefineMethod.CaliperTab, true, true, 0.80, "cal"),
            new(SegmentRefineMethod.Template, false, true, 0, "未示教", Skipped: true),
        ];
        var advice = ScenePlaybook.Recommend(new TaskConstraints(), scene, bakeoff);
        Assert.True(advice.Confidence < 0.75, $"未示教置信应偏低，实际 {advice.Confidence:0.00}");
        Assert.Contains("推荐置信", advice.ConfidenceNote, StringComparison.Ordinal);
        Assert.Contains("尚未示教", advice.ConfidenceNote, StringComparison.Ordinal);
    }

    [Fact]
    public void FromHealth_DownranksCurrentMethod()
    {
        var prior = ScenePlaybook.FromHealth(true, false, false, SegmentRefineMethod.Template);
        Assert.NotNull(prior);
        Assert.Equal(SegmentRefineMethod.Template, prior!.Downrank);
        Assert.Contains("1019", prior.Reason, StringComparison.Ordinal);

        var winner = ScenePlaybook.PickWinnerForTask(
        [
            new(SegmentRefineMethod.Template, true, true, 0.90, "tpl"),
            new(SegmentRefineMethod.CaliperTab, true, true, 0.70, "cal"),
        ], new TaskConstraints(), prior: prior);
        Assert.Equal(SegmentRefineMethod.CaliperTab, winner!.Method);
    }

    [Fact]
    public void PolicyOrder_CanPreferSift()
    {
        var prior = new RecipePrior([SegmentRefineMethod.Sift, SegmentRefineMethod.Template]);
        var winner = ScenePlaybook.PickWinnerForTask(
        [
            new(SegmentRefineMethod.Sift, true, true, 0.88, "sift"),
            new(SegmentRefineMethod.Template, true, true, 0.90, "tpl"),
        ], new TaskConstraints(), prior: prior);
        Assert.Equal(SegmentRefineMethod.Sift, winner!.Method);
    }

    [Fact]
    public void SceneVotes_InconsistentKinds_LowerConfidence()
    {
        var scene = new SceneDescriptor(SceneKind.HousingWithTab, LightingClass.BrightField,
            2.4, 0.35, 4.0, 0.04, false, 12, 1100, "凸起")
        {
            KindConfidence = 0.8,
        };
        var mixed = new Dictionary<SceneKind, int>
        {
            [SceneKind.HousingWithTab] = 1,
            [SceneKind.PrintedTexture] = 2,
        };
        var even = ScenePlaybook.Recommend(new TaskConstraints(), scene);
        var split = ScenePlaybook.Recommend(new TaskConstraints(), scene, sceneVotes: mixed);
        Assert.True(split.Confidence < even.Confidence,
            $"分帧不一致应更低：{split.Confidence:0.00} vs {even.Confidence:0.00}");
        Assert.Contains("不一致", split.ConfidenceNote, StringComparison.Ordinal);
    }

    [Fact]
    public void RelativeEntropy_TexturedPartExceedsUniformBackground()
    {
        using var img = new Mat(200, 320, MatType.CV_8UC3, new Scalar(200, 200, 200));
        for (var i = 0; i < 30; i++)
        {
            var x0 = 40 + i * 8;
            var v = i % 2 == 0 ? 0 : 255;
            Cv2.Rectangle(img, new Point(x0, 70), new Point(x0 + 8, 130), new Scalar(v, v, v), -1);
        }

        Point2f[] contour =
        [
            new(40, 70), new(280, 70), new(280, 130), new(40, 130),
        ];
        var scene = ScenePlaybook.Describe(img, contour);
        Assert.True(scene.RelativeEntropy > 0.4,
            $"条纹件相对熵应高于均匀背景，实际 {scene.RelativeEntropy:0.00} 件内熵 {scene.TextureEntropy:0.00}");
    }
}
