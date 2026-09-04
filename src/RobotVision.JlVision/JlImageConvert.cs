using JLVisionLib;
using OpenCvSharp;
using RobotVision.Core.Geometry;

namespace RobotVision.JlVision;

/// <summary>OpenCV Mat ↔ JlImage，以及 HALCON 行/列/弧度角与 RobotVision 像素角互转。</summary>
public static class JlImageConvert
{
    /// <summary>复制灰度图到 JLVision（doCopy=true，与 OpenCV 缓冲脱钩）。</summary>
    public static JlImage FromGrayMat(Mat src)
    {
        ArgumentNullException.ThrowIfNull(src);
        if (src.Empty())
            throw new ArgumentException("空图不能转 JlImage", nameof(src));

        using var gray = ToGray(src);
        using var continuous = gray.IsContinuous() ? gray : gray.Clone();
        var owned = !gray.IsContinuous();
        try
        {
            var img = new JlImage();
            img.GenImage1Rect(
                continuous.Data,
                continuous.Width,
                continuous.Height,
                (int)continuous.Step(),
                8,
                8,
                "true",
                IntPtr.Zero);
            return img;
        }
        finally
        {
            if (owned)
                continuous.Dispose();
        }
    }

    public static Mat ToGray(Mat src)
    {
        if (src.Channels() == 1)
            return src.Clone();
        var gray = new Mat();
        Cv2.CvtColor(src, gray, src.Channels() == 4
            ? ColorConversionCodes.BGRA2GRAY
            : ColorConversionCodes.BGR2GRAY);
        return gray;
    }

    /// <summary>HALCON phi（弧度，相对列轴）→ RobotVision 有向角（度）。</summary>
    public static double PhiToDeg(double phiRad) =>
        AngleGeometry.NormalizeSignedDeg(phiRad * 180.0 / Math.PI);

    public static double DegToPhi(double deg) =>
        AngleGeometry.NormalizeSignedDeg(deg) * Math.PI / 180.0;

    public static bool TryFirst(JlTuple? tuple, out double value)
    {
        value = double.NaN;
        if (tuple is null || tuple.Length < 1)
            return false;
        value = tuple[0].D;
        return double.IsFinite(value);
    }
}
