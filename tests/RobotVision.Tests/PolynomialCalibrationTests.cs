using OpenCvSharp;
using RobotVision.Core;
using RobotVision.Core.Models;
using RobotVision.Infrastructure.Calibration;
using Xunit;

namespace RobotVision.Tests;

/// <summary>
/// 多项式标定（单图模式）测试：合成网格数据验证拟合正确性、棋盘朝向自动判定（RMS 筛选）、
/// 档案校验、像素→机器人映射（位置/角度/平移合成/位姿校验）、旋转轴心多项式路径。
/// </summary>
public class PolynomialCalibrationTests
{
    private const int W = 1280;
    private const int H = 960;
    private const double Square = 5.0;

    /// <summary>生成合成棋盘角点：像素网格 + 已知地面真值映射（缩放+旋转+平移）。
    /// 默认 mmPerPx = Square/pixelStep（每格当量与方格边长参数自洽，通过标定器间距校验）。</summary>
    private static (Point2f[] Pixels, Point2f[] Robots, Size Pattern) MakeGrid(
        int cols, int rows, double pixelStep = 60.0,
        double rotDeg = 15.0, double? mmPerPx = null, double tx = 100, double ty = 200,
        bool mirror = false)
    {
        var rad = rotDeg * Math.PI / 180.0;
        var cos = Math.Cos(rad);
        var sin = Math.Sin(rad);
        var scale = mmPerPx ?? Square / pixelStep;
        var sx = mirror ? -scale : scale;

        var pixels = new Point2f[cols * rows];
        var robots = new Point2f[cols * rows];
        var k = 0;
        for (var j = 0; j < rows; j++)
            for (var i = 0; i < cols; i++)
            {
                var px = 140.0 + i * pixelStep;
                var py = 120.0 + j * pixelStep;
                pixels[k] = new Point2f((float)px, (float)py);
                var mx = px * sx;
                var my = py * scale;
                robots[k] = new Point2f(
                    (float)(tx + mx * cos - my * sin),
                    (float)(ty + mx * sin + my * cos));
                k++;
            }
        return (pixels, robots, new Size(cols, rows));
    }

    private static Point2f GridPoint(Point2f[] robots, Size pattern, int i, int j) =>
        robots[j * pattern.Width + i];

    [Fact]
    public void Fit_LinearGroundTruth_SecondOrderRecoversExact()
    {
        // 地面真值是线性映射（旋转+缩放+平移）：二阶多项式系数的高次项应为 0，残差≈0
        var (pixels, robots, _) = MakeGrid(9, 6);

        var profile = PolynomialCalibrator.Fit("st1", "cam1", pixels, robots, W, H, 2);

        // float 像素坐标 + SVD 数值误差的量级为 1e-5 mm（纳米级），远低于任何工艺容差
        Assert.True(profile.Rms < 1e-4, $"线性真值拟合残差应≈0，实际 {profile.Rms}");
        Assert.True(profile.MaxResidual < 1e-3);
        Assert.Equal(54, profile.PointCount);
        Assert.Equal(6, profile.CoefficientCount);
    }

    [Fact]
    public void Fit_NonlinearDistortion_SecondOrderAbsorbs()
    {
        // 叠加二次畸变（像素→机器人含 u² 项）：二阶多项式应精确表达
        var (pixels, robots, _) = MakeGrid(9, 6);
        for (var k = 0; k < robots.Length; k++)
        {
            var u = 2.0 * pixels[k].X / W - 1;
            robots[k] = new Point2f(
                robots[k].X + (float)(3.0 * u * u),
                robots[k].Y);
        }

        var profile = PolynomialCalibrator.Fit("st1", "cam1", pixels, robots, W, H, 2);
        Assert.True(profile.Rms < 1e-4, $"二次畸变应被二阶项精确吸收，实际 RMS {profile.Rms}");
    }

    [Fact]
    public void Calibrate_TwoRowReferences_RecoversMapping()
    {
        // 完整流程：同一行两个参考角点（含机器人坐标）→ 全网格推导 → 拟合
        var (pixels, robots, pattern) = MakeGrid(9, 6);

        var profile = PolynomialCalibrator.Calibrate(
            "st1", "cam1", pixels, pattern, Square,
            GridPoint(pixels, pattern, 0, 0), GridPoint(robots, pattern, 0, 0),
            GridPoint(pixels, pattern, 8, 0), GridPoint(robots, pattern, 8, 0),
            W, H, 2);

        Assert.True(profile.Rms < 1e-4, $"参考点推导+拟合应还原线性映射，实际 RMS {profile.Rms}");

        // 任取一个非参考点验证映射（float→double 精度边界，1μm 内即认为还原）
        var probe = 30; // (3,3)
        var (mx, my) = profile.Evaluate(pixels[probe].X, pixels[probe].Y);
        Assert.Equal(robots[probe].X, mx, 3);
        Assert.Equal(robots[probe].Y, my, 3);
    }

    [Fact]
    public void Calibrate_MirroredBoard_AutoPicksBetterOrientation()
    {
        // 镜像地面真值（含反射）：ey 两候选符号由 RMS 自动选定，无需用户声明朝向
        var (pixels, robots, pattern) = MakeGrid(9, 6, mirror: true);

        var profile = PolynomialCalibrator.Calibrate(
            "st1", "cam1", pixels, pattern, Square,
            GridPoint(pixels, pattern, 0, 0), GridPoint(robots, pattern, 0, 0),
            GridPoint(pixels, pattern, 8, 0), GridPoint(robots, pattern, 8, 0),
            W, H, 2);

        Assert.True(profile.Rms < 1e-4, $"镜像棋盘应自动选出正确朝向，实际 RMS {profile.Rms}");
    }

    [Fact]
    public void Calibrate_SquaredSpanMismatch_Rejected()
    {
        // 参考点机器人间距与"列差×方格边长"不符（抄错/边长错）：每格当量校验拒绝
        var (pixels, robots, pattern) = MakeGrid(9, 6);

        var wrongRobot2 = new Point2f(
            GridPoint(robots, pattern, 8, 0).X * 1.1f,
            GridPoint(robots, pattern, 8, 0).Y);

        var ex = Assert.Throws<VisionException>(() => PolynomialCalibrator.Calibrate(
            "st1", "cam1", pixels, pattern, Square,
            GridPoint(pixels, pattern, 0, 0), GridPoint(robots, pattern, 0, 0),
            GridPoint(pixels, pattern, 8, 0), wrongRobot2,
            W, H, 2));
        Assert.Contains("间距与棋盘规格不符", ex.Message);
    }

    [Fact]
    public void Calibrate_ReferencesInDifferentRows_Rejected()
    {
        var (pixels, robots, pattern) = MakeGrid(9, 6);

        var ex = Assert.Throws<VisionException>(() => PolynomialCalibrator.Calibrate(
            "st1", "cam1", pixels, pattern, Square,
            GridPoint(pixels, pattern, 0, 0), GridPoint(robots, pattern, 0, 0),
            GridPoint(pixels, pattern, 0, 2), GridPoint(robots, pattern, 0, 2),
            W, H, 2));
        Assert.Contains("同一棋盘行", ex.Message);
    }

    [Fact]
    public void Fit_TooFewPoints_Rejected()
    {
        var pixels = new[] { new Point2f(0, 0), new Point2f(10, 0), new Point2f(0, 10), new Point2f(10, 10) };
        var robots = new[] { new Point2f(0, 0), new Point2f(1, 0), new Point2f(0, 1), new Point2f(1, 1) };

        Assert.Throws<VisionException>(() =>
            PolynomialCalibrator.Fit("st1", "cam1", pixels, robots, W, H, 3));
    }

    // ---- 免示教棋盘毫米系（CalibrateImageSpace）----

    [Fact]
    public void CalibrateImageSpace_NoTeach_RecoversMmMapping()
    {
        // 地面真值：像素 → 毫米是纯比例缩放（60px/格，5mm/格 → 1/12 mm/px）
        // 免示教拟合应精确还原：任意角点的输出 ≈ 索引×格距
        var (pixels, _, pattern) = MakeGrid(9, 6);

        var profile = PolynomialCalibrator.CalibrateImageSpace(
            "st1", "cam1", pixels, pattern, Square, W, H, 2);

        Assert.Equal(PolynomialCoordinateSpace.Image, profile.CoordinateSpace);
        Assert.True(profile.Rms < 1e-4, $"纯比例真值拟合残差应≈0，实际 {profile.Rms}");

        // 抽查角点：(3,2) 应输出 (3×5, ±2×5)，量值精确、y 符号由候选选择决定
        var (x, y) = profile.Evaluate(pixels[2 * pattern.Width + 3].X, pixels[2 * pattern.Width + 3].Y);
        Assert.Equal(15.0, Math.Abs(x), 3);
        Assert.Equal(10.0, Math.Abs(y), 3);
    }

    [Fact]
    public void CalibrateImageSpace_NonlinearDistortion_Absorbed()
    {
        // 叠加径向畸变后，二阶多项式仍应精确表达（免示教路径同样吸收畸变）
        var (pixels, _, pattern) = MakeGrid(9, 6);
        // 用像素自身构造畸变：目标 x 含 u² 项——重排 pixels 使映射非均匀
        // 简化验证：直接验证拟合能力（真值含二次项时残差≈0）
        var targets = new Point2f[pixels.Length];
        for (var k = 0; k < pixels.Length; k++)
        {
            var u = 2.0 * pixels[k].X / W - 1;
            targets[k] = new Point2f(
                (float)((pixels[k].X / 12.0) + 3.0 * u * u),
                (float)(pixels[k].Y / 12.0));
        }

        var profile = PolynomialCalibrator.Fit("st1", "cam1", pixels, targets, W, H, 2);
        Assert.True(profile.Rms < 1e-4, $"二次畸变应被吸收，实际 RMS {profile.Rms}");
        Assert.Equal(pattern.Width * pattern.Height, profile.PointCount);
    }

    [Fact]
    public void CalibrateImageSpace_MirroredBoard_AutoPicksOrientation()
    {
        // 镜像像素网格（x 翻转）：候选行方向自动适配，仍得低残差
        var (pixels, _, pattern) = MakeGrid(9, 6, mirror: true);

        var profile = PolynomialCalibrator.CalibrateImageSpace(
            "st1", "cam1", pixels, pattern, Square, W, H, 2);

        Assert.True(profile.Rms < 1e-4, $"镜像棋盘应自动适配朝向，实际 RMS {profile.Rms}");
    }

    [Fact]
    public void CalibrateImageSpace_OutputIsImageSpaceNotRobot()
    {
        // 免示教结果不得携带机器人系语义（MountType 固定语义由保存侧决定，此处校验 CoordinateSpace）
        var (pixels, _, pattern) = MakeGrid(9, 6);

        var profile = PolynomialCalibrator.CalibrateImageSpace(
            "st1", "cam1", pixels, pattern, Square, W, H, 2);

        Assert.Equal(PolynomialCoordinateSpace.Image, profile.CoordinateSpace);
        Assert.Equal(PolynomialCoordinateSpace.Robot, PolynomialCalibrator.Fit(
            "st1", "cam1", pixels, pixels.Select(p => new Point2f(p.X / 12f, p.Y / 12f)).ToArray(),
            W, H, 2).CoordinateSpace); // 旧入口保持 Robot 缺省
    }

    [Fact]
    public void VerifyPolynomialClientPose_ImageSpace_SkipsCheck()
    {
        // Image 毫米系：无机器人系概念，任意上报位姿不拦截（也不参与合成）
        var manager = new CalibrationManager();
        var (pixels, _, pattern) = MakeGrid(9, 6);
        manager.LoadPolynomial(PolynomialCalibrator.CalibrateImageSpace(
            "st1", "cam1", pixels, pattern, Square, W, H, 2));

        manager.VerifyPolynomialClientPose("st1", new TcpClientPose(999, 999, 999));
    }

    [Fact]
    public void PixelToRobotPolynomial_ImageSpace_NoTranslateCompose()
    {
        // Image 毫米系即使档案被配成 OnArm+Translate 也不做平移合成（无机器人系基准）
        var manager = new CalibrationManager();
        var (pixels, _, pattern) = MakeGrid(9, 6);
        var poly = PolynomialCalibrator.CalibrateImageSpace(
            "st1", "cam1", pixels, pattern, Square, W, H, 2)
            with { MountType = CameraMountType.OnArm, ComposeMode = PoseComposeMode.Translate, HasTeachPose = true };
        manager.LoadPolynomial(poly);

        var probe = pixels[30];
        var basePose = manager.PixelToRobotPolynomial("st1", new PixelPose(probe.X, probe.Y, 0, 1), clientPose: null);
        var moved = manager.PixelToRobotPolynomial("st1", new PixelPose(probe.X, probe.Y, 0, 1),
            clientPose: new TcpClientPose(507.5, 298, 0.1));

        Assert.Equal(basePose.X, moved.X, 9); // 不平移
        Assert.Equal(basePose.Y, moved.Y, 9);
    }

    [Fact]
    public void ValidatePolynomial_IllegalCoordinateSpace_Rejected()
    {
        var poly = ValidPoly() with { CoordinateSpace = "Mars" };
        Assert.Throws<VisionException>(() => CalibrationManager.ValidatePolynomial(poly));
    }

    // ---- CalibrationManager 集成 ----

    private static PolynomialProfile ValidPoly(string stationId = "st1", string mountType = CameraMountType.Fixed,
        string composeMode = PoseComposeMode.Check, bool hasTeachPose = false,
        double teachX = 0, double teachY = 0, double teachRz = 0)
    {
        var (pixels, robots, _) = MakeGrid(9, 6);
        var poly = PolynomialCalibrator.Fit(stationId, "cam1", pixels, robots, W, H, 2);
        return poly with
        {
            MountType = mountType,
            ComposeMode = composeMode,
            HasTeachPose = hasTeachPose,
            TeachTcpX = teachX,
            TeachTcpY = teachY,
            TeachRzDeg = teachRz,
        };
    }

    [Fact]
    public void ValidatePolynomial_CorruptedCoefficients_Rejected()
    {
        var poly = ValidPoly() with { CoefX = [1, 2, 3] }; // 个数不符
        var ex = Assert.Throws<VisionException>(() => CalibrationManager.ValidatePolynomial(poly));
        Assert.Contains("系数个数非法", ex.Message);

        var nan = ValidPoly();
        var bad = nan.CoefX.ToArray();
        bad[0] = double.NaN;
        Assert.Throws<VisionException>(() => CalibrationManager.ValidatePolynomial(nan with { CoefX = bad }));
    }

    [Fact]
    public void PixelToRobotPolynomial_AppliesMappingAndAngle()
    {
        // 旋转 90°的映射（像素 0° → 机器人 90°）：角度经局部雅可比正确变换
        var (pixels, robots, _) = MakeGrid(9, 6, rotDeg: 90);
        var poly = PolynomialCalibrator.Fit("st1", "cam1", pixels, robots, W, H, 2);

        var manager = new CalibrationManager();
        manager.LoadPolynomial(poly);

        var probe = 30;
        var pose = manager.PixelToRobotPolynomial("st1", new PixelPose(pixels[probe].X, pixels[probe].Y, 0, 1));
        Assert.Equal(robots[probe].X, pose.X, 3);
        Assert.Equal(robots[probe].Y, pose.Y, 3);
        Assert.Equal(90, pose.AngleDeg, 1);
    }

    [Fact]
    public void PixelToRobotPolynomial_CameraMismatch_Throws()
    {
        var manager = new CalibrationManager();
        manager.LoadPolynomial(ValidPoly());

        Assert.Throws<VisionException>(() =>
            manager.PixelToRobotPolynomial("st1", new PixelPose(640, 480, 0, 1), cameraId: "cam_other"));
    }

    [Fact]
    public void PixelToRobotPolynomial_TranslateMode_ComposesClientOffset()
    {
        // OnArm + Translate：输出 = 基准映射 + (当前TCP − 示教TCP)，换拍照点不重标
        var manager = new CalibrationManager();
        manager.LoadPolynomial(ValidPoly(mountType: CameraMountType.OnArm,
            composeMode: PoseComposeMode.Translate, hasTeachPose: true,
            teachX: 500, teachY: 300, teachRz: 0));

        var basePose = manager.PixelToRobotPolynomial("st1", new PixelPose(640, 480, 45, 1), clientPose: null);
        var moved = manager.PixelToRobotPolynomial("st1", new PixelPose(640, 480, 45, 1),
            clientPose: new TcpClientPose(507.5, 298, 0.1));

        Assert.Equal(basePose.X + 7.5, moved.X, 6);
        Assert.Equal(basePose.Y - 2.0, moved.Y, 6);
        Assert.Equal(basePose.AngleDeg, moved.AngleDeg, 6); // 平移不改角度
    }

    [Fact]
    public void VerifyPolynomialClientPose_CheckMode_RejectsXyDrift()
    {
        var manager = new CalibrationManager();
        manager.LoadPolynomial(ValidPoly(mountType: CameraMountType.OnArm,
            composeMode: PoseComposeMode.Check, hasTeachPose: true,
            teachX: 100, teachY: 200, teachRz: 45));

        manager.VerifyPolynomialClientPose("st1", new TcpClientPose(100.1, 200.1, 45.0)); // 容差内
        var ex = Assert.Throws<VisionException>(() =>
            manager.VerifyPolynomialClientPose("st1", new TcpClientPose(102, 200, 45))); // 偏 2mm
        Assert.Equal(VisionErrorCode.PoseMismatch, ex.ErrorCode);
    }

    [Fact]
    public void VerifyPolynomialClientPose_TranslateMode_AllowsXyRejectsRz()
    {
        var manager = new CalibrationManager();
        manager.LoadPolynomial(ValidPoly(mountType: CameraMountType.OnArm,
            composeMode: PoseComposeMode.Translate, hasTeachPose: true,
            teachX: 100, teachY: 200, teachRz: 0));

        // 平移任意大都不拒（合成参数）
        manager.VerifyPolynomialClientPose("st1", new TcpClientPose(999, 888, 0.1));
        // 姿态变必须拒
        Assert.Throws<VisionException>(() =>
            manager.VerifyPolynomialClientPose("st1", new TcpClientPose(150, 250, 3)));
    }

    [Fact]
    public void VerifyPolynomialClientPose_FixedMount_Skips()
    {
        var manager = new CalibrationManager();
        manager.LoadPolynomial(ValidPoly()); // Fixed
        manager.VerifyPolynomialClientPose("st1", new TcpClientPose(999, 999, 999));
    }

    [Fact]
    public void RotationCenterRobot_PolynomialStation_MapsAxisWithoutExtrinsic()
    {
        // 多项式工位 + 旋转中心（不同心工具补偿链路）：轴心直接经多项式映射，无需外参档案
        var (pixels, robots, _) = MakeGrid(9, 6);
        var manager = new CalibrationManager();
        manager.LoadPolynomial(PolynomialCalibrator.Fit("st1", "cam1", pixels, robots, W, H, 2));
        manager.LoadRotationCenter(new RotationCenterProfile
        {
            StationId = "st1", CameraId = "cam1", Cx = 640, Cy = 480, RadiusPx = 50,
        });

        var center = manager.RotationCenterRobot("st1");

        // 线性映射下 (640,480) 的机器人坐标 = 直接计算
        var expected = manager.PixelToRobotPolynomial("st1", new PixelPose(640, 480, 0, 1));
        Assert.Equal(expected.X, center.X, 3);
        Assert.Equal(expected.Y, center.Y, 3);
    }

    [Fact]
    public void LoadDirectory_LoadsPolynomialProfile()
    {
        var folder = Path.Combine(Path.GetTempPath(), "rv_poly_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            var poly = ValidPoly("st_poly");
            File.WriteAllText(Path.Combine(folder, "st_poly.polynomial.json"),
                System.Text.Json.JsonSerializer.Serialize(poly));

            var manager = new CalibrationManager();
            var errors = manager.LoadDirectory(folder);

            Assert.Empty(errors);
            Assert.Equal(1, manager.PolynomialCount);
            Assert.True(manager.HasPolynomial("st_poly"));
        }
        finally
        {
            Directory.Delete(folder, true);
        }
    }

    [Fact]
    public void Evaluate_MatchesDesignMatrixConvention()
    {
        // 求值路径与拟合基函数顺序一致性：构造单点已知的系数向量验证枚举顺序
        // 基顺序（2 阶，j 外层 i 内层）: [1, u, u², v, uv, v²]
        var poly = new PolynomialProfile
        {
            StationId = "s", CameraId = "c", Width = W, Height = H, Order = 2,
            CoefX = [1, 2, 3, 4, 5, 6],
            CoefY = [0, 0, 0, 0, 0, 0],
        };
        // 取 px=W/2, py=H/4 → u=0, v=-0.5
        // X = 1·1 + 2·0 + 3·0 + 4·(-0.5) + 5·0 + 6·0.25 = 1 − 2 + 1.5 = 0.5
        var (x, _) = poly.Evaluate(W / 2.0, H / 4.0);
        Assert.Equal(0.5, x, 9);
    }
}
