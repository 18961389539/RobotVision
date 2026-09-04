using System.Diagnostics.CodeAnalysis;
using OpenCvSharp;

namespace RobotVision.Vision;

public static partial class MaskTemplateMatcher
{
    /// <summary>模板 PNG base64 编码（内嵌配方文件存储）。</summary>
    public static string EncodeTemplatePng(Mat template)
    {
        Cv2.ImEncode(".png", template, out var bytes);
        return Convert.ToBase64String(bytes);
    }

    /// <summary>模板 PNG base64 解码（字节流解码，兼容任意路径/内嵌场景）。</summary>
    public static Mat DecodeTemplatePng(string base64)
    {
        var bytes = Convert.FromBase64String(base64);
        var mat = Cv2.ImDecode(bytes, ImreadModes.Color);
        if (mat.Empty())
            throw new InvalidOperationException("模板图解码失败（数据损坏）");
        return mat;
    }

    /// <summary>单通道 Canny（形状匹配 / 边缘 NCC 共用）。</summary>
    [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope",
        Justification = "Returned Mat ownership transfers to caller.")]
    public static Mat ToCanny8u(Mat src)
    {
        using var gray = new Mat();
        if (src.Channels() == 1)
            src.CopyTo(gray);
        else
            Cv2.CvtColor(src, gray, src.Channels() == 4
                ? ColorConversionCodes.BGRA2GRAY
                : ColorConversionCodes.BGR2GRAY);
        using var blurred = new Mat();
        Cv2.GaussianBlur(gray, blurred, new Size(3, 3), 0);
        var edges = new Mat();
        var (low, high) = AdaptiveCannyThresholds(blurred);
        Cv2.Canny(blurred, edges, low, high);
        TightenOrLoosenEdges(blurred, edges, low, high);
        return edges;
    }

    /// <summary>灰度图 → 三通道 Canny 边缘图（与 Matcher 复用同管线：轻降噪 + 中高阈值）。</summary>
    public static Mat ToEdgeMap(Mat src)
    {
        using var edges = ToCanny8u(src);
        var bgr = new Mat();
        Cv2.CvtColor(edges, bgr, ColorConversionCodes.GRAY2BGR);
        return bgr;
    }

    /// <summary>按灰度均值/σ 与 Otsu 估 Canny 双阈值；弱对比度降低门槛，避免固定 60/160 出空边图。</summary>
    public static (double Low, double High) AdaptiveCannyThresholds(Mat gray8)
    {
        if (gray8.Empty())
            return (60, 160);
        Cv2.MeanStdDev(gray8, out var meanS, out var stdS);
        using var tmp = new Mat();
        var otsu = Cv2.Threshold(gray8, tmp, 0, 255, ThresholdTypes.Binary | ThresholdTypes.Otsu);
        var sigma = stdS.Val0;
        var t = otsu;
        if (sigma < 15)
            t = Math.Max(8, Math.Min(otsu, meanS.Val0) * 0.65);
        var low = Math.Clamp(0.5 * t, 8, 100);
        var high = Math.Clamp(Math.Max(t, low + 20), 24, 220);
        return (low, high);
    }

    private static void TightenOrLoosenEdges(Mat blurred, Mat edges, double low, double high)
    {
        var n = blurred.Rows * blurred.Cols;
        if (n <= 0)
            return;
        var density = Cv2.CountNonZero(edges) / (double)n;
        if (density < 0.003)
        {
            Cv2.Canny(blurred, edges, 2, Math.Max(16, high * 0.25));
            return;
        }

        if (density > 0.22)
            Cv2.Canny(blurred, edges, Math.Min(120, low * 1.35), Math.Min(240, high * 1.35));
    }
}
