using System.Globalization;
using System.Diagnostics;
using System.Text.Json;
using OpenCvSharp;
using RobotVision.Core.Abstractions;
using RobotVision.Core.Models;
using RobotVision.Core.Recipe;
using RobotVision.Hosting;
using RobotVision.Infrastructure;
using RobotVision.Infrastructure.Cameras;
using RobotVision.Infrastructure.Inference;
using RobotVision.Infrastructure.Inference.Strategies;
using Xunit;
using Xunit.Abstractions;

namespace RobotVision.Tests;

/// <summary>
/// OSDP 真机复测：连续 Grab + 分割+模板配准，头尾不得 180° 翻转。
/// 设置 RV_HARDWARE_TEST=1，且不要同时开着占用相机的 WPF。
/// 相机必须是配方 cam_basler 的 DeviceId（现场 #1 = 24654744），禁止取枚举第一台。
/// </summary>
[Trait("Category", "Hardware")]
public sealed class MaskTemplateLiveOsdpTests(ITestOutputHelper output)
{
    private static string RepoRoot => TestBuildPaths.FindRepoRoot()
                                      ?? throw new InvalidOperationException("Repo root not found.");

    [Fact]
    public void OsdpLive_AngleDoesNotFlip180()
    {
        TestPreconditions.RequireHardware();
        RunOsdpLive(SegmentRefineMethod.Template, "template");
    }

    [Fact]
    public void OsdpLive_CaliperTab_DoesNotFlip180()
    {
        TestPreconditions.RequireHardware();
        RunOsdpLive(SegmentRefineMethod.CaliperTab, "caliper");
    }

    [Fact]
    public void OsdpLive_CaliperVsTemplate_RefineTime()
    {
        TestPreconditions.RequireHardware();

        var recipeDir = OsdpRecipesDir();
        var modelsDir = TestBuildPaths.ResolveModelsDir()
                        ?? throw new InvalidOperationException("models directory not found.");
        TestPreconditions.RequireFile(Path.Combine(modelsDir, "OSFP-SEG.onnx"), "Missing OSFP-SEG.onnx");
        var loader = new RecipeLoader(recipeDir);
        var tplRecipe = loader.Get("OSDP").Clone();
        tplRecipe.Template.RefineMethod = SegmentRefineMethod.Template;
        var calRecipe = tplRecipe.Clone();
        calRecipe.Template.RefineMethod = SegmentRefineMethod.CaliperTab;

        using var models = new ModelManager(modelsDir);
        var tpl = new MaskTemplateStrategy(models);
        var cal = new MaskTemplateStrategy(models);

        var wpfAppsettings = TestBuildPaths.CombineWpf("appsettings.json");
        var camCfg = ReadCamBasler(wpfAppsettings);
        var wantedSn = Environment.GetEnvironmentVariable("RV_OSDP_CAMERA_SN")?.Trim();
        if (string.IsNullOrWhiteSpace(wantedSn))
            wantedSn = string.IsNullOrWhiteSpace(camCfg.DeviceId) ? "24654744" : camCfg.DeviceId.Trim();
        var serial = BaslerCamera.EnumerateDevices()
            .Select(d => d.Split('|')[0].Trim())
            .FirstOrDefault(s => s.Equals(wantedSn, StringComparison.OrdinalIgnoreCase));
        Assert.True(serial is not null, $"未找到相机 {wantedSn}");

        var tplRefine = new List<double>(8);
        var calRefine = new List<double>(8);
        var tplAll = new List<double>(8);
        var calAll = new List<double>(8);
        var grabMs = new List<double>(8);

        using var cam = new BaslerCamera("osdp_time", serial,
            exposureTimeUs: camCfg.ExposureUs, gain: camCfg.Gain, grabTimeoutMs: 15000);
        using (cam.Grab()) { }
        using (cam.Grab()) { }

        static (double Seg, double Refine, double Total) TimeCompute(
            MaskTemplateStrategy strategy, VisionImage image, RecipeConfig recipe)
        {
            InferenceStageClock.Reset();
            var sw = Stopwatch.StartNew();
            var poses = strategy.Compute(image, recipe);
            sw.Stop();
            Assert.True(poses.Count >= 1, "未检出");
            var snap = InferenceStageClock.Snapshot();
            return (snap.SegmentMs, snap.RefineMs, sw.Elapsed.TotalMilliseconds);
        }

        // 预热：分割会话 + 模板旋转库
        using (var warm = cam.Grab())
        {
            TimeCompute(tpl, warm.Image, tplRecipe);
            TimeCompute(cal, warm.Image, calRecipe);
        }

        const int n = 8;
        for (var i = 0; i < n; i++)
        {
            var gsw = Stopwatch.StartNew();
            using var frame = cam.Grab();
            gsw.Stop();
            grabMs.Add(gsw.Elapsed.TotalMilliseconds);

            var firstTpl = i % 2 == 0;
            var a = firstTpl
                ? TimeCompute(tpl, frame.Image, tplRecipe)
                : TimeCompute(cal, frame.Image, calRecipe);
            var b = firstTpl
                ? TimeCompute(cal, frame.Image, calRecipe)
                : TimeCompute(tpl, frame.Image, tplRecipe);
            if (firstTpl)
            {
                tplRefine.Add(a.Refine); tplAll.Add(a.Total);
                calRefine.Add(b.Refine); calAll.Add(b.Total);
            }
            else
            {
                calRefine.Add(a.Refine); calAll.Add(a.Total);
                tplRefine.Add(b.Refine); tplAll.Add(b.Total);
            }

            output.WriteLine(
                $"#{i + 1:00} grab={grabMs[^1]:0}  " +
                $"模板 精修={tplRefine[^1]:0.0} 合计={tplAll[^1]:0.0}  " +
                $"卡尺 精修={calRefine[^1]:0.0} 合计={calAll[^1]:0.0}  " +
                $"分割≈{a.Seg:0.0}ms");
        }

        static string Stat(List<double> xs) =>
            $"均值{xs.Average():0.0}  中位{xs.OrderBy(v => v).ElementAt(xs.Count / 2):0.0}  " +
            $"min{xs.Min():0.0}  max{xs.Max():0.0}";

        output.WriteLine($"取图     {Stat(grabMs)} ms");
        output.WriteLine($"模板精修 {Stat(tplRefine)} ms");
        output.WriteLine($"卡尺精修 {Stat(calRefine)} ms");
        output.WriteLine($"模板合计 {Stat(tplAll)} ms（分割+精修）");
        output.WriteLine($"卡尺合计 {Stat(calAll)} ms（分割+精修）");
        output.WriteLine(
            $"精修比 模板/卡尺 = {tplRefine.Average() / Math.Max(0.01, calRefine.Average()):0.0}x  " +
            $"卡尺少 {tplRefine.Average() - calRefine.Average():0.0} ms");
    }

    private void RunOsdpLive(SegmentRefineMethod method, string tag)
    {
        var recipeDir = OsdpRecipesDir();
        var modelsDir = TestBuildPaths.ResolveModelsDir()
                        ?? throw new InvalidOperationException("models directory not found.");
        Assert.True(File.Exists(Path.Combine(recipeDir, "OSDP.json")), $"缺少配方: {recipeDir}\\OSDP.json");
        TestPreconditions.RequireFile(Path.Combine(modelsDir, "OSFP-SEG.onnx"), "缺少 OSFP-SEG.onnx");

        var loader = new RecipeLoader(recipeDir);
        var recipe = loader.Get("OSDP").Clone();
        recipe.Template.RefineMethod = method;
        using var models = new ModelManager(modelsDir);
        var strategy = new MaskTemplateStrategy(models);

        var wpfAppsettings = TestBuildPaths.CombineWpf("appsettings.json");
        var camCfg = ReadCamBasler(wpfAppsettings);
        var wantedSn = Environment.GetEnvironmentVariable("RV_OSDP_CAMERA_SN")?.Trim();
        if (string.IsNullOrWhiteSpace(wantedSn))
            wantedSn = string.IsNullOrWhiteSpace(camCfg.DeviceId) ? "24654744" : camCfg.DeviceId.Trim();

        IReadOnlyList<string> devices = [];
        try
        {
            devices = BaslerCamera.EnumerateDevices();
        }
        catch (Exception ex)
        {
            Assert.Fail($"pylon 枚举失败: {ex.Message}");
        }
        output.WriteLine($"[{tag}] 可用相机: {string.Join("; ", devices)}");
        var serial = devices
            .Select(d => d.Split('|')[0].Trim())
            .FirstOrDefault(s => s.Equals(wantedSn, StringComparison.OrdinalIgnoreCase));
        Assert.True(serial is not null,
            $"未找到配方相机 {wantedSn}。可用: {string.Join("; ", devices)}");
        output.WriteLine($"[{tag}] 相机 {serial}  refine={method}  曝光配置={camCfg.ExposureUs}µs");

        const int grabs = 12;
        var angles = new List<double>(grabs);
        var xs = new List<double>(grabs);
        var ys = new List<double>(grabs);
        var snapDir = Path.Combine(Path.GetTempPath(), "RobotVision-camera-test");
        Directory.CreateDirectory(snapDir);

        using var cam = new BaslerCamera("osdp_live", serial,
            exposureTimeUs: camCfg.ExposureUs, gain: camCfg.Gain, grabTimeoutMs: 15000);
        using (cam.Grab()) { }
        using (cam.Grab()) { }

        if (cam is IExposureControl exp)
            output.WriteLine($"[{tag}] 曝光 {exp.GetExposureTimeUs():0}µs  增益 {exp.GetGain():0.00}");

        for (var i = 0; i < grabs; i++)
        {
            using var frame = cam.Grab();
            using var mat = VisionImageCv.AsMat(frame.Image);
            Cv2.MeanStdDev(mat, out var mean, out var stddev);
            if (i == 0)
            {
                var snap = Path.Combine(snapDir, $"osdp-live-{tag}-0.png");
                Cv2.ImWrite(snap, mat);
                output.WriteLine($"[{tag}] 首帧 {frame.Image.Width}x{frame.Image.Height} mean={mean.Val0:0.1} 已存 {snap}");
                DiagnoseSegmentation(models, recipe, frame.Image);
            }

            List<PixelPose> poses;
            try
            {
                poses = strategy.Compute(frame.Image, recipe);
            }
            catch (Exception ex)
            {
                output.WriteLine($"[{tag}] Compute 异常: {ex}");
                throw;
            }
            if (poses.Count == 0)
            {
                Cv2.ImWrite(Path.Combine(snapDir, $"osdp-live-{tag}-miss-{i}.png"), mat);
                output.WriteLine($"[{tag}] 第 {i + 1} 帧未检出 mean={mean.Val0:0.1}");
            }
            Assert.True(poses.Count >= 1, $"[{tag}] 第 {i + 1} 帧未检出（图已存 {snapDir}）");
            var p = poses[0];
            angles.Add(p.AngleDeg);
            xs.Add(p.Cx);
            ys.Add(p.Cy);
            if (method == SegmentRefineMethod.CaliperTab)
            {
                var d = MaskCaliperTab.LastDebug;
                output.WriteLine($"[{tag}] #{i + 1:00}  {p.Cx:0.00},{p.Cy:0.00}  {p.AngleDeg:0.000}°  " +
                                 $"probes={d.ValidProbes} par={d.ParallelDeg:0.00}° w={d.WidthPx:0.0} " +
                                 $"tab={d.TabSign?.ToString(CultureInfo.InvariantCulture) ?? "n"} diff={d.TabGrayDiff:0.0}");
            }
            else
            {
                var dbg = MaskTemplateMatcher.LastDebug;
                output.WriteLine($"[{tag}] #{i + 1:00}  {p.Cx:0.00},{p.Cy:0.00}  {p.AngleDeg:0.000}°  score={p.Score:0.000}  " +
                                 $"ncc0={dbg.Score0:0.0000} ncc180={dbg.Score180:0.0000}  " +
                                 $"tSign={dbg.TemplateSign} sSign={dbg.SceneSign}  d={dbg.PeakDistPx:0.0}px");
            }
        }

        var flips = 0;
        for (var i = 1; i < angles.Count; i++)
        {
            if (Math.Abs(Wrap180(angles[i] - angles[i - 1])) > 90)
                flips++;
        }

        var meanA = angles.Average();
        var span = angles.Max(a => Math.Abs(Wrap180(a - meanA)));
        var xSpan = xs.Max() - xs.Min();
        var ySpan = ys.Max() - ys.Min();
        output.WriteLine($"[{tag}] flips={flips}  angleSpan={span:0.000}°  Xspan={xSpan:0.02}px  Yspan={ySpan:0.02}px");

        Assert.Equal(0, flips);
        Assert.True(span < 3.0, $"[{tag}] 角度极差 {span:0.00}° 过大（交付：无 180° 翻转且同姿态 <3°）");
        if (method == SegmentRefineMethod.CaliperTab)
            Assert.True(ySpan < 8.0, $"[{tag}] Y 极差 {ySpan:0.02}px 过大（短轴应稳住壳体中缝）");
    }

    private void DiagnoseSegmentation(ModelManager models, RecipeConfig recipe, VisionImage image)
    {
        using var roiImage = RoiHelper.CropToVisionImage(image, recipe.Roi, out _, out _);
        var input = roiImage ?? image;
        var session = models.Open(recipe.Models[0], InferenceTask.Segmentation);
        var segs = session.Run(y =>
            y.RunSegmentation(input, recipe.Confidence, recipe.Segmentation.PixelConfidence, recipe.Iou));
        output.WriteLine($"分割 {segs.Count} 个（ROI 内 {input.Width}x{input.Height}）");
        foreach (var s in segs.Take(5))
            output.WriteLine($"  {s.Label} conf={s.Confidence:0.000} box={s.Box.Width:0}x{s.Box.Height:0} @({s.Box.Left:0},{s.Box.Top:0})");

        if (segs.Count == 0 && recipe.Roi is not null)
        {
            var full = session.Run(y =>
                y.RunSegmentation(image, recipe.Confidence, recipe.Segmentation.PixelConfidence, recipe.Iou));
            output.WriteLine($"全图分割 {full.Count} 个（{image.Width}x{image.Height}）");
            foreach (var s in full.Take(5))
                output.WriteLine($"  {s.Label} conf={s.Confidence:0.000} box={s.Box.Width:0}x{s.Box.Height:0}");
        }
    }

    private static string OsdpRecipesDir()
    {
        var wpfBin = TestBuildPaths.ResolveWpfBin();
        var settings = wpfBin is not null ? Path.Combine(wpfBin, "appsettings.json") : "";
        if (File.Exists(settings))
        {
            try
            {
                var cfg = JsonSerializer.Deserialize<AppConfig>(File.ReadAllText(settings),
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (cfg is { DataRoot.Length: > 0 })
                {
                    DataRootBinder.Apply(cfg);
                    if (File.Exists(Path.Combine(cfg.RecipesFolder, "OSDP.json")))
                        return cfg.RecipesFolder;
                }
            }
            catch (JsonException)
            {
            }
        }

        return TestBuildPaths.ResolveRecipesDir()
               ?? Path.Combine(wpfBin ?? RepoRoot, "recipes");
    }

    private static (string DeviceId, double? ExposureUs, double? Gain) ReadCamBasler(string appsettingsPath)
    {
        if (!File.Exists(appsettingsPath))
            return ("", null, null);
        using var doc = JsonDocument.Parse(File.ReadAllText(appsettingsPath));
        if (!doc.RootElement.TryGetProperty("Cameras", out var cameras))
            return ("", null, null);
        foreach (var cam in cameras.EnumerateArray())
        {
            if (!cam.TryGetProperty("Id", out var id) || id.GetString() != "cam_basler")
                continue;
            var deviceId = cam.TryGetProperty("DeviceId", out var d) ? d.GetString() ?? "" : "";
            double? exposure = cam.TryGetProperty("ExposureTimeUs", out var e) && e.ValueKind is JsonValueKind.Number
                ? e.GetDouble() : null;
            double? gain = cam.TryGetProperty("Gain", out var g) && g.ValueKind is JsonValueKind.Number
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
