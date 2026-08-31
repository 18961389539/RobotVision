using OpenCvSharp;
using RobotVision.Core.Models;

namespace RobotVision.Infrastructure;

/// <summary>
/// VisionImage ↔ Mat 零拷贝桥：Infrastructure 内部用 OpenCV，Core 契约只见 VisionImage。
/// AsMat 只建不拥有数据的头，必须在 VisionImage 存活期内使用。
/// </summary>
public static class VisionImageCv
{
    public static VisionImage FromMat(Mat mat, bool ownsMat)
    {
        ArgumentNullException.ThrowIfNull(mat);
        if (mat.Empty())
            throw new ArgumentException("空图像不能包装为 VisionImage", nameof(mat));

        return new VisionImage(
            mat.Width,
            mat.Height,
            mat.Channels(),
            mat.Data,
            (int)mat.Step(),
            ownsMat ? mat : null);
    }

    /// <summary>取得 Mat 所有权并包装为 VisionImage；调用方不得再 Dispose mat。</summary>
    public static VisionImage Adopt(Mat mat) => FromMat(mat, ownsMat: true);

    /// <summary>不拥有像素的 Mat 头。Dispose 只释放头，不释放 VisionImage 缓冲。</summary>
    public static Mat AsMat(VisionImage image)
    {
        ArgumentNullException.ThrowIfNull(image);
        var type = image.Channels switch
        {
            1 => MatType.CV_8UC1,
            3 => MatType.CV_8UC3,
            4 => MatType.CV_8UC4,
            _ => throw new NotSupportedException($"不支持的通道数: {image.Channels}"),
        };
        return Mat.FromPixelData(image.Height, image.Width, type, image.Data, image.Stride);
    }
}
