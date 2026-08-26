using BenchmarkDotNet.Attributes;
using OpenCvSharp;
using RobotVision.Core.Geometry;
using RobotVision.Core.Models;
using RobotVision.Infrastructure.Geometry;

namespace RobotVision.Benchmarks;

[MemoryDiagnoser]
public class AngleGeometryBenchmarks
{
    private ImagePoint _a = new(123.456, 789.012);
    private ImagePoint _b = new(456.789, 123.456);

    [Benchmark]
    public double NormalizeDeg() => AngleGeometry.NormalizeDeg(123456.789);

    [Benchmark]
    public double NormalizeSignedDeg() => AngleGeometry.NormalizeSignedDeg(-54321.123);

    [Benchmark]
    public (ImagePoint Center, double AngleDeg) FromTwoPoints() => AngleGeometry.FromTwoPoints(_a, _b);

    private Point2f[] _contour = BuildContour();

    [Benchmark]
    public (ImagePoint Center, double AngleDeg) LongAxisFromMinAreaRect() =>
        MinAreaRectGeometry.LongAxis(_contour);

    private static Point2f[] BuildContour()
    {
        var points = new Point2f[16];
        var center = new Point2f(640, 480);
        for (var i = 0; i < 16; i++)
        {
            var angle = i * (Math.PI * 2 / 16);
            var rx = 200.0;
            var ry = 80.0;
            points[i] = new Point2f(
                (float)(center.X + Math.Cos(angle) * rx),
                (float)(center.Y + Math.Sin(angle) * ry));
        }
        return points;
    }
}
