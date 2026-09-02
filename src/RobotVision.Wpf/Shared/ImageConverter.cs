using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using OpenCvSharp;
using RobotVision.Core.Models;
using RobotVision.Hosting;

namespace RobotVision.WpfHost.Shared;

/// <summary>Mat → 冻结 BitmapSource 转换（可在任意线程调用，产物可跨线程使用）。</summary>
public static class ImageConverter
{
    public static BitmapSource ToBitmapSource(VisionImage image)
    {
        using var mat = VisionImageMat.AsMat(image);
        return ToBitmapSource(mat);
    }

    public static BitmapSource ToBitmapSource(BgraImageBuffer buffer)
    {
        var bitmap = new WriteableBitmap(buffer.Width, buffer.Height, 96, 96, PixelFormats.Bgra32, null);
        bitmap.WritePixels(
            new Int32Rect(0, 0, buffer.Width, buffer.Height),
            buffer.Pixels,
            buffer.Stride,
            0);
        bitmap.Freeze();
        return bitmap;
    }

    public static BitmapSource ToBitmapSource(Mat bgr)
    {
        var width = bgr.Width;
        var height = bgr.Height;
        var stride = (int)bgr.Step();

        if (bgr.Channels() == 4)
        {
            // 已是 BGRA：直接同步拷贝写像素，无需 Clone（5MP 帧省 20 MB/帧；WritePixels 为拷贝语义，源只需在读瞬间有效）
            var bitmap = new WriteableBitmap(width, height, 96, 96, PixelFormats.Bgra32, null);
            bitmap.WritePixels(new Int32Rect(0, 0, width, height), bgr.Data, (int)(bgr.Rows * bgr.Step()), stride);
            bitmap.Freeze();
            return bitmap;
        }

        using var bgra = new Mat();
        Cv2.CvtColor(bgr, bgra, ColorConversionCodes.BGR2BGRA);
        var converted = new WriteableBitmap(width, height, 96, 96, PixelFormats.Bgra32, null);
        converted.WritePixels(
            new Int32Rect(0, 0, width, height), bgra.Data, (int)(bgra.Rows * bgra.Step()), (int)bgra.Step());
        converted.Freeze();
        return converted;
    }
}
