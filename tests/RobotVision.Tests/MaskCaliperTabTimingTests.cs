using System.Diagnostics;
using OpenCvSharp;
using RobotVision.Core.Recipe;
using RobotVision.Infrastructure.Inference.Strategies;
using RobotVision.Vision;
using Xunit;
using Xunit.Abstractions;

namespace RobotVision.Tests;

/// <summary>
/// 精修耗时对比（无相机）：OSDP 真实模板尺寸 + 与现场同量级的转正窗 / 壳体。
/// 不含取图与分割。
/// </summary>
[Trait("Category", "Bench")]
public sealed class MaskCaliperTabTimingTests(ITestOutputHelper output)
{
    [SkippableFact]
    public void RefineOnly_CaliperFasterThanTemplate_OsdpScale()
    {
        var recipeDir = TestBuildPaths.ResolveRecipesDir()
                        ?? TestBuildPaths.CombineWpf("recipes");
        var path = Path.Combine(recipeDir, "OSDP.json");
        TestPreconditions.RequireFile(path, $"Missing {path}");

        var recipe = new RecipeLoader(recipeDir).Get("OSDP");
        using var template = MaskTemplateMatcher.DecodeTemplatePng(recipe.Template.TemplateImageBase64);
        using var bank = MaskTemplateMatcher.CreateRotationBank(template, recipe.Template.RefineRangeDeg);

        const int roiW = 2105, roiH = 1573;
        const int bodyW = 882, bodyH = 430;
        using var roi = new Mat(roiH, roiW, MatType.CV_8UC3, new Scalar(240, 240, 240));
        var cx = roiW / 2.0;
        var cy = roiH / 2.0;
        Cv2.Rectangle(roi,
            new Point((int)(cx - bodyW / 2.0), (int)(cy - bodyH / 2.0)),
            new Point((int)(cx + bodyW / 2.0), (int)(cy + bodyH / 2.0)),
            new Scalar(70, 70, 70), -1);
        Cv2.Rectangle(roi,
            new Point((int)(cx - 40), (int)(cy + bodyH / 2.0)),
            new Point((int)(cx + 40), (int)(cy + bodyH / 2.0 + 28)),
            new Scalar(25, 25, 25), -1);
        var tx = (int)(cx - template.Width / 2.0);
        var ty = (int)(cy + bodyH / 2.0 - template.Height + 8);
        if (tx >= 0 && ty >= 0 && tx + template.Width <= roiW && ty + template.Height <= roiH)
            template.CopyTo(roi[new Rect(tx, ty, template.Width, template.Height)]);

        var contour = BodyContour(cx, cy, bodyW, bodyH);

        var crop = MaskTemplateMatcher.UprightCrop(roi, contour, 0.15);
        try
        {
            output.WriteLine(
                $"模板 {template.Width}x{template.Height}  转正窗 {crop.Upright.Width}x{crop.Upright.Height}  " +
                $"ROI {roiW}x{roiH}  旋转库 {bank.Count} 张");

            _ = MaskTemplateMatcher.MatchBest(crop.Upright, template, recipe.Template.RefineRangeDeg,
                recipe.Template.MatchThreshold, bank);
            _ = MaskCaliperTab.Refine(roi, contour);
        }
        finally
        {
            crop.Upright.Dispose();
        }

        const int n = 20;
        var tplMs = TimeMs(n, () =>
        {
            var c = MaskTemplateMatcher.UprightCrop(roi, contour, 0.15);
            try
            {
                _ = MaskTemplateMatcher.MatchBest(c.Upright, template, recipe.Template.RefineRangeDeg,
                    recipe.Template.MatchThreshold, bank);
            }
            finally
            {
                c.Upright.Dispose();
            }
        });
        var calMs = TimeMs(n, () => MaskCaliperTab.Refine(roi, contour));

        output.WriteLine($"模板精修（转正+NCC×{n}）均值 {tplMs:0.0} ms");
        output.WriteLine($"卡尺精修（×{n}）均值 {calMs:0.0} ms");
        output.WriteLine($"比 模板/卡尺 = {tplMs / Math.Max(0.01, calMs):0.0}x  卡尺少 {tplMs - calMs:0.0} ms");
        Assert.True(calMs < tplMs, $"卡尺应更快：卡尺 {calMs:0.0}ms 模板 {tplMs:0.0}ms");
    }

    private static double TimeMs(int n, Action work)
    {
        var sw = Stopwatch.StartNew();
        for (var i = 0; i < n; i++)
            work();
        sw.Stop();
        return sw.Elapsed.TotalMilliseconds / n;
    }

    private static Point2f[] BodyContour(double cx, double cy, int w, int h)
    {
        var hw = w / 2f;
        var hh = h / 2f;
        return
        [
            new((float)(cx - hw), (float)(cy - hh)),
            new((float)(cx + hw), (float)(cy - hh)),
            new((float)(cx + hw), (float)(cy + hh)),
            new((float)(cx - hw), (float)(cy + hh)),
        ];
    }
}
