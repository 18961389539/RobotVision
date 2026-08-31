using OpenCvSharp;
using SkiaSharp;

namespace RobotVision.Infrastructure.Inference;

/// <summary>
/// OpenCvSharp Mat 与 SkiaSharp SKBitmap 的转换（YoloDotNet 输入为 SKBitmap）。
/// </summary>
public static unsafe class MatSkiaConverter
{
    public static SKBitmap ToSKBitmap(Mat bgr)
    {
        var info = new SKImageInfo(bgr.Width, bgr.Height, SKColorType.Bgra8888, SKAlphaType.Opaque);
        var bitmap = new SKBitmap(info);
        var dst = (byte*)bitmap.GetPixels();
        var dstStep = (nint)bitmap.RowBytes;
        var src = (byte*)bgr.Data;
        var srcStep = (nint)bgr.Step();

        switch (bgr.Channels())
        {
            case 3:
                for (var r = 0; r < bgr.Rows; r++)
                {
                    var s = src + r * srcStep;
                    var d = dst + r * dstStep;
                    for (var c = 0; c < bgr.Cols; c++)
                    {
                        d[0] = s[0];
                        d[1] = s[1];
                        d[2] = s[2];
                        d[3] = 255;
                        s += 3;
                        d += 4;
                    }
                }
                return bitmap;

            case 1:
                for (var r = 0; r < bgr.Rows; r++)
                {
                    var s = src + r * srcStep;
                    var d = dst + r * dstStep;
                    for (var c = 0; c < bgr.Cols; c++)
                    {
                        d[0] = d[1] = d[2] = s[0];
                        d[3] = 255;
                        s += 1;
                        d += 4;
                    }
                }
                return bitmap;

            case 4:
                // 4 通道输入按 BGRA 直接整行复制——对已是 BGRA 的输入走 BGR2BGRA
                // 会被当成 3 通道错误转换（丢一通道/错位），必须与 3 通道路径区分
                {
                    var rowBytes4 = (nint)(bgr.Cols * 4);
                    for (var r = 0; r < bgr.Rows; r++)
                        Buffer.MemoryCopy(src + r * srcStep, dst + r * dstStep, rowBytes4, rowBytes4);
                }
                return bitmap;

            default:
                // 其他通道数（2 通道等）：先 CvtColor 到 BGRA 再整行复制
                using (var bgra = new Mat())
                {
                    Cv2.CvtColor(bgr, bgra, ColorConversionCodes.BGR2BGRA);
                    var src2 = (byte*)bgra.Data;
                    var srcStep2 = (nint)bgra.Step();
                    var rowBytes = (nint)(bgra.Cols * 4);
                    for (var r = 0; r < bgra.Rows; r++)
                        Buffer.MemoryCopy(src2 + r * srcStep2, dst + r * dstStep, rowBytes, rowBytes);
                }
                return bitmap;
        }
    }
}
