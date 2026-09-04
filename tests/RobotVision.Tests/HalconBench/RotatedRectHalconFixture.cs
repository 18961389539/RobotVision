using OpenCvSharp;
using RobotVision.Vision;

namespace RobotVision.Tests.HalconBench;

internal sealed record RotatedRectHalconFixture(
    string Id,
    string Scenario,
    double TrueDeg,
    double SeedDeg,
    RectFitOptions Options,
    Func<Mat> CreateImage,
    Func<Point2f[]> CreateContour);
