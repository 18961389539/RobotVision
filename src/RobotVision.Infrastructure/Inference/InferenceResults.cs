using RobotVision.Core.Models;

namespace RobotVision.Infrastructure.Inference;

/// <summary>像素轴对齐框（左上角 + 宽高）。推理结果用，不暴露 SKRectI。</summary>
public readonly record struct PixelBox(int X, int Y, int Width, int Height)
{
    public int Left => X;

    public int Top => Y;

    public int Right => X + Width;

    public int Bottom => Y + Height;
}

/// <summary>目标检测结果（框架无关）。</summary>
public sealed record ObjectDetectionResult(PixelBox Box, double Confidence, string Label);

/// <summary>关键点。</summary>
public sealed record KeypointDetection(double X, double Y, double Confidence);

/// <summary>姿态估计结果（框架无关）。</summary>
public sealed record PoseDetectionResult(
    PixelBox Box,
    double Confidence,
    string Label,
    IReadOnlyList<KeypointDetection> KeyPoints);

/// <summary>
/// 实例分割结果（框架无关）。
/// <see cref="ContourLocal"/> 为相对包围盒的局部坐标（与旧 Yolo GetContourPoints 同口径）；
/// <see cref="BitPackedMask"/> 为包围盒尺寸、LSB-first 位打包掩码。
/// </summary>
public sealed record InstanceSegmentation(
    PixelBox Box,
    double Confidence,
    string Label,
    IReadOnlyList<ImagePoint> ContourLocal,
    byte[] BitPackedMask);
