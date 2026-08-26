using OpenCvSharp;
using RobotVision.Core;
using RobotVision.Core.Models;
using RobotVision.Infrastructure.Calibration;
using Xunit;

namespace RobotVision.Tests;

/// <summary>
/// 九点外参标定（NinePointExtrinsicCalibrator）测试：
/// - 精确仿射数据可拟合，RMS/最大残差接近零，仿射矩阵元素与真值一致；
/// - 输入防护：少于 3 组、长度不匹配、像素/机器人点共线（退化）均抛 NotCalibrated；
/// - 分布尺度防护：传图像尺寸时，9 点挤在角落小区域被拒绝（防灾难性外推）；
/// - 逐点残差与留一交叉验证：单个抄错的机器人点被放大暴露。
/// </summary>
public class NinePointExtrinsicCalibratorTests
{
    /// <summary>生成 3×3 网格：像素 0..400 步进 200，机器人 = 仿射 rx=px*0.1+5, ry=py*0.2+3。</summary>
    private static (Point2f[] Pixels, Point2f[] Robots) MakeNinePointGrid()
    {
        var pixels = new Point2f[9];
        var robots = new Point2f[9];
        var k = 0;
        for (var row = 0; row < 3; row++)
            for (var col = 0; col < 3; col++)
            {
                var px = col * 200f;
                var py = row * 200f;
                pixels[k] = new Point2f(px, py);
                robots[k] = new Point2f((float)(px * 0.1 + 5), (float)(py * 0.2 + 3));
                k++;
            }
        return (pixels, robots);
    }

    [Fact]
    public void Calibrate_ExactAffine_FitsWithinTolerance()
    {
        var (pixels, robots) = MakeNinePointGrid();

        var profile = NinePointExtrinsicCalibrator.Calibrate("S1", "C1", pixels, robots);

        // 精确仿射数据：整体与逐点残差都应极小（float 舍入量级）
        Assert.True(profile.Rms < 0.01, $"RMS 应接近 0，实测 {profile.Rms}");
        Assert.True(profile.MaxResidual < 0.01, $"最大残差应接近 0，实测 {profile.MaxResidual}");
        Assert.All(profile.PointResiduals!, r => Assert.True(r < 0.01, $"逐点残差 {r} 过大"));
    }

    [Fact]
    public void Calibrate_ExactAffine_AffineMatrixMatchesTruth()
    {
        var (pixels, robots) = MakeNinePointGrid();

        var profile = NinePointExtrinsicCalibrator.Calibrate("S1", "C1", pixels, robots);

        // 行主序 2x3：[a0,a1,a2, a3,a4,a5]，期望 [0.1,0,5, 0,0.2,3]
        var a = profile.Affine;
        Assert.Equal(0.1, a[0], 3);
        Assert.Equal(0.0, a[1], 3);
        Assert.Equal(5.0, a[2], 3);
        Assert.Equal(0.0, a[3], 3);
        Assert.Equal(0.2, a[4], 3);
        Assert.Equal(3.0, a[5], 3);
    }

    [Fact]
    public void Calibrate_FewerThanThreePoints_Throws()
    {
        var pixels = new[] { new Point2f(0, 0), new Point2f(10, 0) };
        var robots = new[] { new Point2f(1, 1), new Point2f(2, 1) };

        var ex = Assert.Throws<VisionException>(() =>
            NinePointExtrinsicCalibrator.Calibrate("S1", "C1", pixels, robots));
        Assert.Equal(VisionErrorCode.NotCalibrated, ex.ErrorCode);
        Assert.Contains("3", ex.Message);
    }

    [Fact]
    public void Calibrate_MismatchedLengths_Throws()
    {
        var pixels = new[] { new Point2f(0, 0), new Point2f(10, 0), new Point2f(20, 0) };
        var robots = new[] { new Point2f(1, 1), new Point2f(2, 1) };

        Assert.Throws<VisionException>(() =>
            NinePointExtrinsicCalibrator.Calibrate("S1", "C1", pixels, robots));
    }

    [Fact]
    public void Calibrate_CollinearPixels_Throws()
    {
        // 像素三点共线：仿射解病态，必须拒绝
        var pixels = new[]
        {
            new Point2f(0, 0), new Point2f(10, 0), new Point2f(20, 0),
            new Point2f(0, 0), new Point2f(10, 0), new Point2f(20, 0), // 前三组即共线，其余占位
            new Point2f(0, 0), new Point2f(10, 0), new Point2f(20, 0),
        };
        var robots = new[]
        {
            new Point2f(0, 0), new Point2f(1, 0), new Point2f(2, 0),
            new Point2f(0, 0), new Point2f(1, 0), new Point2f(2, 0),
            new Point2f(0, 0), new Point2f(1, 0), new Point2f(2, 0),
        };

        var ex = Assert.Throws<VisionException>(() =>
            NinePointExtrinsicCalibrator.Calibrate("S1", "C1", pixels, robots));
        Assert.Equal(VisionErrorCode.NotCalibrated, ex.ErrorCode);
        Assert.Contains("共线", ex.Message);
    }

    [Fact]
    public void Calibrate_CollinearRobots_Throws()
    {
        // 像素分布正常，但机器人坐标全部抄在一条线上：同样病态
        var (pixels, _) = MakeNinePointGrid();
        var robots = pixels.Select(p => new Point2f(p.X * 0.1f, 0f)).ToArray(); // Y 全为 0，共线

        var ex = Assert.Throws<VisionException>(() =>
            NinePointExtrinsicCalibrator.Calibrate("S1", "C1", pixels, robots));
        Assert.Equal(VisionErrorCode.NotCalibrated, ex.ErrorCode);
        Assert.Contains("共线", ex.Message);
    }

    [Fact]
    public void Calibrate_DuplicateRobotPoints_Throws()
    {
        var (pixels, _) = MakeNinePointGrid();
        // 机器人点全部重合：面积退化检测应命中
        var robots = Enumerable.Repeat(new Point2f(100, 100), 9).ToArray();

        Assert.Throws<VisionException>(() =>
            NinePointExtrinsicCalibrator.Calibrate("S1", "C1", pixels, robots));
    }

    [Fact]
    public void Calibrate_TinySpread_WithImageSize_Throws()
    {
        // 网格跨度仅 40px，但图像 2000×2000：最大三角形面积远小于 1% 图像面积 → 拒绝外推
        var pixels = new Point2f[9];
        var robots = new Point2f[9];
        var k = 0;
        for (var row = 0; row < 3; row++)
            for (var col = 0; col < 3; col++)
            {
                pixels[k] = new Point2f(col * 20f, row * 20f);
                robots[k] = new Point2f(col * 2f, row * 2f);
                k++;
            }

        var ex = Assert.Throws<VisionException>(() =>
            NinePointExtrinsicCalibrator.Calibrate("S1", "C1", pixels, robots, width: 2000, height: 2000));
        Assert.Equal(VisionErrorCode.NotCalibrated, ex.ErrorCode);
        Assert.Contains("分布过小", ex.Message);
    }

    [Fact]
    public void Calibrate_WideSpread_WithImageSize_Ok()
    {
        var (pixels, robots) = MakeNinePointGrid();

        // 网格 0..400 覆盖 1000×1000 的 1% 面积下限：最大三角形 200*200/2=20000 ≥ 0.01*1e6=10000 → 通过
        var profile = NinePointExtrinsicCalibrator.Calibrate("S1", "C1", pixels, robots, width: 1000, height: 1000);
        Assert.True(profile.Rms < 0.01);
    }

    [Fact]
    public void Calibrate_RecordsResolutionAndMetadata()
    {
        var (pixels, robots) = MakeNinePointGrid();

        var profile = NinePointExtrinsicCalibrator.Calibrate("S1", "C1", pixels, robots, width: 1280, height: 960);

        Assert.Equal("S1", profile.StationId);
        Assert.Equal("C1", profile.CameraId);
        Assert.Equal(1280, profile.Width);
        Assert.Equal(960, profile.Height);
        Assert.Equal(9, profile.PointResiduals!.Length);
    }

    [Fact]
    public void Calibrate_OutlierRobotPoint_LeaveOneOutExposesIt()
    {
        var (pixels, robots) = MakeNinePointGrid();
        // 第 8 个机器人点抄错：偏移 +100（人为误点，模拟示教抄错）
        robots[8] = new Point2f(robots[8].X + 100, robots[8].Y);

        var profile = NinePointExtrinsicCalibrator.Calibrate("S1", "C1", pixels, robots);

        // 误点被整体拟合吸收一部分，但残差仍显著；留一交叉验证对误点最敏感
        Assert.True(profile.PointResiduals![8] > 5, $"误点残差应显著，实测 {profile.PointResiduals[8]}");
        Assert.True(profile.LeaveOneOutMax > 5, $"留一交叉验证应暴露误点，实测 {profile.LeaveOneOutMax}");
        // 误点的残差应明显大于其余正常点（正常点残差量级 < 0.1）
        Assert.Equal(profile.MaxResidual, profile.PointResiduals[8], 1);
    }

    [Fact]
    public void Calibrate_NoImageSize_SkipsSpreadCheck()
    {
        // 不传图像尺寸（旧调用方）：分布检查跳过，小跨度也能标定（行为向后兼容）
        var pixels = new Point2f[9];
        var robots = new Point2f[9];
        var k = 0;
        for (var row = 0; row < 3; row++)
            for (var col = 0; col < 3; col++)
            {
                pixels[k] = new Point2f(col * 20f, row * 20f);
                robots[k] = new Point2f(col * 2f, row * 2f);
                k++;
            }

        var profile = NinePointExtrinsicCalibrator.Calibrate("S1", "C1", pixels, robots);
        Assert.True(profile.Rms < 0.01);
    }
}
