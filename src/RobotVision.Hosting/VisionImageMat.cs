using OpenCvSharp;
using RobotVision.Core.Models;

namespace RobotVision.Hosting;

/// <summary>VisionImage ↔ Mat 桥（WPF 不直接依赖 Infrastructure.VisionImageCv）。</summary>
public static class VisionImageMat
{
    public static Mat AsMat(VisionImage image) => Infrastructure.VisionImageCv.AsMat(image);

    public static VisionImage FromMat(Mat mat, bool ownsMat) =>
        Infrastructure.VisionImageCv.FromMat(mat, ownsMat);

    public static VisionImage Adopt(Mat mat) => Infrastructure.VisionImageCv.Adopt(mat);
}
