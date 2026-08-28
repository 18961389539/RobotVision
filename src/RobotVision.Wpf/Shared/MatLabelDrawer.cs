using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using OpenCvSharp;
using WpfPoint = System.Windows.Point;
using WpfRect = System.Windows.Rect;

namespace RobotVision.WpfHost.Shared;

/// <summary>
/// 在 OpenCV Mat 上绘制中文/Unicode 标签。Hershey 字体只支持 ASCII，中文会变成问号。
/// </summary>
internal static class MatLabelDrawer
{
    private const int Pad = 3;

    public static bool ContainsNonAscii(string text)
    {
        foreach (var ch in text)
        {
            if (ch > 127)
                return true;
        }

        return false;
    }

    /// <param name="baseline">与 Cv2.PutText 一致：文字基线左端点。</param>
    public static void DrawBaseline(Mat bgr, OpenCvSharp.Point baseline, string text, float fontSizePx, Color? foreground = null)
    {
        var rendered = RenderLabel(text, fontSizePx, foreground ?? Colors.White);
        var x = baseline.X - Pad;
        var y = baseline.Y - rendered.BaselineY;
        Blit(bgr, x, y, rendered.Pixels, rendered.Width, rendered.Height);
    }

    private readonly record struct RenderedLabel(byte[] Pixels, int Width, int Height, int BaselineY);

    private static RenderedLabel RenderLabel(string text, float fontSizePx, Color foreground)
    {
        var typeface = new Typeface(
            new FontFamily("Microsoft YaHei UI, Segoe UI"),
            FontStyles.Normal,
            FontWeights.Bold,
            FontStretches.Normal);
        var formatted = new FormattedText(
            text,
            CultureInfo.CurrentUICulture,
            FlowDirection.LeftToRight,
            typeface,
            fontSizePx,
            new SolidColorBrush(foreground),
            1.0);

        var w = (int)Math.Ceiling(formatted.Width) + Pad * 2;
        var h = (int)Math.Ceiling(formatted.Height) + Pad * 2;
        w = Math.Max(1, w);
        h = Math.Max(1, h);
        var baselineY = Pad + (int)Math.Ceiling(formatted.Baseline);

        var rtb = new RenderTargetBitmap(w, h, 96, 96, PixelFormats.Pbgra32);
        var dv = new DrawingVisual();
        using (var dc = dv.RenderOpen())
        {
            dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(24, 24, 24)), null, new WpfRect(0, 0, w, h));
            dc.DrawText(formatted, new WpfPoint(Pad, Pad));
        }

        rtb.Render(dv);
        var stride = w * 4;
        var pixels = new byte[stride * h];
        rtb.CopyPixels(pixels, stride, 0);
        return new RenderedLabel(pixels, w, h, baselineY);
    }

    private static void Blit(Mat bgr, int dstX, int dstY, byte[] bgra, int width, int height)
    {
        var stride = width * 4;
        for (var row = 0; row < height; row++)
        {
            var imgY = dstY + row;
            if ((uint)imgY >= (uint)bgr.Height)
                continue;

            for (var col = 0; col < width; col++)
            {
                var imgX = dstX + col;
                if ((uint)imgX >= (uint)bgr.Width)
                    continue;

                var si = row * stride + col * 4;
                var a = bgra[si + 3];
                if (a < 16)
                    continue;

                var srcB = bgra[si];
                var srcG = bgra[si + 1];
                var srcR = bgra[si + 2];
                var dst = bgr.At<Vec3b>(imgY, imgX);
                var af = a / 255f;
                bgr.At<Vec3b>(imgY, imgX) = new Vec3b(
                    (byte)(srcB * af + dst.Item0 * (1f - af)),
                    (byte)(srcG * af + dst.Item1 * (1f - af)),
                    (byte)(srcR * af + dst.Item2 * (1f - af)));
            }
        }
    }
}
