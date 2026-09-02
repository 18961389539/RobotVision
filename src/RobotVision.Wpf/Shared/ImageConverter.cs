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
        Mat bgra;
        if (bgr.Channels() == 3)
        {
            bgra = new Mat();
            Cv2.CvtColor(bgr, bgra, ColorConversionCodes.BGR2BGRA);
        }
        else
        {
            bgra = bgr.Clone();
        }

        try
        {
            var width = bgra.Width;
            var height = bgra.Height;
            var stride = (int)bgra.Step();
            var size = (int)(bgra.Rows * bgra.Step());

            var bitmap = new WriteableBitmap(width, height, 96, 96, PixelFormats.Bgra32, null);
            bitmap.WritePixels(new Int32Rect(0, 0, width, height), bgra.Data, size, stride);
            bitmap.Freeze();
            return bitmap;
        }
        finally
        {
            bgra.Dispose();
        }
    }
}
