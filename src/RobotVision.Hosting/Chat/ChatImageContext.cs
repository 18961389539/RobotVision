using System.Globalization;
using OpenCvSharp;

namespace RobotVision.Hosting.Chat;

/// <summary>拍照工具：文本摘要 + 可选 base64 附图（OpenAI 多模态格式）。</summary>
public static class ChatImageContext
{
    public static string Describe(string imagePath)
    {
        try
        {
            using var mat = Cv2.ImRead(imagePath, ImreadModes.Color);
            if (mat.Empty())
                return "图像读取失败。";
            using var gray = new Mat();
            Cv2.CvtColor(mat, gray, ColorConversionCodes.BGR2GRAY);
            var mean = Cv2.Mean(gray);
            return string.Format(
                CultureInfo.InvariantCulture,
                "图像 {0}×{1}px，平均亮度 {2:0}。",
                mat.Width,
                mat.Height,
                mean.Val0);
        }
        catch (Exception ex)
        {
            return "图像摘要失败: " + ex.Message;
        }
    }

    public static string? ToDataUrl(string imagePath, int maxEdgePx)
    {
        if (string.IsNullOrWhiteSpace(imagePath) || !File.Exists(imagePath))
            return null;
        try
        {
            using var mat = Cv2.ImRead(imagePath, ImreadModes.Color);
            if (mat.Empty())
                return null;
            var edge = maxEdgePx > 0 ? maxEdgePx : 768;
            var maxSide = Math.Max(mat.Width, mat.Height);
            if (maxSide > edge)
            {
                var scale = edge / (double)maxSide;
                var resized = new Mat();
                Cv2.Resize(mat, resized, new Size(), scale, scale, InterpolationFlags.Area);
                mat.Dispose();
                using (resized)
                {
                    return EncodeJpegDataUrl(resized);
                }
            }

            return EncodeJpegDataUrl(mat);
        }
        catch
        {
            return null;
        }
    }

    public static string EnrichToolText(string jsonText, string imagePath)
    {
        var brief = Describe(imagePath);
        if (string.IsNullOrWhiteSpace(jsonText))
            return brief;
        return jsonText.TrimEnd() + "\n" + brief;
    }

    private static string EncodeJpegDataUrl(Mat mat)
    {
        Cv2.ImEncode(".jpg", mat, out var bytes, [new ImageEncodingParam(ImwriteFlags.JpegQuality, 85)]);
        return "data:image/jpeg;base64," + Convert.ToBase64String(bytes);
    }
}
