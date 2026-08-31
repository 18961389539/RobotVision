using OpenCvSharp;
using RobotVision.Infrastructure.Inference.Strategies;
using Xunit;
using Xunit.Abstractions;

namespace RobotVision.Tests;

/// <summary>
/// 模板匹配（MaskTemplate 精修级）在弱纹理目标上的角度抖动评估：
/// 合成目标（弱/中/强纹理三档）按已知角度渲染 + 传感器噪声，
/// 统计测角误差均值（偏差）与标准差（抖动）。弱纹理 = 平滑表面仅轮廓有信息。
/// </summary>
public class MaskTemplateJitterTests(ITestOutputHelper output)
{
    private const int ImgSize = 640;
    private const int TargetW = 280;
    private const int TargetH = 110;

    [Fact]
    public void JitterEvaluation_WeakVsStrongTexture()
    {
        var report = new List<string> { "纹理档 | 匹配域 | 噪声σ | 误差均值(°) | 抖动σ(°) | 最大误差(°) | 匹配分 | 180°翻转" };
        report.Add("--------|--------|-------|-----------|---------|-----------|--------|---------");

        foreach (var (label, texturize) in new (string, Action<Mat, Random>)[]
                 {
                     ("弱纹理", (m, r) => TexturizeSmooth(m, r)),
                     ("中纹理", (m, r) => TexturizeMedium(m, r)),
                     ("强纹理", (m, r) => TexturizeStrong(m, r)),
                 })
        {
            foreach (var mode in new[] { "灰度", "边缘图", "混合", "直线拟合" })
            {
                foreach (var noiseSigma in new[] { 0.0, 2.0, 5.0 })
                {
                    var (bias, jitter, maxErr, avgScore, flips) = Evaluate(texturize, noiseSigma, mode);
                    report.Add($"{label} | {mode} | σ={noiseSigma:0} | {bias:+0.00;-0.00} | {jitter:0.00} | {maxErr:0.00} | {avgScore:0.000} | {flips}/{Trials}");
                }
            }
        }

        output.WriteLine(string.Join("\n", report));
    }

    private const int Trials = 60;

    /// <summary>诊断：弱纹理单 case 的候选角-分数曲线（定位精修失效原因）。</summary>
    [Fact]
    public void Diagnose_WeakTexture_ScoreCurve()
    {
        Action<Mat, Random> smooth = TexturizeSmooth;
        using var templateCanvas = RenderTarget(0, smooth, seed: 1000);
        using var template = TightCrop(templateCanvas);

        var rng = new Random(5000);
        var trueAngle = rng.NextDouble() * 180.0;
        var coarseErr = 3.5; // 固定注入粗角误差：转正后目标残留 -3.5°，理想峰应在 φ=-3.5°
        using var canvas = RenderTarget(trueAngle, smooth, seed: 6000, 0);
        using var upright = UprightByTrueAngle(canvas, trueAngle + coarseErr);

        var lines = new List<string> { $"粗角误差={coarseErr}°（理想峰 φ=-3.5），候选角分数：" };
        for (var phi = -8.0; phi <= 8.0; phi += 1.0)
        {
            using var rotated = RotateTemplateForDiag(template, phi);
            if (rotated.Width > upright.Width || rotated.Height > upright.Height)
            { lines.Add($"φ={phi:0.0}:SKIP"); continue; }
            using var result = upright.MatchTemplate(rotated, TemplateMatchModes.CCoeffNormed);
            Cv2.MinMaxLoc(result, out _, out var maxVal, out _, out _);
            lines.Add($"φ={phi:0.0}:{maxVal:0.0000}");
        }
        output.WriteLine(string.Join("\n", lines));
    }

    /// <summary>诊断用：与 MaskTemplateMatcher.RotateTemplate 同实现（边缘均值填充）。</summary>
    private static Mat RotateTemplateForDiag(Mat template, double deg)
    {
        if (Math.Abs(deg) < 1e-9) return template.Clone();
        var center = new Point2f(template.Width / 2f, template.Height / 2f);
        using var m = Cv2.GetRotationMatrix2D(center, deg, 1.0);
        var rad = deg * Math.PI / 180.0;
        var cos = Math.Abs(Math.Cos(rad)); var sin = Math.Abs(Math.Sin(rad));
        var w = (int)Math.Ceiling(template.Width * cos + template.Height * sin);
        var h = (int)Math.Ceiling(template.Width * sin + template.Height * cos);
        m.Set(0, 2, m.At<double>(0, 2) + (w - template.Width) / 2.0);
        m.Set(1, 2, m.At<double>(1, 2) + (h - template.Height) / 2.0);
        using var top = template.Row(0); using var bottom = template.Row(template.Rows - 1);
        using var left = template.Col(0); using var right = template.Col(template.Cols - 1);
        var mt = top.Mean(); var mb = bottom.Mean(); var ml = left.Mean(); var mr = right.Mean();
        var fill = new Scalar((mt.Val0 + mb.Val0 + ml.Val0 + mr.Val0) / 4);
        var dst = new Mat();
        Cv2.WarpAffine(template, dst, m, new Size(w, h), InterpolationFlags.Linear, BorderTypes.Constant, fill);
        return dst;
    }

    /// <summary>合成目标 → 注入粗角误差转正 → 按模式精修 → 统计误差/抖动/头尾翻转。
    /// 粗角误差 ~N(0,3°) 模拟分割 minAreaRect 的真实精度。模式：灰度/边缘图/混合（产品接入方案）。</summary>
    private static (double Bias, double Jitter, double MaxErr, double AvgScore, int Flips) Evaluate(
        Action<Mat, Random> texturize, double noiseSigma, string mode)
    {
        // 模板：0° 基准目标（与示教同口径：转正紧裁剪），纹理 seed 独立于测试图
        using var templateCanvas = RenderTarget(0, texturize, seed: 1000);
        using var template = TightCrop(templateCanvas);

        var errors = new List<double>();
        var scores = new List<double>();
        var flips = 0;
        for (var i = 0; i < Trials; i++)
        {
            var rng = new Random(5000 + i);
            var trueAngle = rng.NextDouble() * 180.0; // 全角度域随机
            var coarseErr = Gaussian(rng) * 3.0;      // 分割粗角误差 ~N(0,3°)

            using var canvas = RenderTarget(trueAngle, texturize, seed: 6000 + i, noiseSigma);
            // 用"粗角度"（真值+误差）转正——与运行时分割给出的角度同语义
            using var uprightGray = UprightByTrueAngle(canvas, trueAngle + coarseErr);

            MaskTemplateMatchResult? match;
            switch (mode)
            {
                case "灰度":
                    match = MaskTemplateMatcher.MatchBest(uprightGray, template, 5, 0.0);
                    break;
                case "边缘图":
                    using (var ue = MaskTemplateMatcher.ToEdgeMap(uprightGray))
                    using (var te = MaskTemplateMatcher.ToEdgeMap(template))
                        match = MaskTemplateMatcher.MatchBest(ue, te, 5, 0.0);
                    break;
                case "直线拟合":
                    // 直线拟合吃掩码轮廓（与纹理无关）：合成旋转矩形轮廓直接驱动，
                    // coarseAngle = trueAngle + coarseErr（与分割粗角同语义）
                {
                    var contour = BuildContour(trueAngle);
                    var (ang, _, _) = MaskTemplateMatcher.RefineByLineFit(contour, trueAngle + coarseErr);
                    match = new MaskTemplateMatchResult(1.0, ang - (trueAngle + coarseErr), new OpenCvSharp.Point2d(0, 0));
                    break;
                }
                default: // 混合（产品方案）
                    match = MaskTemplateMatcher.MatchBestHybrid(uprightGray, template, 5, 0.0);
                    break;
            }
            Assert.NotNull(match);

            // 精修输出角应 = -coarseErr（把转正多转的角修回来）；恢复残差即角度误差
            var err = NormalizeErr(match.RotationDeg - (-coarseErr));
            errors.Add(err);
            scores.Add(match.Score);
            // 头尾翻转：匹配落在 180° 分支但纹理应指向 0° 分支（误差≈±180 的等价残差判据）
            if (Math.Abs(NormalizeRaw(match.RotationDeg)) > 90.0)
                flips++;
        }

        var bias = errors.Average();
        var jitter = Math.Sqrt(errors.Select(e => (e - bias) * (e - bias)).Average());
        return (bias, jitter, errors.Max(Math.Abs), scores.Average(), flips);
    }

    /// <summary>合成旋转矩形轮廓（模拟分割掩码边界点，每边密集采样）。</summary>
    private static Point2f[] BuildContour(double deg)
    {
        var cx = ImgSize / 2.0;
        var cy = ImgSize / 2.0;
        var hw = TargetW / 2.0;
        var hh = TargetH / 2.0;
        var corners = new[]
        {
            new Point2d(cx - hw, cy - hh), new Point2d(cx + hw, cy - hh),
            new Point2d(cx + hw, cy + hh), new Point2d(cx - hw, cy + hh),
        };
        var rad = deg * Math.PI / 180.0;
        var cos = Math.Cos(rad);
        var sin = Math.Sin(rad);
        Point2f Rot(Point2d p) => new(
            (float)(cx + (p.X - cx) * cos - (p.Y - cy) * sin),
            (float)(cy + (p.X - cx) * sin + (p.Y - cy) * cos));
        var rotatedCorners = corners.Select(Rot).ToArray();

        var pts = new List<Point2f>();
        const int samplesPerEdge = 50;
        for (var e = 0; e < 4; e++)
        {
            var a = rotatedCorners[e];
            var b = rotatedCorners[(e + 1) % 4];
            for (var s = 0; s < samplesPerEdge; s++)
            {
                var t = (double)s / samplesPerEdge;
                pts.Add(new Point2f((float)(a.X + (b.X - a.X) * t), (float)(a.Y + (b.Y - a.Y) * t)));
            }
        }
        return pts.ToArray();
    }

    /// <summary>Box-Muller 高斯采样。</summary>
    private static double Gaussian(Random rng)
    {
        double u1 = 1.0 - rng.NextDouble(), u2 = rng.NextDouble();
        return Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2);
    }

    /// <summary>原始角度归一到 (-180,180]。</summary>
    private static double NormalizeRaw(double deg)
    {
        var d = ((deg + 180.0) % 360.0 + 360.0) % 360.0 - 180.0;
        return d == -180.0 ? 180.0 : d;
    }

    /// <summary>渲染一个中心旋转 deg 的合成目标（浅灰背景 + 深色矩形 + 纹理档 + 可选噪声）。
    /// 流程：目标纹理化 → 贴到画布中心 → 绕画布中心（=目标中心）旋转。</summary>
    private static Mat RenderTarget(double deg, Action<Mat, Random> texturize, int seed, double noiseSigma = 0)
    {
        var rng = new Random(seed);

        // 目标纹理化（固定 seed=777：同一目标外观帧间稳定，真实语义——
        // 模板匹配前提是外观不变，帧间只有噪声差异；随机纹理等于换目标，不公平）
        using var target = new Mat(TargetH, TargetW, MatType.CV_8UC3, new Scalar(60, 60, 60));
        texturize(target, new Random(777));

        // 贴到画布中心（先贴后转，保证旋转中心=目标中心）
        using var canvas = new Mat(ImgSize, ImgSize, MatType.CV_8UC3, new Scalar(185, 185, 185));
        var roi = new Rect((ImgSize - TargetW) / 2, (ImgSize - TargetH) / 2, TargetW, TargetH);
        target.CopyTo(canvas[roi]);

        // 绕画布中心旋转
        var center = new Point2f(ImgSize / 2f, ImgSize / 2f);
        using var m = Cv2.GetRotationMatrix2D(center, deg, 1.0);
        using var rotated = new Mat();
        Cv2.WarpAffine(canvas, rotated, m, new Size(ImgSize, ImgSize), InterpolationFlags.Linear,
            BorderTypes.Constant, new Scalar(185, 185, 185));

        if (noiseSigma > 0)
        {
            using var noise = new Mat(rotated.Size(), MatType.CV_16SC3);
            Cv2.Randn(noise, Scalar.All(0), Scalar.All(noiseSigma));
            using var buf = new Mat();
            rotated.ConvertTo(buf, MatType.CV_16SC3);
            Cv2.Add(buf, noise, buf);
            buf.ConvertTo(rotated, MatType.CV_8UC3);
        }
        // rotated 是 using 局部变量，返回克隆避免悬垂句柄
        return rotated.Clone();
    }

    /// <summary>弱纹理：表面完全平滑（仅缓慢渐变），信息只来自轮廓边缘。</summary>
    private static void TexturizeSmooth(Mat target, Random rng)
    {
        using var grad = new Mat(target.Size(), MatType.CV_8UC1);
        for (var y = 0; y < grad.Rows; y++)
        {
            var row = grad.Row(y);
            row.SetTo(Scalar.All(70 + 20.0 * y / grad.Rows)); // 极缓渐变
        }
        Cv2.Merge(new[] { grad, grad, grad }, target);
    }

    /// <summary>中纹理：少量随机斑点（模拟丝印/轻微划痕）。</summary>
    private static void TexturizeMedium(Mat target, Random rng)
    {
        for (var i = 0; i < 25; i++)
        {
            var p = new Point(rng.Next(target.Width), rng.Next(target.Height));
            Cv2.Circle(target, p, rng.Next(2, 5), new Scalar(120, 120, 120), -1);
        }
    }

    /// <summary>强纹理：密集方向性纹理（模拟芯片丝印/引脚）。</summary>
    private static void TexturizeStrong(Mat target, Random rng)
    {
        for (var x = 8; x < target.Width - 8; x += 12)
            Cv2.Line(target, new Point(x, 6), new Point(x, target.Height - 6), new Scalar(140, 140, 140), 2);
        for (var i = 0; i < 10; i++)
        {
            var p = new Point(rng.Next(target.Width), rng.Next(target.Height));
            Cv2.Circle(target, p, 4, new Scalar(200, 90, 40), -1);
        }
    }

    /// <summary>按真值角度转正 + 15% 边距裁剪（与 MaskTemplateStrategy 同参数，窗口约 1.3 倍）。</summary>
    private static Mat UprightByTrueAngle(Mat canvas, double deg)
    {
        var center = new Point2f(canvas.Width / 2f, canvas.Height / 2f);
        using var m = Cv2.GetRotationMatrix2D(center, -deg, 1.0);
        using var rotated = new Mat();
        Cv2.WarpAffine(canvas, rotated, m, canvas.Size(), InterpolationFlags.Linear,
            BorderTypes.Reflect101);

        var margin = 0.15;
        var cropW = (int)(TargetW * (1 + 2 * margin));
        var cropH = (int)(TargetH * (1 + 2 * margin));
        var x = (int)(center.X - cropW / 2.0);
        var y = (int)(center.Y - cropH / 2.0);
        return rotated[new Rect(x, y, cropW, cropH)].Clone();
    }

    /// <summary>模板紧裁剪（与示教同口径）：中心区域直接取。</summary>
    private static Mat TightCrop(Mat templateCanvas)
    {
        var center = new Point2f(templateCanvas.Width / 2f, templateCanvas.Height / 2f);
        var x = (int)(center.X - TargetW / 2.0);
        var y = (int)(center.Y - TargetH / 2.0);
        return templateCanvas[new Rect(x, y, TargetW, TargetH)].Clone();
    }

    /// <summary>误差归一到 (-90,90]（模板匹配含 180° 分支，±180 等价）。</summary>
    private static double NormalizeErr(double deg)
    {
        var d = ((deg + 90.0) % 360.0 + 360.0) % 360.0 - 90.0;
        return d > 90.0 ? d - 180.0 : d;
    }
}
