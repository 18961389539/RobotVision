using System.Text.Json;
using OpenCvSharp;
using RobotVision.Core.Abstractions;
using RobotVision.Core.Models;
using RobotVision.Core.Recipe;
using RobotVision.Infrastructure;
using RobotVision.Infrastructure.Cameras;
using RobotVision.Infrastructure.Inference;
using RobotVision.Infrastructure.Inference.Strategies;
using Xunit;
using Xunit.Abstractions;

namespace RobotVision.Tests;

/// <summary>
/// 配方 Product + 真实相机 up：核对示教写入的智能项（卡尺/极性/几何/件数）
/// 与当前画面四路赛马是否一致。需 RV_HARDWARE_TEST=1，且不要占用相机的 WPF。
/// </summary>
[Trait("Category", "Hardware")]
public sealed class ProductLiveAdvisorTests(ITestOutputHelper output)
{
    private static bool Enabled =>
        string.Equals(Environment.GetEnvironmentVariable("RV_HARDWARE_TEST"), "1",
            StringComparison.OrdinalIgnoreCase);

    private static string RepoRoot => Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory, "..", "..", "..", "..", ".."));

    [Fact]
    public void Product_Live_Caliper_And_BakeOff_Agree()
    {
        if (!Enabled)
            return;

        var wpfBin = Path.Combine(RepoRoot, "src", "RobotVision.Wpf", "bin", "Debug", "net8.0-windows");
        var recipeDir = Path.Combine(wpfBin, "recipes");
        var modelsDir = Path.Combine(wpfBin, "models");
        Assert.True(File.Exists(Path.Combine(recipeDir, "Product.json")), $"缺少 {recipeDir}\\Product.json");
        Assert.True(File.Exists(Path.Combine(modelsDir, "OSFP-SEG.onnx")), "缺少 OSFP-SEG.onnx");

        var recipe = new RecipeLoader(recipeDir).Get("Product").Clone();
        Assert.Equal(AngleMode.MaskTemplate, recipe.AngleMode);
        Assert.Equal("up", recipe.CameraId);

        var camCfg = ReadCamera(Path.Combine(wpfBin, "appsettings.json"), "up");
        var sn = string.IsNullOrWhiteSpace(camCfg.DeviceId) ? "24654744" : camCfg.DeviceId.Trim();
        var devices = BaslerCamera.EnumerateDevices();
        output.WriteLine("相机: " + string.Join("; ", devices));
        var serial = devices.Select(d => d.Split('|')[0].Trim())
            .FirstOrDefault(s => s.Equals(sn, StringComparison.OrdinalIgnoreCase));
        Assert.True(serial is not null, $"未找到相机 {sn}");

        output.WriteLine(
            $"配方 refine={recipe.Template.RefineMethod} coarse={recipe.Template.AllowCoarseFallback} " +
            $"expected={recipe.Template.ExpectedCount} peak={recipe.Template.TeachPeakScore:0.000} " +
            $"th={recipe.Template.MatchThreshold:0.00} pix={recipe.Segmentation.PixelConfidence:0.00} " +
            $"edge={recipe.Template.HousingEdgePolarity} tab={recipe.Template.TabPolarity} " +
            $"area={recipe.Template.TeachAreaPx:0} aspect={recipe.Template.TeachAspect:0.00}");

        using var models = new ModelManager(modelsDir);
        var strategy = new MaskTemplateStrategy(models);
        using var template = string.IsNullOrEmpty(recipe.Template.TemplateImageBase64)
            ? null
            : MaskTemplateMatcher.DecodeTemplatePng(recipe.Template.TemplateImageBase64);

        using var cam = new BaslerCamera("product_live", serial,
            exposureTimeUs: camCfg.ExposureUs, gain: camCfg.Gain, grabTimeoutMs: 15000);
        using (cam.Grab()) { }
        using (cam.Grab()) { }

        const int n = 8;
        var angles = new List<double>(n);
        var xs = new List<double>(n);
        var ys = new List<double>(n);
        var scores = new List<double>(n);
        var usableN = 0;
        var missN = 0;
        var code1019 = 0;
        SegmentRefineAdvice? advice = null;
        var snapDir = Path.Combine(Path.GetTempPath(), "RobotVision-product-live");
        Directory.CreateDirectory(snapDir);

        for (var i = 0; i < n; i++)
        {
            using var frame = cam.Grab();
            using var mat = VisionImageCv.AsMat(frame.Image);
            if (i == 0)
            {
                Cv2.ImWrite(Path.Combine(snapDir, "product-0.png"), mat);
                advice = Advise(models, recipe, frame.Image, template);
                output.WriteLine($"赛马推荐 {SegmentRefineAdvisor.MethodLabel(advice.Recommended)}  " +
                                 $"可向={advice.CanResolveOrientation} 轴比={advice.Aspect:0.00} " +
                                 $"分差={advice.Separability:0.00} 凸起px={advice.ProtrusionPx:0.0}");
                output.WriteLine(advice.Summary);
                foreach (var c in advice.Candidates)
                    output.WriteLine($"  {c.Method,-18} ok={c.Ok} dir={c.Directed} score={c.Score:0.00}  {c.Note}");
            }

            var poses = strategy.Compute(frame.Image, recipe);
            var reject = PixelPoseOutput.RejectReason(poses);
            if (poses.Count == 0)
            {
                missN++;
                Cv2.ImWrite(Path.Combine(snapDir, $"product-miss-{i}.png"), mat);
                output.WriteLine($"#{i + 1:00} 1007 未检出");
                continue;
            }

            if (reject == VisionErrorCode.RefineFailed)
            {
                code1019++;
                var d = MaskCaliperTab.LastDebug;
                output.WriteLine($"#{i + 1:00} 1019 精修未过门  n={poses.Count} usable=0  " +
                                 $"par={d.ParallelDeg:0.00} probes={d.ValidProbes} tab={d.TabSign}");
                continue;
            }

            var p = poses.First(x => x.Usable);
            usableN++;
            angles.Add(p.AngleDeg);
            xs.Add(p.Cx);
            ys.Add(p.Cy);
            scores.Add(p.Score);
            var dbg = MaskCaliperTab.LastDebug;
            output.WriteLine(
                $"#{i + 1:00} OK  {p.Cx:0.02},{p.Cy:0.02}  {p.AngleDeg:0.000}°  q={p.Score:0.000}  " +
                $"seg={p.SegmentScore:0.000}  probes={dbg.ValidProbes} par={dbg.ParallelDeg:0.00}°  " +
                $"tab={dbg.TabSign} diff={dbg.TabGrayDiff:0.0}  n={poses.Count} usable={poses.Count(x => x.Usable)}");
        }

        output.WriteLine($"汇总  OK={usableN}  1019={code1019}  1007={missN} / {n}");
        Assert.True(usableN >= 6, $"合格帧过少 OK={usableN} 1019={code1019} 1007={missN}，图在 {snapDir}");
        Assert.Equal(0, missN);

        var flips = 0;
        for (var i = 1; i < angles.Count; i++)
        {
            if (Math.Abs(Wrap180(angles[i] - angles[i - 1])) > 90)
                flips++;
        }

        var meanA = angles.Average();
        var span = angles.Max(a => Math.Abs(Wrap180(a - meanA)));
        output.WriteLine(
            $"flips={flips}  angleSpan={span:0.000}°  Xspan={xs.Max() - xs.Min():0.02}px  " +
            $"Yspan={ys.Max() - ys.Min():0.02}px  q={scores.Average():0.000}");

        Assert.Equal(0, flips);
        Assert.True(span < 3.0, $"角度极差 {span:0.00}°");
        Assert.NotNull(advice);
        Assert.Equal(SegmentRefineMethod.CaliperTab, recipe.Template.RefineMethod);
        Assert.False(recipe.Template.AllowCoarseFallback);
        Assert.Equal(1, recipe.Template.ExpectedCount);
        Assert.True(recipe.Segmentation.PixelConfidence <= 0.56);
        Assert.Equal(HousingEdgePolarity.DarkToBright, recipe.Template.HousingEdgePolarity);
        Assert.Equal(TabPolarityLock.MinusShortAxis, recipe.Template.TabPolarity);

        var rec = advice!.Recommended;
        output.WriteLine($"配方已用卡尺+凸起；本帧赛马胜出 {SegmentRefineAdvisor.MethodLabel(rec)}");
        Assert.True(
            rec is SegmentRefineMethod.CaliperTab or SegmentRefineMethod.CentroidHoleLine,
            $"本帧赛马给出无向方法 {rec}，与已示教极性冲突，请检查画面是否仍是同一只零件");
    }

    private static SegmentRefineAdvice Advise(
        ModelManager models, RecipeConfig recipe, VisionImage image, Mat? template)
    {
        using var roiImage = RoiHelper.CropToVisionImage(image, recipe.Roi, out var ox, out var oy);
        var input = roiImage ?? image;
        using var roiMat = VisionImageCv.AsMat(input);
        var session = models.Open(recipe.Models[0], InferenceTask.Segmentation);
        var segs = session.Run(y => y.RunSegmentation(
            input, recipe.Confidence, recipe.Segmentation.PixelConfidence, recipe.Iou));
        var seg = segs.Where(s => (double)s.Box.Width * s.Box.Height >= 400 && s.ContourLocal.Count >= 4)
            .OrderByDescending(s => s.Confidence)
            .FirstOrDefault();
        if (seg is null)
            throw new InvalidOperationException("首帧分割未检出，无法赛马");

        var box = seg.Box;
        var points = new Point2f[seg.ContourLocal.Count];
        for (var i = 0; i < seg.ContourLocal.Count; i++)
            points[i] = new Point2f((float)(seg.ContourLocal[i].X + box.X), (float)(seg.ContourLocal[i].Y + box.Y));

        return SegmentRefineAdvisor.Analyze(
            roiMat, points, upright: null, seg.BitPackedMask, box.Width, box.Height,
            recipe.Template, template, image.Width, image.Height, ox, oy,
            instanceConfidence: seg.Confidence,
            boxConfidence: recipe.Confidence,
            pixelConfidence: recipe.Segmentation.PixelConfidence);
    }

    private static (string DeviceId, double? ExposureUs, double? Gain) ReadCamera(string path, string id)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        foreach (var cam in doc.RootElement.GetProperty("Cameras").EnumerateArray())
        {
            if (cam.GetProperty("Id").GetString() != id)
                continue;
            var deviceId = cam.TryGetProperty("DeviceId", out var d) ? d.GetString() ?? "" : "";
            double? exposure = cam.TryGetProperty("ExposureTimeUs", out var e) && e.ValueKind == JsonValueKind.Number
                ? e.GetDouble() : null;
            double? gain = cam.TryGetProperty("Gain", out var g) && g.ValueKind == JsonValueKind.Number
                ? g.GetDouble() : null;
            return (deviceId, exposure, gain);
        }

        return ("", null, null);
    }

    private static double Wrap180(double deg)
    {
        var d = ((deg + 180.0) % 360.0 + 360.0) % 360.0 - 180.0;
        return d == -180.0 ? 180.0 : d;
    }
}
