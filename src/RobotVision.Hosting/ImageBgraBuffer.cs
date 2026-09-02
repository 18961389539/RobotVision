using OpenCvSharp;

namespace RobotVision.Hosting;

/// <summary>冻结前的 BGRA 像素缓冲（跨层传递 Mat 预览，避免 WPF 引用 OpenCvSharp 绘图链）。</summary>
public sealed record BgraImageBuffer(int Width, int Height, int Stride, byte[] Pixels)
{
    public static BgraImageBuffer FromBgrMat(Mat bgr)
    {
        Mat bgra;
        if (bgr.Channels() == 3)
        {
            bgra = new Mat();
            Cv2.CvtColor(bgr, bgra, ColorConversionCodes.BGR2BGRA);
        }
        else
            bgra = bgr.Clone();

        try
        {
            var stride = (int)bgra.Step();
            var size = (int)(bgra.Rows * bgra.Step());
            var pixels = new byte[size];
            System.Runtime.InteropServices.Marshal.Copy(bgra.Data, pixels, 0, size);
            return new BgraImageBuffer(bgra.Width, bgra.Height, stride, pixels);
        }
        finally
        {
            bgra.Dispose();
        }
    }

    public static void WritePng(BgraImageBuffer buffer, string path)
    {
        using var bgra = new Mat(buffer.Height, buffer.Width, MatType.CV_8UC4);
        var dstStride = (int)bgra.Step();
        var rowBytes = buffer.Width * 4;
        if (buffer.Stride == dstStride)
        {
            System.Runtime.InteropServices.Marshal.Copy(buffer.Pixels, 0, bgra.Data, buffer.Pixels.Length);
        }
        else
        {
            for (var y = 0; y < buffer.Height; y++)
            {
                var srcOffset = y * buffer.Stride;
                System.Runtime.InteropServices.Marshal.Copy(
                    buffer.Pixels, srcOffset, bgra.Data + (nint)(y * dstStride), rowBytes);
            }
        }

        using var bgr = new Mat();
        Cv2.CvtColor(bgra, bgr, ColorConversionCodes.BGRA2BGR);
        Cv2.ImWrite(path, bgr);
    }
}
