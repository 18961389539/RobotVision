using System.Text.Json;
using OpenCvSharp;
using RobotVision.Core;
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
        Assert.Contains("debugPassthrough", ex.Message);
    }

    [Fact]
    public void PixelToRobot_EmptyStation_WithExplicitPassthrough_PassesThroughPixelPose()
    {
        var manager = new CalibrationManager();
        var pose = manager.PixelToRobot(null, new PixelPose(12.5, -3.25, 45, 1.0), allowPassthrough: true);
        Assert.Equal(12.5, pose.X, 1e-9);
        Assert.Equal(-3.25, pose.Y, 1e-9);
        Assert.Equal(45, pose.AngleDeg, 1e-9);
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
        var ex = Assert.Throws<InvalidDataException>(() => manager.LoadIntrinsic(new IntrinsicProfile
        {
            CameraId = "bad",
            Width = 100,
            Height = 100,
            CameraMatrix = [1, 2, 3],
            DistCoeffs = [0, 0, 0, 0, 0],
        }));
        Assert.Contains("9 元素", ex.Message);
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
