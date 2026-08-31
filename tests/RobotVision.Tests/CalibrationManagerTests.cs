using System.Text.Json;
using OpenCvSharp;
using RobotVision.Core;
using RobotVision.Core.IO;
using RobotVision.Core.Models;
using RobotVision.Core.Recipe;
using RobotVision.Infrastructure.Calibration;
using Xunit;

namespace RobotVision.Tests;

public class CalibrationManagerTests
{
    [Fact]
    public void PixelToRobot_AppliesAffineAndRotatesAngle()
    {
        var manager = new CalibrationManager();
        // 仿射: 旋转 +90°（图像系→机器人系）+ 平移 (100, 50)
        manager.LoadExtrinsic(new ExtrinsicProfile
        {
            StationId = "st1",
            CameraId = "cam1",
            Affine = [0, -1, 100, 1, 0, 50],
        });

        var pose = manager.PixelToRobot("st1", new PixelPose(10, 20, 0, 1.0));

        Assert.Equal(80, pose.X, 1e-9);   // 0*10 - 1*20 + 100
        Assert.Equal(60, pose.Y, 1e-9);   // 1*10 + 0*20 + 50
        Assert.Equal(90, pose.AngleDeg, 1e-6); // 像素 0° → 机器人 90°
    }

    [Fact]
    public void PixelToRobot_EmptyStation_WithoutPassthrough_Throws()
    {
        // 安全修复验证：stationId 缺省 + 未显式开启直通 → 报错而非静默返回像素坐标
        var manager = new CalibrationManager();
        var ex = Assert.Throws<VisionException>(
            () => manager.PixelToRobot(null, new PixelPose(12.5, -3.25, 45, 1.0)));
        Assert.Equal(VisionErrorCode.NotCalibrated, ex.ErrorCode);
        Assert.Contains("stationId", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void PixelToRobot_UnknownStation_Throws()
    {
        var manager = new CalibrationManager();
        Assert.Throws<VisionException>(
            () => manager.PixelToRobot("nope", new PixelPose(0, 0, 0, 1)));
    }

    [Fact]
    public void Undistort_WithoutIntrinsic_Throws()
    {
        var manager = new CalibrationManager();
        using var image = new Mat(100, 100, MatType.CV_8UC3, Scalar.All(0));

        var ex = Assert.Throws<VisionException>(() => manager.Undistort("cam1", image));
        Assert.Equal(VisionErrorCode.NotCalibrated, ex.ErrorCode);
    }

    [Fact]
    public void Undistort_ResolutionMismatch_Throws()
    {
        var manager = new CalibrationManager();
        manager.LoadIntrinsic(new IntrinsicProfile
        {
            CameraId = "cam1",
            Width = 100,
            Height = 100,
            CameraMatrix = [1000, 0, 50, 0, 1000, 50, 0, 0, 1],
            DistCoeffs = [0, 0, 0, 0, 0],
        });

        using var wrong = new Mat(200, 200, MatType.CV_8UC3, Scalar.All(0));
        Assert.Throws<VisionException>(() => manager.Undistort("cam1", wrong));
    }

    [Fact]
    public void Undistort_IdentityDistortion_ReturnsSameSize()
    {
        var manager = new CalibrationManager();
        manager.LoadIntrinsic(new IntrinsicProfile
        {
            CameraId = "cam1",
            Width = 100,
            Height = 100,
            CameraMatrix = [1000, 0, 50, 0, 1000, 50, 0, 0, 1],
            DistCoeffs = [0, 0, 0, 0, 0],
        });

        using var image = new Mat(100, 100, MatType.CV_8UC3, Scalar.All(128));
        using var undistorted = manager.Undistort("cam1", image);

        Assert.Equal(100, undistorted.Width);
        Assert.Equal(100, undistorted.Height);
        using var gray = new Mat();
        Cv2.CvtColor(undistorted, gray, ColorConversionCodes.BGR2GRAY);
        Assert.Equal(128, gray.At<byte>(50, 50));
        manager.Dispose();
    }

    private static void LoadStation(CalibrationManager manager, double[] affine,
        double cx = 100, double cy = 100, string rotationCameraId = "cam1")
    {
        manager.LoadExtrinsic(new ExtrinsicProfile
        {
            StationId = "st1",
            CameraId = "cam1",
            Affine = affine,
        });
        manager.LoadRotationCenter(new RotationCenterProfile
        {
            StationId = "st1",
            CameraId = rotationCameraId,
            Cx = cx,
            Cy = cy,
            RadiusPx = 50,
        });
    }

    [Fact]
    public void CompensateRotation_EccentricTool_IdentityAffine()
    {
        var manager = new CalibrationManager();
        LoadStation(manager, [1, 0, 0, 0, 1, 0]);

        // 恒等仿射：像素位姿直通。零件 (150,100) 角 90°，轴心 (100,100)
        var pose = manager.PixelToRobot("st1", new PixelPose(150, 100, 90, 1.0));
        var result = manager.CompensateRotation("st1", RotationCompensationMode.EccentricTool, pose);

        Assert.Equal(100, result.X, 6);
        Assert.Equal(50, result.Y, 6);
        Assert.Equal(90, result.AngleDeg, 6);
    }

    [Fact]
    public void CompensateRotation_EccentricTool_RotatedAffine()
    {
        var manager = new CalibrationManager();
        // 仿射: 旋转 +90° + 平移 (100, 50)：轴心像素 (10,20) → 机器人 (80,60)
        LoadStation(manager, [0, -1, 100, 1, 0, 50], cx: 10, cy: 20);

        var pose = manager.PixelToRobot("st1", new PixelPose(60, 20, 0, 1.0));
        // 像素 (60,20) → 机器人 (80,110)，像素角 0° → 机器人角 90°
        Assert.Equal(80, pose.X, 6);
        Assert.Equal(110, pose.Y, 6);

        var result = manager.CompensateRotation("st1", RotationCompensationMode.EccentricTool, pose);
        // 绕机器人轴心 (80,60) 反转 90°：偏移 (0,50) → (50,0)
        Assert.Equal(130, result.X, 6);
        Assert.Equal(60, result.Y, 6);
        Assert.Equal(90, result.AngleDeg, 6);
    }

    [Fact]
    public void CompensateRotation_NoneMode_OrEmptyStation_PassesThrough()
    {
        var manager = new CalibrationManager();
        LoadStation(manager, [1, 0, 0, 0, 1, 0]);
        var pose = new RobotPose(150, 100, 90);

        var none = manager.CompensateRotation("st1", RotationCompensationMode.None, pose);
        var empty = manager.CompensateRotation(null, RotationCompensationMode.EccentricTool, pose);

        Assert.Equal(pose, none);
        Assert.Equal(pose, empty);
    }

    [Fact]
    public void CompensateRotation_WithoutRotationCenter_Throws()
    {
        var manager = new CalibrationManager();
        manager.LoadExtrinsic(new ExtrinsicProfile { StationId = "st1", CameraId = "cam1", Affine = [1, 0, 0, 0, 1, 0] });

        var ex = Assert.Throws<VisionException>(() =>
            manager.CompensateRotation("st1", RotationCompensationMode.EccentricTool, new RobotPose(0, 0, 0)));
        Assert.Equal(VisionErrorCode.NotCalibrated, ex.ErrorCode);
    }

    [Fact]
    public void CompensateRotation_CameraMismatch_Throws()
    {
        var manager = new CalibrationManager();
        LoadStation(manager, [1, 0, 0, 0, 1, 0], rotationCameraId: "cam2");

        Assert.Throws<VisionException>(() =>
            manager.CompensateRotation("st1", RotationCompensationMode.EccentricTool, new RobotPose(0, 0, 0)));
    }

    [Fact]
    public void LoadDirectory_LoadsAllThreeProfileTypes()
    {
        var folder = Path.Combine(Path.GetTempPath(), "rv_calib_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            File.WriteAllText(Path.Combine(folder, "cam1.intrinsic.json"),
                JsonSerializer.Serialize(new IntrinsicProfile
                {
                    CameraId = "cam1",
                    Width = 100,
                    Height = 100,
                    CameraMatrix = [1000, 0, 50, 0, 1000, 50, 0, 0, 1],
                    DistCoeffs = [0, 0, 0, 0, 0],
                }));
            File.WriteAllText(Path.Combine(folder, "st1.extrinsic.json"),
                JsonSerializer.Serialize(new ExtrinsicProfile
                {
                    StationId = "st1",
                    CameraId = "cam1",
                    Affine = [1, 0, 0, 0, 1, 0],
                }));
            File.WriteAllText(Path.Combine(folder, "st1.rotation.json"),
                JsonSerializer.Serialize(new RotationCenterProfile
                {
                    StationId = "st1",
                    CameraId = "cam1",
                    Cx = 10,
                    Cy = 20,
                    RadiusPx = 30,
                }));

            var manager = new CalibrationManager();
            manager.LoadDirectory(folder);

            Assert.Equal(1, manager.IntrinsicCount);
            Assert.Equal(1, manager.ExtrinsicCount);
            Assert.Equal(1, manager.RotationCenterCount);

            // 文件加载后的完整链路：像素位姿 → 外参（恒等）→ 偏心补偿（轴心 (10,20)）
            var result = manager.CompensateRotation("st1", RotationCompensationMode.EccentricTool,
                manager.PixelToRobot("st1", new PixelPose(10, 70, 90, 1.0)));

            Assert.Equal(60, result.X, 6);
            Assert.Equal(20, result.Y, 6);
            Assert.Equal(90, result.AngleDeg, 6);
            manager.Dispose();
        }
        finally
        {
            Directory.Delete(folder, true);
        }
    }

    // ---- 标定模块 13 项改进的回归测试 ----



        [Fact]
    public void PixelToRobot_CameraMismatch_Throws()
    {
        // 外参档案相机与取图相机不一致 → 必须拦截（坐标系错配会让位姿完全错误且无感知）
        var manager = new CalibrationManager();
        manager.LoadExtrinsic(new ExtrinsicProfile
        {
            StationId = "st1",
            CameraId = "cam_a",
            Affine = [1, 0, 0, 0, 1, 0],
        });

        var ex = Assert.Throws<VisionException>(
            () => manager.PixelToRobot("st1", new PixelPose(10, 20, 0, 1.0), cameraId: "cam_b"));
        Assert.Equal(VisionErrorCode.NotCalibrated, ex.ErrorCode);

        // 相机一致时不抛
        var pose = manager.PixelToRobot("st1", new PixelPose(10, 20, 0, 1.0), cameraId: "cam_a");
        Assert.Equal(10, pose.X, 1e-9);
    }



        [Fact]
    public void LoadIntrinsic_InvalidCameraMatrix_Throws()
    {
        // 坏档案（CameraMatrix 不足 9 元素）必须拒绝，而不是越界崩溃
        var manager = new CalibrationManager();
        var ex = Assert.Throws<VisionException>(() => manager.LoadIntrinsic(new IntrinsicProfile
        {
            CameraId = "bad",
            Width = 100,
            Height = 100,
            CameraMatrix = [1, 2, 3],
            DistCoeffs = [0, 0, 0, 0, 0],
        }));
        Assert.Equal(VisionErrorCode.InternalError, ex.ErrorCode);
        Assert.Contains("9 元素", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void LoadIntrinsic_NonFiniteOrNonPositiveFocalLength_Throws()
    {
        // fx=0 / Infinity 的档案会静默生成垃圾映射（Remap 输出错图、机器人坐标错），必须在加载时拒绝
        var manager = new CalibrationManager();

        var zeroFocal = Assert.Throws<VisionException>(() => manager.LoadIntrinsic(new IntrinsicProfile
        {
            CameraId = "zero_fx",
            Width = 100,
            Height = 100,
            CameraMatrix = [0, 0, 50, 0, 1000, 50, 0, 0, 1],
            DistCoeffs = [],
        }));
        Assert.Contains("焦距", zeroFocal.Message, StringComparison.Ordinal);

        var infinite = Assert.Throws<VisionException>(() => manager.LoadIntrinsic(new IntrinsicProfile
        {
            CameraId = "inf_fx",
            Width = 100,
            Height = 100,
            CameraMatrix = [double.PositiveInfinity, 0, 50, 0, 1000, 50, 0, 0, 1],
            DistCoeffs = [],
        }));
        Assert.Contains("非有限值", infinite.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void PixelToRobot_ExtrinsicResolutionMismatchWithIntrinsic_Throws()
    {
        // 换相机/改分辨率后内参重标，旧外参像素坐标系失效——不一致必须拒绝，防静默错位
        var manager = new CalibrationManager();
        manager.LoadIntrinsic(new IntrinsicProfile
        {
            CameraId = "cam1",
            Width = 100,
            Height = 100,
            CameraMatrix = [100, 0, 50, 0, 100, 50, 0, 0, 1],
            DistCoeffs = [0, 0, 0, 0, 0],
        });
        manager.LoadExtrinsic(new ExtrinsicProfile
        {
            StationId = "st1",
            CameraId = "cam1",
            Affine = [1, 0, 0, 0, 1, 0],
            Width = 200, // 与内参 100x100 不一致
            Height = 200,
        });

        var ex = Assert.Throws<VisionException>(
            () => manager.PixelToRobot("st1", new PixelPose(10, 20, 0, 1.0), cameraId: "cam1"));
        Assert.Equal(VisionErrorCode.NotCalibrated, ex.ErrorCode);
        Assert.Contains("不一致", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void PixelToRobot_ExtrinsicWithoutResolution_SkipsConsistencyCheck()
    {
        // 旧版档案（Width/Height 未记录 = 0）向后兼容：不校验，行为与升级前一致
        var manager = new CalibrationManager();
        manager.LoadIntrinsic(new IntrinsicProfile
        {
            CameraId = "cam1",
            Width = 100,
            Height = 100,
            CameraMatrix = [100, 0, 50, 0, 100, 50, 0, 0, 1],
            DistCoeffs = [0, 0, 0, 0, 0],
        });
        manager.LoadExtrinsic(new ExtrinsicProfile
        {
            StationId = "st1",
            CameraId = "cam1",
            Affine = [1, 0, 0, 0, 1, 0],
        });

        var pose = manager.PixelToRobot("st1", new PixelPose(10, 20, 0, 1.0), cameraId: "cam1");
        Assert.Equal(10, pose.X, 1e-9);
    }

    [Fact]
    public void CompensateRotation_RotationCenterResolutionMismatch_Throws()
    {
        var manager = new CalibrationManager();
        manager.LoadIntrinsic(new IntrinsicProfile
        {
            CameraId = "cam1",
            Width = 100,
            Height = 100,
            CameraMatrix = [100, 0, 50, 0, 100, 50, 0, 0, 1],
            DistCoeffs = [0, 0, 0, 0, 0],
        });
        manager.LoadExtrinsic(new ExtrinsicProfile
        {
            StationId = "st1",
            CameraId = "cam1",
            Affine = [1, 0, 0, 0, 1, 0],
        });
        manager.LoadRotationCenter(new RotationCenterProfile
        {
            StationId = "st1",
            CameraId = "cam1",
            Cx = 50,
            Cy = 50,
            RadiusPx = 30,
            Width = 640, // 与内参不一致
            Height = 480,
        });

        Assert.Throws<VisionException>(() =>
            manager.CompensateRotation("st1", RotationCompensationMode.EccentricTool, new RobotPose(0, 0, 0)));
    }

    [Fact]
    public void LoadExtrinsic_PoorQuality_AddsWarning()
    {
        var manager = new CalibrationManager();
        manager.LoadExtrinsic(new ExtrinsicProfile
        {
            StationId = "st_bad",
            CameraId = "cam1",
            Affine = [1, 0, 0, 0, 1, 0],
            MaxResidual = 0.8, // > 0.5 可用上限
        });
        manager.LoadRotationCenter(new RotationCenterProfile
        {
            StationId = "st_bad",
            CameraId = "cam1",
            Cx = 50,
            Cy = 50,
            RadiusPx = 30,
            Rms = 0.8, // > 0.5 可用上限
        });

        Assert.Contains(manager.QualityWarnings, w => w.Contains("外参 st_bad", StringComparison.Ordinal));
        Assert.Contains(manager.QualityWarnings, w => w.Contains("旋转中心 st_bad", StringComparison.Ordinal));
    }

    [Fact]
    public void LoadDirectory_FileNameIdMismatch_AddsWarning()
    {
        var folder = Path.Combine(Path.GetTempPath(), "rv_cal_name_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            // 文件名 st_old，内部 Id st_new：手工重命名场景
            File.WriteAllText(Path.Combine(folder, "st_old.extrinsic.json"),
                JsonSerializer.Serialize(new ExtrinsicProfile
                {
                    StationId = "st_new",
                    CameraId = "cam1",
                    Affine = [1, 0, 0, 0, 1, 0],
                }));

            var manager = new CalibrationManager();
            manager.LoadDirectory(folder);

            Assert.Equal(1, manager.ExtrinsicCount); // 档案仍按 Id 生效
            Assert.Contains(manager.QualityWarnings, w => w.Contains("不一致", StringComparison.Ordinal));
        }
        finally
        {
            Directory.Delete(folder, true);
        }
    }

    [Fact]
    public void NinePointCalibrate_RobotPointsDegenerate_Throws()
    {
        // 机器人坐标全相同（抄错/漏改）时仿射病态，必须拒绝
        var pixel = new Point2f[]
        {
            new(0, 0), new(100, 0), new(200, 0), new(0, 100), new(100, 100),
            new(200, 100), new(0, 200), new(100, 200), new(200, 200),
        };
        var robotSame = Enumerable.Repeat(new Point2f(10f, 10f), 9).ToArray();

        var ex = Assert.Throws<VisionException>(() =>
            NinePointExtrinsicCalibrator.Calibrate("st1", "cam1", pixel, robotSame));
        Assert.Contains("机器人点", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void NinePointCalibrate_RecordsResolutionInProfile()
    {
        var pixel = new Point2f[]
        {
            new(0, 0), new(100, 0), new(200, 0), new(0, 100), new(100, 100),
            new(200, 100), new(0, 200), new(100, 200), new(200, 200),
        };
        var robot = new Point2f[]
        {
            new(0, 0), new(50, 0), new(100, 0), new(0, 50), new(50, 50),
            new(100, 50), new(0, 100), new(50, 100), new(100, 100),
        };

        var profile = NinePointExtrinsicCalibrator.Calibrate("st1", "cam1", pixel, robot, 1280, 960);

        Assert.Equal(1280, profile.Width);
        Assert.Equal(960, profile.Height);
    }

    [Fact]
    public void AtomicFile_OverwritesExistingFileAtomically()
    {
        var folder = Path.Combine(Path.GetTempPath(), "rv_cal_atomic_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            var path = Path.Combine(folder, "cam1.intrinsic.json");
            File.WriteAllText(path, "old content");

            AtomicFile.WriteAllText(path, "{\"a\":1}");

            Assert.Equal("{\"a\":1}", File.ReadAllText(path));
            // 不残留临时文件
            Assert.Empty(Directory.GetFiles(folder, "*.tmp"));
            Assert.Single(Directory.GetFiles(folder));
        }
        finally
        {
            Directory.Delete(folder, true);
        }
    }

    // ---- SCARA 场景：安装模式 / 方向自检 / 工具零位偏角 ----

    [Fact]
    public void ValidateExtrinsic_IllegalMountType_Throws()
    {
        var ex = Assert.Throws<VisionException>(() =>
        {
            var m = new CalibrationManager();
            m.LoadExtrinsic(new ExtrinsicProfile
            {
                StationId = "st1",
                CameraId = "cam1",
                Affine = [1, 0, 0, 0, 1, 0],
                MountType = "Flying",
            });
        });
        Assert.Contains("MountType", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidateExtrinsic_OnArmDefaultFields_Accepted()
    {
        // 旧档案无 MountType → 默认 Fixed；OnArm 带位姿字段合法
        var m = new CalibrationManager();
        m.LoadExtrinsic(new ExtrinsicProfile
        {
            StationId = "st_fixed", CameraId = "cam1", Affine = [1, 0, 0, 0, 1, 0],
        });
        m.LoadExtrinsic(new ExtrinsicProfile
        {
            StationId = "st_arm", CameraId = "cam1", Affine = [1, 0, 0, 0, 1, 0],
            MountType = "OnArm", TeachTcpX = 100.5, TeachTcpY = -20.25, TeachRzDeg = 45,
            CalibrationPlaneZ = 12.5,
        });

        Assert.Equal(2, m.ExtrinsicCount);
    }

    [Fact]
    public void VerifyRotationDirection_ConsistentAngles_Pass()
    {
        var manager = new CalibrationManager();
        // 恒等外参：像素系方向 = 机器人系方向
        manager.LoadExtrinsic(new ExtrinsicProfile
        {
            StationId = "st1", CameraId = "cam1", Affine = [1, 0, 0, 0, 1, 0],
        });

        // 轴心 (100,100)、半径 50：第 4 轴角度递增 → 标记点绕轴心逆时针（数学正方向）
        var rc = new RotationCenterProfile { StationId = "st1", CameraId = "cam1", Cx = 100, Cy = 100, RadiusPx = 50 };
        var angles = new[] { 0.0, 45, 90, 135, 180 };
        var points = angles
            .Select(a => new Point2f(
                100 + 50 * (float)Math.Cos(a * Math.PI / 180.0),
                100 + 50 * (float)Math.Sin(a * Math.PI / 180.0)))
            .ToArray();

        manager.VerifyRotationDirection("st1", rc, points, angles); // 不抛 = 通过
    }

    [Fact]
    public void VerifyRotationDirection_ReversedAngles_Throws()
    {
        // 方向装反场景：第 4 轴角度记录递增，标记点实际顺时针（角度取反）→ 必须拦截
        var manager = new CalibrationManager();
        manager.LoadExtrinsic(new ExtrinsicProfile
        {
            StationId = "st1", CameraId = "cam1", Affine = [1, 0, 0, 0, 1, 0],
        });

        var rc = new RotationCenterProfile { StationId = "st1", CameraId = "cam1", Cx = 100, Cy = 100, RadiusPx = 50 };
        var angles = new[] { 0.0, 45, 90, 135, 180 };
        var points = angles
            .Select(a => new Point2f(
                100 + 50 * (float)Math.Cos(-a * Math.PI / 180.0), // 实际轨迹反向
                100 + 50 * (float)Math.Sin(-a * Math.PI / 180.0)))
            .ToArray();

        var ex = Assert.Throws<VisionException>(
            () => manager.VerifyRotationDirection("st1", rc, points, angles));
        Assert.Contains("方向自检失败", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void VerifyRotationDirection_MirroredExtrinsic_StillDetectsMismatch()
    {
        // 含反射的外参（x 取反）：点与轴心同过映射后方向性比对仍成立
        var manager = new CalibrationManager();
        manager.LoadExtrinsic(new ExtrinsicProfile
        {
            StationId = "st1", CameraId = "cam1", Affine = [-1, 0, 300, 0, 1, 0], // x 翻转
        });

        // 像素系里标记点顺时针（角度取反），机器人系映射后变逆时针 → 与递增角度一致 → 通过
        var rc = new RotationCenterProfile { StationId = "st1", CameraId = "cam1", Cx = 100, Cy = 100, RadiusPx = 50 };
        var angles = new[] { 0.0, 45, 90, 135, 180 };
        var points = angles
            .Select(a => new Point2f(
                100 + 50 * (float)Math.Cos(-a * Math.PI / 180.0),
                100 + 50 * (float)Math.Sin(-a * Math.PI / 180.0)))
            .ToArray();

        manager.VerifyRotationDirection("st1", rc, points, angles); // 不抛 = 通过
    }

    [Fact]
    public void VerifyRotationDirection_WithoutExtrinsic_Throws()
    {
        var manager = new CalibrationManager();
        var rc = new RotationCenterProfile { StationId = "st1", CameraId = "cam1", Cx = 100, Cy = 100, RadiusPx = 50 };

        Assert.Throws<VisionException>(() =>
            manager.VerifyRotationDirection("st1", rc,
                [new(150, 100), new(100, 150), new(50, 100)], [0, 45, 90]));
    }

    [Fact]
    public void VerifyRotationDirection_WithPolynomial_Pass()
    {
        var manager = new CalibrationManager();
        manager.LoadPolynomial(new PolynomialProfile
        {
            StationId = "st1",
            CameraId = "cam1",
            Width = 200,
            Height = 200,
            Order = 2,
            CoefX = [100, 100, 0, 0, 0, 0],
            CoefY = [100, 0, 0, 100, 0, 0],
        });

        var rc = new RotationCenterProfile { StationId = "st1", CameraId = "cam1", Cx = 100, Cy = 100, RadiusPx = 50 };
        var angles = new[] { 0.0, 45, 90, 135, 180 };
        var points = angles
            .Select(a => new Point2f(
                100 + 50 * (float)Math.Cos(a * Math.PI / 180.0),
                100 + 50 * (float)Math.Sin(a * Math.PI / 180.0)))
            .ToArray();

        manager.VerifyRotationDirection("st1", rc, points, angles);
    }

    [Fact]
    public void CompensateRotation_ToolOffsetAppliedFromProfile()
    {
        // 档案带工具零位偏角 δ=10°：输出第 4 轴角 = 零件角 − 10°，位置按 R(δ−θ) 反转
        var manager = new CalibrationManager();
        manager.LoadExtrinsic(new ExtrinsicProfile
        {
            StationId = "st1", CameraId = "cam1", Affine = [1, 0, 0, 0, 1, 0],
        });
        manager.LoadRotationCenter(new RotationCenterProfile
        {
            StationId = "st1", CameraId = "cam1", Cx = 100, Cy = 100, RadiusPx = 50,
            ToolOffsetDeg = 10,
        });

        var result = manager.CompensateRotation("st1", RotationCompensationMode.EccentricTool,
            new RobotPose(150, 100, 90));

        Assert.Equal(80, result.AngleDeg, 6);
        Assert.Equal(108.68, result.X, 2);
        Assert.Equal(50.76, result.Y, 2);
    }

    // ---- TRIGGER 拍照位姿校验（OnArm 工位）----

    private static CalibrationManager PoseCheckManager(string mountType = "OnArm",
        double teachX = 100, double teachY = 200, double teachRz = 45, bool hasTeachPose = true)
    {
        var manager = new CalibrationManager();
        manager.LoadExtrinsic(new ExtrinsicProfile
        {
            StationId = "st1", CameraId = "cam1", Affine = [1, 0, 0, 0, 1, 0],
            MountType = mountType,
            TeachTcpX = teachX, TeachTcpY = teachY, TeachRzDeg = teachRz,
            HasTeachPose = hasTeachPose,
        });
        return manager;
    }

    [Fact]
    public void VerifyClientPose_MatchingPose_Passes()
    {
        var manager = PoseCheckManager();
        manager.VerifyClientPose("st1", new TcpClientPose(100.2, 200.3, 45.1)); // 容差内，不抛
    }

    [Fact]
    public void VerifyClientPose_XyBeyondTolerance_Throws1012()
    {
        var manager = PoseCheckManager();
        var ex = Assert.Throws<VisionException>(() =>
            manager.VerifyClientPose("st1", new TcpClientPose(101.0, 200.0, 45.0))); // 偏 1mm > 0.5
        Assert.Equal(VisionErrorCode.PoseMismatch, ex.ErrorCode);
        Assert.Contains("拍照位姿不一致", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void VerifyClientPose_RzBeyondTolerance_Throws1012()
    {
        var manager = PoseCheckManager();
        Assert.Throws<VisionException>(() =>
            manager.VerifyClientPose("st1", new TcpClientPose(100.0, 200.0, 46.0))); // 偏 1° > 0.5°
    }

    [Fact]
    public void VerifyClientPose_RzWrapAround_MeasuresNormalizedDelta()
    {
        // 标定 179°，上报 -179°：实际差 2°（跨 ±180 边界），不是 358°——超容差必须拦截
        var manager = PoseCheckManager(teachRz: 179);
        Assert.Throws<VisionException>(() =>
            manager.VerifyClientPose("st1", new TcpClientPose(100.0, 200.0, -179.0)));

        // 标定 -180°，上报 180°：同一方向，差 0°——通过
        manager.LoadExtrinsic(new ExtrinsicProfile
        {
            StationId = "st2", CameraId = "cam1", Affine = [1, 0, 0, 0, 1, 0],
            MountType = "OnArm", TeachTcpX = 100, TeachTcpY = 200, TeachRzDeg = -180,
        });
        manager.VerifyClientPose("st2", new TcpClientPose(100.0, 200.0, 180.0));
    }

    [Fact]
    public void VerifyClientPose_FixedMount_SkipsCheck()
    {
        // Fixed（固定机架）档案与拍照位姿无关：位姿随便报都不拦
        var manager = PoseCheckManager(mountType: "Fixed");
        manager.VerifyClientPose("st1", new TcpClientPose(999, -999, 123));
    }

    [Fact]
    public void VerifyClientPose_ProfileWithoutTeachPose_SkipsCheck()
    {
        // OnArm 但档案未记录位姿（旧档案，HasTeachPose=false）：无从比对，放行（档案侧另有提示）
        var manager = new CalibrationManager();
        manager.LoadExtrinsic(new ExtrinsicProfile
        {
            StationId = "st1", CameraId = "cam1", Affine = [1, 0, 0, 0, 1, 0],
            MountType = "OnArm", // TeachTcpX/Y/RzDeg 全 0 且无标志
        });
        manager.VerifyClientPose("st1", new TcpClientPose(55, 66, 77));
    }

    [Fact]
    public void VerifyClientPose_TeachPoseAtOrigin_FlagStillEnforcesCheck()
    {
        // 拍照点恰为坐标原点 (0,0,0) 且 HasTeachPose=true：哨兵值时代会被误判为"未记录"，
        // 显式标志后正常校验——偏离即 1012
        var manager = PoseCheckManager(teachX: 0, teachY: 0, teachRz: 0, hasTeachPose: true);

        manager.VerifyClientPose("st1", new TcpClientPose(0.1, 0.1, 0.1)); // 容差内通过
        Assert.Throws<VisionException>(() =>
            manager.VerifyClientPose("st1", new TcpClientPose(5, 5, 5))); // 偏 7mm：拦截
    }

    [Fact]
    public void VerifyClientPose_OnArmWithoutFlag_SkipsCheck()
    {
        // HasTeachPose=false（旧档案语义）：位姿字段即使非零也不比对
        var manager = PoseCheckManager(hasTeachPose: false);
        manager.VerifyClientPose("st1", new TcpClientPose(999, 999, 999));
    }

    [Fact]
    public void VerifyClientPose_Disabled_SkipsCheck()
    {
        var manager = PoseCheckManager();
        manager.PoseCheckEnabled = false;
        manager.VerifyClientPose("st1", new TcpClientPose(999, 999, 999));
    }

    [Fact]
    public void VerifyClientPose_UnknownStation_SkipsCheck()
    {
        // 无外参档案的工位：外参缺失由 PixelToRobot 统一报 1004，此处不重复拦截
        var manager = new CalibrationManager();
        manager.VerifyClientPose("ghost", new TcpClientPose(1, 2, 3));
    }

    [Fact]
    public void VerifyClientPose_CustomTolerance_Applied()
    {
        var manager = PoseCheckManager();
        manager.PoseXyToleranceMm = 2.0; // 放宽容差
        manager.VerifyClientPose("st1", new TcpClientPose(101.0, 200.0, 45.0)); // 偏 1mm ≤ 2mm：通过
    }

    [Fact]
    public void RequireClientPose_OnArmWithTeachPose_NullPose_Throws1014()
    {
        var manager = PoseCheckManager();
        var ex = Assert.Throws<VisionException>(() => manager.RequireClientPose("st1", null));
        Assert.Equal(VisionErrorCode.PoseRequired, ex.ErrorCode);
        manager.RequireClientPose("st1", new TcpClientPose(100, 200, 45));
    }

    [Fact]
    public void ClientPoseRequired_FixedOrDisabled_IsFalse()
    {
        Assert.False(PoseCheckManager(mountType: "Fixed").ClientPoseRequired("st1"));
        var off = PoseCheckManager();
        off.PoseCheckEnabled = false;
        Assert.False(off.ClientPoseRequired("st1"));
    }

    [Fact]
    public void VerifyClientPose_Translate_IgnoresXy_ChecksRz()
    {
        var manager = new CalibrationManager();
        manager.LoadExtrinsic(new ExtrinsicProfile
        {
            StationId = "st1", CameraId = "cam1", Affine = [1, 0, 0, 0, 1, 0],
            MountType = "OnArm", ComposeMode = "Translate",
            TeachTcpX = 100, TeachTcpY = 200, TeachRzDeg = 45, HasTeachPose = true,
        });
        manager.VerifyClientPose("st1", new TcpClientPose(180, 10, 45.2));
        Assert.Throws<VisionException>(() =>
            manager.VerifyClientPose("st1", new TcpClientPose(180, 10, 47)));
    }

    [Fact]
    public void PixelToRobot_Translate_ShiftsByTcpDelta()
    {
        var manager = new CalibrationManager();
        manager.LoadExtrinsic(new ExtrinsicProfile
        {
            StationId = "st1", CameraId = "cam1", Affine = [1, 0, 0, 0, 1, 0],
            MountType = "OnArm", ComposeMode = "Translate",
            TeachTcpX = 10, TeachTcpY = 20, HasTeachPose = true,
        });
        var pose = manager.PixelToRobot("st1", new PixelPose(1, 2, 0, 1),
            clientPose: new TcpClientPose(15, 24, 0));
        Assert.Equal(6, pose.X, 6); // 1 + (15-10)
        Assert.Equal(6, pose.Y, 6); // 2 + (24-20)
    }

    [Fact]
    public void LoadPolynomialAndExtrinsic_SameStation_AddsQualityWarning()
    {
        var manager = new CalibrationManager();
        manager.LoadExtrinsic(new ExtrinsicProfile
        {
            StationId = "st1", CameraId = "cam1", Affine = [1, 0, 0, 0, 1, 0],
        });
        manager.LoadPolynomial(new PolynomialProfile
        {
            StationId = "st1", CameraId = "cam1", Order = 2,
            CoefX = [0, 1, 0, 0, 0, 0], CoefY = [0, 0, 1, 0, 0, 0],
            Width = 100, Height = 100, PointCount = 10,
        });
        Assert.Contains(manager.QualityWarnings, w => w.Contains("同时存在多项式与外参", StringComparison.Ordinal));
    }

    [Fact]
    public void SavePolynomial_WhenExtrinsicExists_Throws_AndDoesNotWrite()
    {
        var folder = Path.Combine(Path.GetTempPath(), "rv_cal_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            var manager = new CalibrationManager();
            manager.LoadDirectory(folder);
            manager.LoadExtrinsic(new ExtrinsicProfile
            {
                StationId = "st1", CameraId = "cam1", Affine = [1, 0, 0, 0, 1, 0],
            });

            var ex = Assert.Throws<InvalidOperationException>(() => manager.SavePolynomial(new PolynomialProfile
            {
                StationId = "st1", CameraId = "cam1", Order = 2,
                CoefX = [0, 1, 0, 0, 0, 0], CoefY = [0, 0, 1, 0, 0, 0],
                Width = 100, Height = 100, PointCount = 10,
            }));
            Assert.Contains("已有外参", ex.Message, StringComparison.Ordinal);
            Assert.Empty(Directory.GetFiles(folder, "*.polynomial.json"));
        }
        finally
        {
            try { Directory.Delete(folder, true); } catch { /* 测试清理 */ }
        }
    }

    [Fact]
    public void SaveScale_WhenPolynomialExists_Throws()
    {
        var folder = Path.Combine(Path.GetTempPath(), "rv_cal_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            var manager = new CalibrationManager();
            manager.LoadDirectory(folder);
            manager.LoadPolynomial(new PolynomialProfile
            {
                StationId = "st1", CameraId = "cam1", Order = 2,
                CoefX = [0, 1, 0, 0, 0, 0], CoefY = [0, 0, 1, 0, 0, 0],
                Width = 100, Height = 100, PointCount = 10,
            });

            var ex = Assert.Throws<InvalidOperationException>(() => manager.SaveScale(new ScaleProfile
            {
                StationId = "st1", CameraId = "cam1", ScaleX = 0.05, ScaleY = 0.05,
            }));
            Assert.Contains("已有多项式", ex.Message, StringComparison.Ordinal);
        }
        finally
        {
            try { Directory.Delete(folder, true); } catch { /* 测试清理 */ }
        }
    }

    [Fact]
    public void SavePolynomial_SameKindOverwrite_Allowed()
    {
        var folder = Path.Combine(Path.GetTempPath(), "rv_cal_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            var manager = new CalibrationManager();
            manager.LoadDirectory(folder);
            var poly = new PolynomialProfile
            {
                StationId = "st1", CameraId = "cam1", Order = 2,
                CoefX = [0, 1, 0, 0, 0, 0], CoefY = [0, 0, 1, 0, 0, 0],
                Width = 100, Height = 100, PointCount = 10,
            };
            manager.SavePolynomial(poly);
            manager.SavePolynomial(poly with { PointCount = 12 });
            Assert.True(File.Exists(Path.Combine(folder, "st1.polynomial.json")));
        }
        finally
        {
            try { Directory.Delete(folder, true); } catch { /* 测试清理 */ }
        }
    }

    // ---- 第四轮复审修复的回归测试 ----

    [Fact]
    public void LoadIntrinsic_PrincipalPointOutOfImage_Throws()
    {
        // cx/cy 跑到图像外（1280 宽图像 cx=5000）：垃圾档案必须拒绝，不静默建错映射
        var manager = new CalibrationManager();
        var ex = Assert.Throws<VisionException>(() => manager.LoadIntrinsic(new IntrinsicProfile
        {
            CameraId = "bad_pp",
            Width = 1280,
            Height = 960,
            CameraMatrix = [1000, 0, 5000, 0, 1000, 480, 0, 0, 1],
            DistCoeffs = [],
        }));
        Assert.Contains("主点越界", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void LoadIntrinsic_PrincipalPointWithinMargin_Passes()
    {
        // 主点略越界但在 10% 余量内（镜头装配偏心）：接受
        var manager = new CalibrationManager();
        manager.LoadIntrinsic(new IntrinsicProfile
        {
            CameraId = "ok_pp",
            Width = 100,
            Height = 100,
            CameraMatrix = [100, 0, -8, 0, 100, 105, 0, 0, 1],
            DistCoeffs = [0, 0, 0, 0, 0],
        });
        Assert.True(manager.IsCalibrated("ok_pp"));
    }

    [Fact]
    public void NinePointCalibrate_ClusteredPoints_RejectedByScaleCheck()
    {
        // 9 个点挤在 50×50px 角落（1280×960 图）：局部拟合好看但对视场是灾难性外推，必须拒绝
        var clustered = new Point2f[]
        {
            new(0, 0), new(25, 0), new(50, 0), new(0, 25), new(25, 25),
            new(50, 25), new(0, 50), new(25, 50), new(50, 50),
        };
        var robot = new Point2f[]
        {
            new(0, 0), new(10, 0), new(20, 0), new(0, 10), new(10, 10),
            new(20, 10), new(0, 20), new(10, 20), new(20, 20),
        };

        var ex = Assert.Throws<VisionException>(() =>
            NinePointExtrinsicCalibrator.Calibrate("st1", "cam1", clustered, robot, 1280, 960));
        Assert.Contains("分布过小", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void NinePointCalibrate_WithoutImageSize_SkipsScaleCheck()
    {
        // 旧调用不传图像尺寸：尺度检查跳过（绝对退化检查仍然生效）
        var clustered = new Point2f[]
        {
            new(0, 0), new(25, 0), new(50, 0), new(0, 25), new(25, 25),
            new(50, 25), new(0, 50), new(25, 50), new(50, 50),
        };
        var robot = new Point2f[]
        {
            new(0, 0), new(10, 0), new(20, 0), new(0, 10), new(10, 10),
            new(20, 10), new(0, 20), new(10, 20), new(20, 20),
        };

        var profile = NinePointExtrinsicCalibrator.Calibrate("st1", "cam1", clustered, robot);
        Assert.NotEmpty(profile.Affine);
    }

    [Fact]
    public void LoadExtrinsic_LeaveOneOutMaxBeyondLimit_AddsWarning()
    {
        var manager = new CalibrationManager();
        manager.LoadExtrinsic(new ExtrinsicProfile
        {
            StationId = "st_loo", CameraId = "cam1", Affine = [1, 0, 0, 0, 1, 0],
            MaxResidual = 0.05, // 残差合格
            LeaveOneOutMax = 2.5, // 但留一误差大：疑似抄错点
        });

        Assert.Contains(manager.QualityWarnings, w => w.Contains("留一最大误差", StringComparison.Ordinal));
    }

    [Fact]
    public void LoadDirectory_DuplicateId_FirstSortedWins_OtherRejected()
    {
        var folder = Path.Combine(Path.GetTempPath(), "rv_cal_dup_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            // 两个文件内部 Id 都是 st1：文件名排序 a_dup 在前 → a_dup 生效，z_dup 被拒
            File.WriteAllText(Path.Combine(folder, "a_dup.extrinsic.json"),
                JsonSerializer.Serialize(new ExtrinsicProfile
                {
                    StationId = "st1", CameraId = "cam_a", Affine = [1, 0, 0, 0, 1, 0],
                }));
            File.WriteAllText(Path.Combine(folder, "z_dup.extrinsic.json"),
                JsonSerializer.Serialize(new ExtrinsicProfile
                {
                    StationId = "st1", CameraId = "cam_z", Affine = [2, 0, 0, 0, 2, 0],
                }));

            var manager = new CalibrationManager();
            var errors = manager.LoadDirectory(folder);

            Assert.Single(errors);
            Assert.Equal("z_dup.extrinsic.json", errors[0].File);
            Assert.Contains("Id 重复", errors[0].Error, StringComparison.Ordinal);
            // 生效的是排序在先的 a_dup（CameraId=cam_a），结果确定
            var loaded = manager.ExtrinsicProfiles.Single(p => p.StationId == "st1");
            Assert.Equal("cam_a", loaded.CameraId);
        }
        finally
        {
            Directory.Delete(folder, true);
        }
    }

    [Fact]
    public void ComputeToolOffsetDeg_ExactCircle_RecoversOffset()
    {
        // 标记绕轴心方位角 = δ + φ：δ=25°，角度 0/45/90/135 → 实测应还原 25°
        var manager = new CalibrationManager();
        manager.LoadExtrinsic(new ExtrinsicProfile
        {
            StationId = "st1", CameraId = "cam1", Affine = [1, 0, 0, 0, 1, 0],
        });
        var rc = new RotationCenterProfile { StationId = "st1", CameraId = "cam1", Cx = 100, Cy = 100, RadiusPx = 50 };
        var delta = 25.0;
        var angles = new[] { 0.0, 45, 90, 135 };
        var points = angles
            .Select(a =>
            {
                var b = (a + delta) * Math.PI / 180.0;
                return new Point2f(100 + 50 * (float)Math.Cos(b), 100 + 50 * (float)Math.Sin(b));
            })
            .ToArray();

        var (offset, spread) = manager.ComputeToolOffsetDeg("st1", rc, points, angles);

        Assert.Equal(25, offset, 2);
        Assert.True(spread < 0.01, $"无噪声数据离散度应为 0，实际 {spread}");
    }

    [Fact]
    public void ComputeToolOffsetDeg_WrapAround180_MeanNotDistorted()
    {
        // δ=170°：部分 δᵢ 跨 ±180 边界（如 170, 215→-145），圆均值应正确还原 170 而非 ~22
        var manager = new CalibrationManager();
        manager.LoadExtrinsic(new ExtrinsicProfile
        {
            StationId = "st1", CameraId = "cam1", Affine = [1, 0, 0, 0, 1, 0],
        });
        var rc = new RotationCenterProfile { StationId = "st1", CameraId = "cam1", Cx = 100, Cy = 100, RadiusPx = 50 };
        var delta = 170.0;
        var angles = new[] { 0.0, 45, 90 };
        var points = angles
            .Select(a =>
            {
                var b = (a + delta) * Math.PI / 180.0;
                return new Point2f(100 + 50 * (float)Math.Cos(b), 100 + 50 * (float)Math.Sin(b));
            })
            .ToArray();

        var (offset, _) = manager.ComputeToolOffsetDeg("st1", rc, points, angles);

        Assert.Equal(170, offset, 2);
    }

    [Fact]
    public void ComputeToolOffsetDeg_TooFewPairs_Throws()
    {
        var manager = new CalibrationManager();
        manager.LoadExtrinsic(new ExtrinsicProfile
        {
            StationId = "st1", CameraId = "cam1", Affine = [1, 0, 0, 0, 1, 0],
        });
        var rc = new RotationCenterProfile { StationId = "st1", CameraId = "cam1", Cx = 100, Cy = 100, RadiusPx = 50 };

        Assert.Throws<VisionException>(() =>
            manager.ComputeToolOffsetDeg("st1", rc, [new(150, 100)], [0]));
    }

    [Fact]
    public void VerifyRotationDirection_ShuffledAngleOrder_StillPasses()
    {
        // 乱序录入（0,135,45,90）：内部按角度排序后比对，顺序不再是隐含要求
        var manager = new CalibrationManager();
        manager.LoadExtrinsic(new ExtrinsicProfile
        {
            StationId = "st1", CameraId = "cam1", Affine = [1, 0, 0, 0, 1, 0],
        });

        var rc = new RotationCenterProfile { StationId = "st1", CameraId = "cam1", Cx = 100, Cy = 100, RadiusPx = 50 };
        var angles = new[] { 0.0, 135, 45, 90 };
        var points = angles
            .Select(a => new Point2f(
                100 + 50 * (float)Math.Cos(a * Math.PI / 180.0),
                100 + 50 * (float)Math.Sin(a * Math.PI / 180.0)))
            .ToArray();

        manager.VerifyRotationDirection("st1", rc, points, angles); // 不抛 = 通过
    }



        [Fact]
    public void LoadDirectory_IsolatesBadFile_LoadsGoodOnes()
    {
        var folder = Path.Combine(Path.GetTempPath(), "rv_cal_bad_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
        File.WriteAllText(Path.Combine(folder, "cam_good.intrinsic.json"), """
            {
              "CameraId": "cam_good",
              "Width": 64,
              "Height": 64,
              "CameraMatrix": [100,0,32,0,100,32,0,0,1],
              "DistCoeffs": [0,0,0,0,0]
            }
            """);
            File.WriteAllText(Path.Combine(folder, "cam_bad.intrinsic.json"), "{ broken json !!!");

            var manager = new CalibrationManager();
            var errors = manager.LoadDirectory(folder);

            Assert.Single(errors);
            Assert.Equal("cam_bad.intrinsic.json", errors[0].File);
            Assert.True(manager.IsCalibrated("cam_good"));
            Assert.False(manager.IsCalibrated("cam_bad"));
        }
        finally
        {
            Directory.Delete(folder, true);
        }
    }



        [Fact]
    public void SaveAndDeleteIntrinsic_RoundTrip()
    {
        var folder = Path.Combine(Path.GetTempPath(), "rv_cal_save_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            var manager = new CalibrationManager();
            manager.LoadDirectory(folder);

            var profile = new IntrinsicProfile
            {
                CameraId = "cam_x",
                Width = 64,
                Height = 64,
                CameraMatrix = [100, 0, 32, 0, 100, 32, 0, 0, 1],
                DistCoeffs = [0, 0, 0, 0, 0],
            };
            manager.SaveIntrinsic(profile);

            Assert.True(File.Exists(Path.Combine(folder, "cam_x.intrinsic.json")));
            Assert.True(manager.IsCalibrated("cam_x"));

            Assert.True(manager.DeleteIntrinsic("cam_x"));
            Assert.False(File.Exists(Path.Combine(folder, "cam_x.intrinsic.json")));
            Assert.False(manager.IsCalibrated("cam_x"));
        }
        finally
        {
            Directory.Delete(folder, true);
        }
    }



        [Fact]
    public void Assess_QualityThresholds()
    {
        Assert.Equal(CalibrationQuality.Good,
            CalibrationManager.AssessIntrinsic(new IntrinsicProfile { Rms = 0.2 }));
        Assert.Equal(CalibrationQuality.Fair,
            CalibrationManager.AssessIntrinsic(new IntrinsicProfile { Rms = 0.4 }));
        Assert.Equal(CalibrationQuality.Poor,
            CalibrationManager.AssessIntrinsic(new IntrinsicProfile { Rms = 0.6 }));

        Assert.Equal(CalibrationQuality.Poor,
            CalibrationManager.AssessExtrinsic(new ExtrinsicProfile { MaxResidual = 0.6 }));

        // 旋转中心：轴比超限即使 RMS 小也判超标
        Assert.Equal(CalibrationQuality.Poor,
            CalibrationManager.AssessRotation(new RotationCenterProfile { Rms = 0.2, AxisRatio = 1.5, PointCount = 6 }));
        Assert.Equal(CalibrationQuality.Good,
            CalibrationManager.AssessRotation(new RotationCenterProfile { Rms = 0.2, AxisRatio = 1.05, PointCount = 6 }));
    }



        [Fact]
    public void HotReload_DoesNotBreakUndistort()
    {
        // 热加载（写锁替换 + 释放旧 Map）与 Undistort（读锁 Remap）并发安全冒烟
        var manager = new CalibrationManager();
        manager.LoadIntrinsic(new IntrinsicProfile
        {
            CameraId = "cam1",
            Width = 64,
            Height = 64,
            CameraMatrix = [100, 0, 32, 0, 100, 32, 0, 0, 1],
            DistCoeffs = [0, 0, 0, 0, 0],
        });

        try
        {
            using var image = new Mat(64, 64, MatType.CV_8UC3, Scalar.All(128));
            for (var i = 0; i < 20; i++)
            {
                // 交替热加载与去畸变
                using var undistorted = manager.Undistort("cam1", image);
                Assert.Equal(64, undistorted.Width);

                manager.LoadIntrinsic(new IntrinsicProfile
                {
                    CameraId = "cam1",
                    Width = 64,
                    Height = 64,
                    CameraMatrix = [100 + i, 0, 32, 0, 100 + i, 32, 0, 0, 1],
                    DistCoeffs = [0, 0, 0, 0, 0],
                });
            }

            using var final = manager.Undistort("cam1", image);
            Assert.Equal(64, final.Width);
        }
        finally
        {
            manager.Dispose();
        }
    }
}
