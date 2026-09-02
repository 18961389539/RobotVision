using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using OpenCvSharp;
using RobotVision.Core;
using RobotVision.Core.Models;
using RobotVision.Core.Recipe;
using RobotVision.Hosting;
using RobotVision.Infrastructure;
using RobotVision.Infrastructure.Calibration;
using RobotVision.Infrastructure.Cameras;
using RobotVision.Infrastructure.Communication;
using RobotVision.Infrastructure.Inference;
using RobotVision.Infrastructure.Inference.Strategies;
using RobotVision.Vision.Inference.Strategies;
using RobotVision.Infrastructure.Lighting;
using Xunit;
using Xunit.Abstractions;

namespace RobotVision.Tests;

/// <summary>
/// ROI 有/无对打：同一张图经 VisionService（取图 → 策略 → 比例映射 → TCP 应答）
/// 两条配方只差 roi，发给 PLC 的 X/Y/角度必须一致。
/// DualBlob 走真实像素连通域；MaskMinAreaRect 用「亮像素包围盒」假分割，
/// 保证裁剪前后看到的是同一块几何，而不是假引擎写死的局部坐标。
/// </summary>
public sealed class RoiPlcParityTests : IDisposable
{
    private const int W = 400;
    private const int H = 300;
    private const double ScaleMmPerPx = 0.1;

    private readonly string _root = Path.Combine(Path.GetTempPath(), "rv_roi_plc_" + Guid.NewGuid().ToString("N"));
    private readonly ITestOutputHelper _out;

    public RoiPlcParityTests(ITestOutputHelper output)
    {
        _out = output;
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, true); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    [Fact]
    public async Task DualBlob_SameImage_WithAndWithoutRoi_PlcXyAngleMatch()
    {
        using var mat = new Mat(H, W, MatType.CV_8UC3, Scalar.Black);
        Cv2.Circle(mat, 200, 150, 20, Scalar.White, -1);
        Cv2.Circle(mat, 250, 150, 8, Scalar.White, -1);

        var (full, roi) = await RunPairAsync(mat, AngleMode.DualBlobCenterLine, models: []);

        AssertPlcPoseMatch(full, roi);
        full.Poses[0].X.Should().BeApproximately(200 * ScaleMmPerPx, 0.3);
        full.Poses[0].Y.Should().BeApproximately(150 * ScaleMmPerPx, 0.3);
        full.Poses[0].AngleDeg.Should().BeApproximately(0, 1.5);
    }

    [Fact]
    public async Task DualBlob_RoiMissesTarget_NoTargetWhileFullImageFinds()
    {
        using var mat = new Mat(H, W, MatType.CV_8UC3, Scalar.Black);
        Cv2.Circle(mat, 200, 150, 20, Scalar.White, -1);
        Cv2.Circle(mat, 250, 150, 8, Scalar.White, -1);

        var missRoi = new Roi(0, 0, 0.2, 0.2); // 左上角空区，切不到斑点
        var (full, roi) = await RunPairAsync(mat, AngleMode.DualBlobCenterLine, models: [], roi: missRoi);

        full.Ok.Should().BeTrue();
        roi.Ok.Should().BeFalse();
        roi.ErrorCode.Should().Be(VisionErrorCode.NoTargetFound);
        _out.WriteLine($"无ROI {PlcXyAngleCsv(full)}");
        _out.WriteLine($"有ROI(空区) ERR,{(int)roi.ErrorCode},{roi.ErrorCode}");
    }

    [Fact]
    public async Task MaskMinAreaRect_SameImage_WithAndWithoutRoi_PlcXyAngleMatch()
    {
        using var mat = new Mat(H, W, MatType.CV_8UC3, Scalar.Black);
        Cv2.Rectangle(mat, new Rect(160, 130, 80, 20), Scalar.White, -1);

        var (full, roi) = await RunPairAsync(mat, AngleMode.MaskMinAreaRect, models: ["seg.onnx"]);

        AssertPlcPoseMatch(full, roi);
        full.Poses[0].X.Should().BeApproximately(200 * ScaleMmPerPx, 0.3);
        full.Poses[0].Y.Should().BeApproximately(140 * ScaleMmPerPx, 0.3);
        full.Poses[0].AngleDeg.Should().BeApproximately(0, 1.5);
    }

    /// <summary>覆盖目标的检测 ROI（相对 400×300：像素 100,75 ~ 300,225）。</summary>
    private static Roi CoveringRoi => new(0.25, 0.25, 0.5, 0.5);

    private async Task<(VisionResult Full, VisionResult Roi)> RunPairAsync(
        Mat image, AngleMode mode, string[] models, Roi? roi = null)
    {
        roi ??= CoveringRoi;
        var caseDir = Path.Combine(_root, Guid.NewGuid().ToString("N"));
        var recipesDir = Path.Combine(caseDir, "recipes");
        var framesDir = Path.Combine(caseDir, "frames");
        var modelsDir = Path.Combine(caseDir, "models");
        Directory.CreateDirectory(recipesDir);
        Directory.CreateDirectory(framesDir);
        Directory.CreateDirectory(modelsDir);

        Cv2.ImWrite(Path.Combine(framesDir, "frame.bmp"), image);
        foreach (var model in models)
            File.WriteAllText(Path.Combine(modelsDir, model), "fake-onnx");

        WriteRecipe(recipesDir, "FULL", mode, models, roi: null);
        WriteRecipe(recipesDir, "ROI", mode, models, roi);

        var calibration = new CalibrationManager();
        calibration.LoadScale(new ScaleProfile
        {
            StationId = "st_scale",
            CameraId = "cam1",
            ScaleX = ScaleMmPerPx,
            ScaleY = ScaleMmPerPx,
            Width = W,
            Height = H,
        });

        var service = CreateService(recipesDir, framesDir, modelsDir, calibration, mode);
        var full = await service.RunAsync("FULL", CancellationToken.None);
        var cropped = await service.RunAsync("ROI", CancellationToken.None);
        return (full, cropped);
    }

    private static void WriteRecipe(
        string folder, string name, AngleMode mode, string[] models, Roi? roi)
    {
        var modelsJson = models.Length == 0
            ? "[]"
            : "[" + string.Join(",", models.Select(m => $"\"{m}\"")) + "]";
        var roiJson = roi is null
            ? "null"
            : $$"""{"x": {{roi.X}}, "y": {{roi.Y}}, "width": {{roi.Width}}, "height": {{roi.Height}}}""";
        File.WriteAllText(Path.Combine(folder, name + ".json"), $$"""
            {
              "cameraId": "cam1",
              "stationId": "st_scale",
              "angleMode": "{{mode}}",
              "models": {{modelsJson}},
              "roi": {{roiJson}}
            }
            """);
    }

    private static VisionService CreateService(
        string recipesDir, string framesDir, string modelsDir,
        CalibrationManager calibration, AngleMode mode)
    {
        var recipes = new RecipeLoader(recipesDir);
        var cameras = new CameraManager();
        cameras.Register(new FileCamera("cam1", framesDir, loop: true));

        IInferenceEngineFactory? factory = mode == AngleMode.MaskMinAreaRect
            ? new FakeInferenceEngineFactory(() => new FakeInferenceEngine
            {
                OnSegmentation = SegmentBrightBlob,
            })
            : null;

        var failures = new FailureImageStore(
            new FailureImageConfig { Folder = Path.Combine(Path.GetTempPath(), "rv_roi_plc_fail") },
            NullLogger<FailureImageStore>.Instance);

        return new VisionService(
            recipes, cameras, new LightingManager(), calibration,
            new AngleStrategyFactory(new ModelManager(modelsDir, factory)),
            failures,
            NullLogger<VisionService>.Instance);
    }

    /// <summary>假分割：输入图（全图或 ROI 裁剪）里亮像素的轴对齐包围盒。
    /// 与真实模型一样吃裁剪后的像素，对打才有意义。</summary>
    private static IReadOnlyList<InstanceSegmentation> SegmentBrightBlob(VisionImage image)
    {
        using var mat = VisionImageCv.AsMat(image);
        using var gray = mat.Channels() == 1
            ? mat.Clone()
            : mat.CvtColor(ColorConversionCodes.BGR2GRAY);
        using var binary = new Mat();
        Cv2.Threshold(gray, binary, 127, 255, ThresholdTypes.Binary);
        using var nz = new Mat();
        Cv2.FindNonZero(binary, nz);
        if (nz.Empty())
            return [];

        var box = Cv2.BoundingRect(nz);
        if ((double)box.Width * box.Height < 400)
            return [];

        return
        [
            new InstanceSegmentation(
                new PixelBox(box.X, box.Y, box.Width, box.Height),
                0.95, "part",
                [
                    new ImagePoint(0, 0),
                    new ImagePoint(box.Width, 0),
                    new ImagePoint(box.Width, box.Height),
                    new ImagePoint(0, box.Height),
                ],
                []),
        ];
    }

    /// <summary>PLC 行里的坐标段（配方名/耗时去掉）：有无 ROI 必须逐字相同。</summary>
    private void AssertPlcPoseMatch(VisionResult full, VisionResult roi)
    {
        full.Ok.Should().BeTrue($"全图推理失败: {full.ErrorCode} {full.Message}");
        roi.Ok.Should().BeTrue($"ROI 推理失败: {roi.ErrorCode} {roi.Message}");
        full.Poses.Should().HaveCount(roi.Poses.Count);

        for (var i = 0; i < full.Poses.Count; i++)
        {
            _out.WriteLine(
                $"pose[{i}] 无ROI X={full.Poses[i].X:0.000} Y={full.Poses[i].Y:0.000} Rz={full.Poses[i].AngleDeg:0.000}  |  " +
                $"有ROI X={roi.Poses[i].X:0.000} Y={roi.Poses[i].Y:0.000} Rz={roi.Poses[i].AngleDeg:0.000}");
            roi.Poses[i].X.Should().BeApproximately(full.Poses[i].X, 0.05, $"pose[{i}].X");
            roi.Poses[i].Y.Should().BeApproximately(full.Poses[i].Y, 0.05, $"pose[{i}].Y");
            roi.Poses[i].AngleDeg.Should().BeApproximately(full.Poses[i].AngleDeg, 0.05, $"pose[{i}].Rz");
        }

        var fullLine = PlcXyAngleCsv(full);
        var roiLine = PlcXyAngleCsv(roi);
        _out.WriteLine($"PLC 无ROI {fullLine}");
        _out.WriteLine($"PLC 有ROI {roiLine}");
        fullLine.Should().Be(roiLine);
    }

    private static string PlcXyAngleCsv(VisionResult result) =>
        TcpServerManager.FormatReply(VisionResult.Success("R", result.Poses, elapsedMs: 0));
}
