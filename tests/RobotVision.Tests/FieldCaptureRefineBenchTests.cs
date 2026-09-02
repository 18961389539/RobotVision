using System.Diagnostics;
using OpenCvSharp;
using OpenCvSharp.Features2D;
using RobotVision.Core.Geometry;
using RobotVision.Core.Models;
using RobotVision.Core.Recipe;
using RobotVision.Infrastructure;
using RobotVision.Infrastructure.Inference;
using RobotVision.Infrastructure.Inference.Strategies;
using RobotVision.Vision.Inference.Strategies;
using Xunit;
using Xunit.Abstractions;

namespace RobotVision.Tests;

/// <summary>
/// 现场成功留存图批量精修：剪影轮廓对比 + Product 配方真实分割。
/// 默认目录：Wpf bin 下 data/captures/yyyy-MM-dd（可通过环境变量 FIELD_CAPTURE_DIR 覆盖）。
/// </summary>
[Trait("Category", "Bench")]
public sealed class FieldCaptureRefineBenchTests(ITestOutputHelper output)
{
    private static string DefaultCaptureDir
    {
        get
        {
            var wpfBin = TestBuildPaths.ResolveWpfBin();
            return wpfBin is null
                ? ""
                : Path.Combine(wpfBin, "data", "captures", "2026-08-28");
        }
    }

    private static string RequireCaptureDir()
    {
        var dir = Environment.GetEnvironmentVariable("FIELD_CAPTURE_DIR") ?? DefaultCaptureDir;
        TestPreconditions.RequireDirectory(dir, $"Capture directory not found: {dir}");
        return dir;
    }

    private static (string CaptureDir, string RecipeDir, string ModelsDir) ResolveBenchAssets()
    {
        var dir = RequireCaptureDir();

        var recipeDir = TestBuildPaths.ResolveRecipesDir()
                        ?? TestBuildPaths.CombineWpf("recipes");
        var modelsDir = TestBuildPaths.ResolveModelsDir()
                        ?? TestBuildPaths.CombineWpf("models");
        TestPreconditions.RequireFile(Path.Combine(recipeDir, "Product.json"), "Missing Product.json for bench.");
        TestPreconditions.RequireFile(Path.Combine(modelsDir, "OSFP-SEG.onnx"), "Missing OSFP-SEG.onnx for bench.");
        return (dir, recipeDir, modelsDir);
    }

    [SkippableFact]
    public void Bench_field_captures_compare_refine_paths()
    {
        var dir = RequireCaptureDir();

        var files = Directory.GetFiles(dir, "*_Product_OK.png", SearchOption.TopDirectoryOnly)
            .OrderBy(f => f).ToArray();
        Assert.NotEmpty(files);

        var teachArea = 285172.0;
        var teachAspect = 2.14;
        var rows = new List<Row>();
        foreach (var file in files)
        {
            using var img = DecodeGray(file);
            Assert.False(img.Empty(), file);
            var contour = ExtractConnectorContour(img, teachArea, teachAspect);
            if (contour.Length < 16)
            {
                output.WriteLine($"SKIP {Path.GetFileName(file)}: contour too small ({contour.Length})");
                continue;
            }

            var housing = MaskHousing.Fit(contour);
            var optDark = new CaliperRefineOptions(HousingEdgePolarity.DarkToBright, TabPolarityLock.Auto);

            var legacy = MaskCaliperTab.TryRefine(img, contour,
                optDark with { ProbeLayout = CaliperProbeLayout.AcrossShortAxis });
            var auto = MaskCaliperTab.TryRefine(img, contour, optDark);
            var lateralOnly = MaskCaliperTab.TryRefine(img, contour,
                optDark with { ProbeLayout = CaliperProbeLayout.AcrossLongAxis });
            var line = MaskTemplateMatcher.RefineByLineFit(contour, housing.LongAxisDeg);
            var lineLat = MaskTemplateMatcher.RefineByLineFitBands(contour, housing.LongAxisDeg, horizontalBands: false);

            rows.Add(new(
                Path.GetFileName(file),
                legacy.Pose,
                auto.Pose,
                lateralOnly.Pose,
                line.Fitted ? line.AngleDeg : null,
                lineLat.Fitted ? lineLat.AngleDeg : null));
        }

        Assert.NotEmpty(rows);
        PrintSummary(rows, "剪影轮廓（ROI+亮目标）");
    }

    [SkippableFact]
    public void Bench_field_captures_product_recipe_yolo()
    {
        var (dir, recipeDir, modelsDir) = ResolveBenchAssets();
        var recipe = new RecipeLoader(recipeDir).Get("Product");
        using var models = new ModelManager(modelsDir);
        var strategy = new MaskTemplateStrategy(models);

        var files = Directory.GetFiles(dir, "*_Product_OK.png", SearchOption.TopDirectoryOnly)
            .OrderBy(f => f).ToArray();
        Assert.NotEmpty(files);

        var ok = 0;
        var miss = 0;
        var refineFail = 0;
        var angles = new List<double>();
        foreach (var file in files)
        {
            using var mat = Cv2.ImDecode(File.ReadAllBytes(file), ImreadModes.Color);
            Assert.False(mat.Empty(), file);
            using var vision = VisionImageCv.FromMat(mat, ownsMat: false);
            var poses = strategy.Compute(vision, recipe);
            var usable = poses.FirstOrDefault(p => p.Usable);
            if (usable is null)
            {
                if (poses.Count == 0)
                    miss++;
                else
                    refineFail++;
                var d = MaskCaliperTab.LastDebug;
                var bad = poses[0];
                var area = bad.Overlay?.Contour is { Count: > 2 } c
                    ? InstanceGeometry.PolygonArea(c.Select(p => (p.X, p.Y)).ToList())
                    : 0;
                output.WriteLine(
                    $"{Path.GetFileName(file)}  FAIL n={poses.Count} usable={bad.Usable} " +
                    $"{bad.AngleDeg:0.00}° area={area:0} " +
                    $"probes={d.ValidProbes} par={d.ParallelDeg:0.00} w={d.WidthPx:0.0} tab={d.TabSign} diff={d.TabGrayDiff:0.0}");
                continue;
            }

            ok++;
            angles.Add(usable.AngleDeg);
            output.WriteLine(
                $"{Path.GetFileName(file)}  {usable.AngleDeg:0.00}° @{usable.Cx:0},{usable.Cy:0} score={usable.Score:0.00}");
        }

        output.WriteLine($"Product 配方+分割 成功 {ok}/{files.Length}  未检出 {miss}  精修失败 {refineFail}");
        if (angles.Count > 1)
        {
            var m = angles.Average();
            var sigma = Math.Sqrt(angles.Select(a => (a - m) * (a - m)).Average());
            output.WriteLine($"角度 mean={m:0.00}° σ={sigma:0.00}°");
        }

        Assert.True(ok >= files.Length * 3 / 4,
            $"真实分割卡尺成功率过低：{ok}/{files.Length}");
    }

    /// <summary>
    /// 同一批现场图：SIFT（示教首帧裁剪）对照卡尺；并对比配方 NCC 模板。
    /// </summary>
    [SkippableFact]
    public void Bench_field_captures_sift_vs_caliper()
    {
        var (dir, recipeDir, modelsDir) = ResolveBenchAssets();
        SIFT? sift;
        try
        {
            sift = SIFT.Create(nFeatures: 800, nOctaveLayers: 3, contrastThreshold: 0.02,
                edgeThreshold: 10, sigma: 1.6);
        }
        catch (Exception ex)
        {
            output.WriteLine($"跳过：OpenCV 无 SIFT（{ex.GetType().Name}: {ex.Message}）");
            return;
        }

        using (sift)
        {
            var recipe = new RecipeLoader(recipeDir).Get("Product");
            using var models = new ModelManager(modelsDir);
            var strategy = new MaskTemplateStrategy(models);
            using var nccTemplate = string.IsNullOrEmpty(recipe.Template.TemplateImageBase64)
                ? null
                : MaskTemplateMatcher.DecodeTemplatePng(recipe.Template.TemplateImageBase64);
            using var prodTeach = nccTemplate is null || nccTemplate.Empty()
                ? null
                : MaskSiftRefine.BuildTeach(nccTemplate);

            var files = Directory.GetFiles(dir, "*_Product_OK.png", SearchOption.TopDirectoryOnly)
                .OrderBy(f => f).ToArray();
            Assert.NotEmpty(files);

            SiftTeach? teach = null;
            var siftOk = 0;
            var siftFlip = 0;
            var siftWeak = 0;
            var siftBad = 0;
            var nccOk = 0;
            var nccFlip = 0;
            var nccFail = 0;
            var undirectedErrs = new List<double>();
            var xyErrs = new List<double>();
            var inliersN = new List<int>();
            var kpN = new List<int>();
            var siftMs = new List<double>();
            var nccMs = new List<double>();
            var prodOk = 0;
            var prodFlip = 0;
            var prodFail = 0;

            try
            {
                foreach (var file in files)
                {
                    using var mat = Cv2.ImDecode(File.ReadAllBytes(file), ImreadModes.Color);
                    Assert.False(mat.Empty(), file);
                    using var vision = VisionImageCv.FromMat(mat, ownsMat: false);
                    var poses = strategy.Compute(vision, recipe);
                    var cal = poses.FirstOrDefault(p => p.Usable);
                    if (cal is null || cal.Overlay?.Contour is not { Count: >= 8 } contourPts)
                    {
                        output.WriteLine($"{Path.GetFileName(file)}  SKIP no caliper pose");
                        continue;
                    }

                    var contour = contourPts.Select(p => new Point2f((float)p.X, (float)p.Y)).ToArray();
                    var name = Path.GetFileName(file);

                    using var query = ExtractSiftView(mat, contour, sift);
                    if (query is null)
                    {
                        siftWeak++;
                        output.WriteLine($"{name}  SIFT=no-keypoints  cal={cal.AngleDeg:0.00}°");
                        continue;
                    }

                    kpN.Add(query.KeyPoints.Length);
                    if (teach is null)
                    {
                        teach = query.CloneAsTeach(cal.AngleDeg, cal.Cx, cal.Cy, name);
                        output.WriteLine(
                            $"示教 {name}  kp={teach.KeyPoints.Length}  cal={cal.AngleDeg:0.00}° @{cal.Cx:0},{cal.Cy:0}");
                    }

                    var sw = Stopwatch.StartNew();
                    var hit = AlignSift(teach, query);
                    sw.Stop();
                    siftMs.Add(sw.Elapsed.TotalMilliseconds);

                    string siftTxt;
                    if (hit is null)
                    {
                        siftWeak++;
                        siftTxt = "WEAK";
                    }
                    else
                    {
                        inliersN.Add(hit.Inliers);
                        var dAng = Math.Abs(AngleGeometry.NormalizeSignedDeg(hit.AngleDeg - cal.AngleDeg));
                        var dUnd = AngleGeometry.UndirectedDeltaDeg(hit.AngleDeg, cal.AngleDeg);
                        var dxy = Dist(hit.Cx, hit.Cy, cal.Cx, cal.Cy);
                        undirectedErrs.Add(dUnd);
                        xyErrs.Add(dxy);
                        if (dUnd < 2.0 && dxy < 20)
                        {
                            if (dAng > 150)
                            {
                                siftFlip++;
                                siftTxt = $"FLIP {hit.AngleDeg:0.00}° in={hit.Inliers}";
                            }
                            else
                            {
                                siftOk++;
                                siftTxt = $"OK {hit.AngleDeg:0.00}° Δ{dUnd:0.02}° xy={dxy:0.0} in={hit.Inliers}/{hit.Matches}";
                            }
                        }
                        else
                        {
                            siftBad++;
                            siftTxt = $"BAD {hit.AngleDeg:0.00}° Δu={dUnd:0.02}° Δ={dAng:0.1}° xy={dxy:0.0} in={hit.Inliers}/{hit.Matches}";
                        }
                    }

                    string nccTxt = "—";
                    if (nccTemplate is not null)
                    {
                        var nccSw = Stopwatch.StartNew();
                        try
                        {
                            var crop = MaskTemplateMatcher.UprightCrop(mat, contour, 0.15);
                            using (crop.Upright)
                            {
                                var match = MaskTemplateMatcher.MatchBest(
                                    crop.Upright, nccTemplate, recipe.Template.RefineRangeDeg, 0.01);
                                nccSw.Stop();
                                nccMs.Add(nccSw.Elapsed.TotalMilliseconds);
                                if (match is null)
                                {
                                    nccFail++;
                                    nccTxt = "NCC fail";
                                }
                                else
                                {
                                    var nccAng = AngleGeometry.NormalizeSignedDeg(crop.WarpAngleDeg + match.RotationDeg);
                                    var nccC = MaskTemplateMatcher.MapUprightToSource(crop, match.CenterInUpright);
                                    var dAng = Math.Abs(AngleGeometry.NormalizeSignedDeg(nccAng - cal.AngleDeg));
                                    var dUnd = AngleGeometry.UndirectedDeltaDeg(nccAng, cal.AngleDeg);
                                    var dxy = Dist(nccC.X, nccC.Y, cal.Cx, cal.Cy);
                                    var passTh = match.Score >= recipe.Template.MatchThreshold;
                                    if (dUnd < 2.0 && dxy < 20 && passTh)
                                    {
                                        if (dAng > 150)
                                        {
                                            nccFlip++;
                                            nccTxt = $"NCC FLIP {nccAng:0.00}° s={match.Score:0.00}";
                                        }
                                        else
                                        {
                                            nccOk++;
                                            nccTxt = $"NCC OK {nccAng:0.00}° s={match.Score:0.00} Δ{dUnd:0.02}°";
                                        }
                                    }
                                    else
                                    {
                                        nccFail++;
                                        nccTxt = $"NCC BAD {nccAng:0.00}° s={match.Score:0.00} Δu={dUnd:0.02}° xy={dxy:0.0}";
                                    }
                                }
                            }
                        }
                        catch (InvalidOperationException)
                        {
                            nccSw.Stop();
                            nccFail++;
                            nccTxt = "NCC crop-fail";
                        }
                    }

                    var prodTxt = "";
                    if (prodTeach is not null)
                    {
                        var prod = MaskSiftRefine.TryRefine(mat, contour, prodTeach);
                        if (prod.Pose is null)
                        {
                            prodFail++;
                            prodTxt = "  prod=WEAK";
                        }
                        else
                        {
                            var p = prod.Pose;
                            var dAng = Math.Abs(AngleGeometry.NormalizeSignedDeg(p.AngleDeg - cal.AngleDeg));
                            var dUnd = AngleGeometry.UndirectedDeltaDeg(p.AngleDeg, cal.AngleDeg);
                            var dxy = Dist(p.Center.X, p.Center.Y, cal.Cx, cal.Cy);
                            if (dUnd < 2.0 && dxy < 20)
                            {
                                if (dAng > 150)
                                {
                                    prodFlip++;
                                    prodTxt = $"  prod=FLIP {p.AngleDeg:0.00}°";
                                }
                                else
                                {
                                    prodOk++;
                                    prodTxt = $"  prod=OK {p.AngleDeg:0.00}° Δ{dUnd:0.02}° xy={dxy:0.0} in={p.Inliers}";
                                }
                            }
                            else
                            {
                                prodFail++;
                                prodTxt = $"  prod=BAD {p.AngleDeg:0.00}° Δu={dUnd:0.02}° xy={dxy:0.0} in={p.Inliers}";
                            }
                        }
                    }

                    output.WriteLine($"{name}  cal={cal.AngleDeg:0.00}°  SIFT={siftTxt}  {nccTxt}{prodTxt}  kp={query.KeyPoints.Length}");
                }
            }
            finally
            {
                teach?.Dispose();
            }

            var n = files.Length;
            output.WriteLine("--- SIFT vs 卡尺 ---");
            output.WriteLine($"SIFT 成功(无向<2°且XY<20px) {siftOk}/{n}  180°翻转 {siftFlip}  匹配弱 {siftWeak}  偏差大 {siftBad}");
            if (undirectedErrs.Count > 0)
                output.WriteLine(
                    $"SIFT 无向角误差 mean={undirectedErrs.Average():0.03}°  p50={Pct(undirectedErrs, 0.5):0.03}°  p90={Pct(undirectedErrs, 0.9):0.03}°  " +
                    $"XY mean={xyErrs.Average():0.1}px  inliers mean={(inliersN.Count == 0 ? 0 : inliersN.Average()):0.0}  " +
                    $"kp mean={(kpN.Count == 0 ? 0 : kpN.Average()):0.0}  {siftMs.Average():0.0} ms/张");
            if (nccTemplate is not null)
            {
                output.WriteLine($"NCC 模板 成功 {nccOk}/{n}  180°翻转 {nccFlip}  失败 {nccFail}" +
                                 (nccMs.Count > 0 ? $"  {nccMs.Average():0.0} ms/张" : ""));
            }
            if (prodTeach is not null)
                output.WriteLine($"SIFT 产线路径（配方示教图）成功 {prodOk}/{n}  180°翻转 {prodFlip}  失败 {prodFail}");
        }
    }

    private static double Pct(List<double> v, double p)
    {
        var a = v.OrderBy(x => x).ToArray();
        var i = (int)Math.Clamp(Math.Round(p * (a.Length - 1)), 0, a.Length - 1);
        return a[i];
    }

    private static double Dist(double x0, double y0, double x1, double y1)
    {
        var dx = x0 - x1;
        var dy = y0 - y1;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    private sealed class SiftTeach : IDisposable
    {
        public required string Name;
        public required KeyPoint[] KeyPoints;
        public required Mat Descriptors;
        public required double OriginX;
        public required double OriginY;
        public required double ObjectX;
        public required double ObjectY;
        public required double AngleDeg;

        public SiftTeach CloneAsTeach(double angleDeg, double cx, double cy, string name) => new()
        {
            Name = name,
            KeyPoints = KeyPoints,
            Descriptors = Descriptors.Clone(),
            OriginX = OriginX,
            OriginY = OriginY,
            ObjectX = cx - OriginX,
            ObjectY = cy - OriginY,
            AngleDeg = angleDeg,
        };

        public void Dispose() => Descriptors.Dispose();
    }

    private sealed record SiftHit(double AngleDeg, double Cx, double Cy, int Matches, int Inliers);

    private static SiftTeach? ExtractSiftView(Mat bgr, Point2f[] contour, SIFT sift)
    {
        var box = Cv2.BoundingRect(contour);
        var padX = Math.Max(12, (int)(box.Width * 0.12));
        var padY = Math.Max(12, (int)(box.Height * 0.12));
        var x = Math.Max(0, box.X - padX);
        var y = Math.Max(0, box.Y - padY);
        var w = Math.Min(bgr.Width - x, box.Width + 2 * padX);
        var h = Math.Min(bgr.Height - y, box.Height + 2 * padY);
        if (w < 32 || h < 32)
            return null;

        using var roi = new Mat(bgr, new Rect(x, y, w, h));
        using var gray = new Mat();
        if (roi.Channels() == 1)
            roi.CopyTo(gray);
        else
            Cv2.CvtColor(roi, gray, roi.Channels() == 4 ? ColorConversionCodes.BGRA2GRAY : ColorConversionCodes.BGR2GRAY);

        var desc = new Mat();
        sift.DetectAndCompute(gray, null, out var kps, desc);
        if (kps.Length < 8 || desc.Empty())
        {
            desc.Dispose();
            return null;
        }

        return new SiftTeach
        {
            Name = "",
            KeyPoints = kps,
            Descriptors = desc,
            OriginX = x,
            OriginY = y,
            ObjectX = w / 2.0,
            ObjectY = h / 2.0,
            AngleDeg = 0,
        };
    }

    private static SiftHit? AlignSift(SiftTeach teach, SiftTeach query)
    {
        if (teach.Descriptors.Empty() || query.Descriptors.Empty())
            return null;

        using var matcher = new BFMatcher(NormTypes.L2, crossCheck: false);
        var knn = matcher.KnnMatch(query.Descriptors, teach.Descriptors, k: 2);
        var src = new List<Point2f>();
        var dst = new List<Point2f>();
        foreach (var pair in knn)
        {
            if (pair.Length < 2)
                continue;
            if (pair[0].Distance >= 0.75 * pair[1].Distance)
                continue;
            src.Add(teach.KeyPoints[pair[0].TrainIdx].Pt);
            dst.Add(query.KeyPoints[pair[0].QueryIdx].Pt);
        }

        if (src.Count < 8)
            return null;

        using var inliers = new Mat();
        using var affine = Cv2.EstimateAffinePartial2D(
            InputArray.Create(src), InputArray.Create(dst), inliers,
            RobustEstimationAlgorithms.RANSAC, 3.5, 2000, 0.99, 10);
        if (affine is null || affine.Empty())
            return null;

        var nIn = 0;
        if (!inliers.Empty())
        {
            for (var i = 0; i < inliers.Rows; i++)
            {
                if (inliers.At<byte>(i, 0) != 0)
                    nIn++;
            }
        }

        if (nIn < 6)
            return null;

        var a = affine.At<double>(0, 0);
        var b = affine.At<double>(1, 0);
        var tx = affine.At<double>(0, 2);
        var ty = affine.At<double>(1, 2);
        var scale = Math.Sqrt(a * a + b * b);
        if (scale < 0.6 || scale > 1.6)
            return null;

        var dRad = Math.Atan2(b, a);
        var dDeg = dRad * 180.0 / Math.PI;
        var qx = a * teach.ObjectX - b * teach.ObjectY + tx;
        var qy = b * teach.ObjectX + a * teach.ObjectY + ty;
        return new SiftHit(
            AngleGeometry.NormalizeSignedDeg(teach.AngleDeg + dDeg),
            query.OriginX + qx,
            query.OriginY + qy,
            src.Count,
            nIn);
    }

    private void PrintSummary(List<Row> rows, string tag)
    {
        static int Ok(Row r, Func<Row, MaskCaliperTab.Result?> pick) => pick(r) is not null ? 1 : 0;

        var n = rows.Count;
        var legacyOk = rows.Sum(r => Ok(r, x => x.Legacy));
        var autoOk = rows.Sum(r => Ok(r, x => x.Auto));
        var lateralOk = rows.Sum(r => Ok(r, x => x.Lateral));
        var lineOk = rows.Count(r => r.LineDeg is not null);
        var lineLatOk = rows.Count(r => r.LineLateralDeg is not null);

        output.WriteLine($"{tag} 样本 {n} 张");
        output.WriteLine($"卡尺-上下(旧): {legacyOk}/{n}  卡尺-Auto(新): {autoOk}/{n}  卡尺-左右: {lateralOk}/{n}");
        output.WriteLine($"直线-上下→左右回退: {lineOk}/{n}  直线-仅左右: {lineLatOk}/{n}");

        var autoAngles = rows.Where(r => r.Auto is not null).Select(r => r.Auto!.AngleDeg).ToArray();
        if (autoAngles.Length > 1)
            output.WriteLine($"Auto 角度: mean={autoAngles.Average():0.###}° σ={Std(autoAngles):0.###}°");

        var tabFlip = rows.Count(r =>
            r.Legacy is not null && r.Auto is not null &&
            Math.Abs(AngleGeometry.NormalizeSignedDeg(r.Legacy.AngleDeg - r.Auto.AngleDeg)) > 150);
        output.WriteLine($"Legacy vs Auto 头尾翻转(>150°): {tabFlip} 张");

        output.WriteLine("--- 逐张 ---");
        foreach (var r in rows)
        {
            output.WriteLine(
                $"{r.Name}  L={Fmt(r.Legacy)}  A={Fmt(r.Auto)}  LR={Fmt(r.Lateral)}  " +
                $"line={FmtDeg(r.LineDeg)} lat={FmtDeg(r.LineLateralDeg)}");
        }
    }

    private static string Fmt(MaskCaliperTab.Result? r) =>
        r is null ? "—" : $"{r.AngleDeg:0.00}° @{r.Center.X:0},{r.Center.Y:0} tab={r.TabSign}";

    private static string FmtDeg(double? d) => d is null ? "—" : $"{d:0.00}°";

    private static double Std(double[] v)
    {
        if (v.Length < 2)
            return 0;
        var m = v.Average();
        return Math.Sqrt(v.Select(x => (x - m) * (x - m)).Average());
    }

    private static Mat DecodeGray(string file)
    {
        using var color = Cv2.ImDecode(File.ReadAllBytes(file), ImreadModes.Color);
        if (color.Empty())
            return new Mat();
        var gray = new Mat();
        Cv2.CvtColor(color, gray, ColorConversionCodes.BGR2GRAY);
        return gray;
    }

    /// <summary>
    /// 暗场亮连接器：高阈值 + 闭运算，按示教面积/轴比挑目标，避免夹爪/光斑粘连。
    /// </summary>
    internal static Point2f[] ExtractConnectorContour(Mat gray, double teachArea, double teachAspect)
    {
        var roi = new Rect(
            (int)(gray.Width * 0.30),
            (int)(gray.Height * 0.32),
            (int)(gray.Width * 0.50),
            (int)(gray.Height * 0.48));
        roi &= new Rect(0, 0, gray.Width, gray.Height);
        using var view = new Mat(gray, roi);
        using var blur = new Mat();
        Cv2.GaussianBlur(view, blur, new Size(5, 5), 0);
        using var bin = new Mat();
        Cv2.Threshold(blur, bin, 80, 255, ThresholdTypes.Binary);
        using var kernel = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(9, 9));
        using var closed = new Mat();
        Cv2.MorphologyEx(bin, closed, MorphTypes.Close, kernel);

        Cv2.FindContours(closed, out var contours, out _, RetrievalModes.External, ContourApproximationModes.ApproxNone);
        if (contours.Length == 0)
            return [];

        Point[]? best = null;
        var bestScore = double.PositiveInfinity;
        foreach (var c in contours)
        {
            var area = Cv2.ContourArea(c);
            if (area < 400)
                continue;
            var shifted = c.Select(p => new Point2f(p.X + roi.X, p.Y + roi.Y)).ToArray();
            var housing = MaskHousing.Fit(shifted);
            var aspect = housing.LongLen / Math.Max(1.0, housing.ShortLen);
            var areaErr = teachArea > 1 ? Math.Abs(Math.Log(area / teachArea)) : 0;
            var aspectErr = teachAspect > 1e-3 ? Math.Abs(Math.Log(aspect / teachAspect)) : 0;
            var score = areaErr + 0.6 * aspectErr;
            if (score < bestScore)
            {
                bestScore = score;
                best = c;
            }
        }

        if (best is null)
            return [];
        return best.Select(p => new Point2f(p.X + roi.X, p.Y + roi.Y)).ToArray();
    }

    private sealed record Row(
        string Name,
        MaskCaliperTab.Result? Legacy,
        MaskCaliperTab.Result? Auto,
        MaskCaliperTab.Result? Lateral,
        double? LineDeg,
        double? LineLateralDeg);
}
