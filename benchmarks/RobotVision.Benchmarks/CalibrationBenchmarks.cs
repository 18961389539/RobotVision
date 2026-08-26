using BenchmarkDotNet.Attributes;
using RobotVision.Core.Geometry;
using RobotVision.Core.Models;
using RobotVision.Core.Recipe;
using RobotVision.Infrastructure.Calibration;

namespace RobotVision.Benchmarks;

[MemoryDiagnoser]
public class CalibrationBenchmarks
{
    private CalibrationManager _calibration = new();
    private PixelPose _pixel = new(320.5, 240.5, 30.0, 0.95);

    [GlobalSetup]
    public void Setup()
    {
        _calibration.LoadExtrinsic(new ExtrinsicProfile
        {
            StationId = "st1",
            CameraId = "cam1",
            // 简单单位仿射
            Affine = [1.5, 0.2, 100, -0.1, 1.3, 50],
            Rms = 0.05,
            MaxResidual = 0.08,
            Width = 1280,
            Height = 960,
        });
    }

    [Benchmark]
    public RobotPose PixelToRobot() =>
        _calibration.PixelToRobot("st1", _pixel, "cam1");

    [Benchmark]
    public RobotPose CompensateRotation_None() =>
        _calibration.CompensateRotation("st1", RotationCompensationMode.None,
            new RobotPose(100, 200, 30));

    [Benchmark]
    public (double X, double Y) RotationCenterCompensate_Rotate() =>
        RotationCenterCompensation.Rotate(100, 200, 640, 480, 30);

    [Benchmark]
    public RobotPose RotationCenterCompensate_Apply() =>
        RotationCenterCompensation.Apply(new RobotPose(100, 200, 30), 640, 480, 5);
}
