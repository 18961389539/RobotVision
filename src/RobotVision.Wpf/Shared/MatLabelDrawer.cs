using System.Collections.Concurrent;
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

    /// <summary>渲染缓存上限：键含完整文本，若标签嵌入配方名/时间戳等动态文本将无限增长，须限容。</summary>
    private const int MaxCacheEntries = 256;

    private static readonly ConcurrentDictionary<LabelCacheKey, RenderedLabel> LabelCache = new();

    private readonly record struct LabelCacheKey(string Text, int FontSizeQ, byte R, byte G, byte B);

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
        var key = new LabelCacheKey(
            text,
            QuantizeFontSize(fontSizePx),
            foreground.R,
            foreground.G,
            foreground.B);
        if (LabelCache.TryGetValue(key, out var cached))
            return cached;

        var rendered = WpfOffscreenRender.Invoke(() => RenderLabelCore(text, fontSizePx, foreground));
        // 超限整体清空：缓存仅是渲染加速，重建单个标签成本 <1ms，避免动态文本导致无限增长。
        // ConcurrentDictionary 并发安全；竞态下最多略超上限或丢一个刚插入项，均可接受。
        if (LabelCache.Count >= MaxCacheEntries)
            LabelCache.Clear();
        LabelCache[key] = rendered;
        return rendered;
    }

    private static int QuantizeFontSize(float fontSizePx) =>
        (int)Math.Round(fontSizePx * 4, MidpointRounding.AwayFromZero);

    private static RenderedLabel RenderLabelCore(string text, float fontSizePx, Color foreground)
    {
        var typeface = new Typeface(
            new FontFamily("Microsoft YaHei UI, Segoe UI"),
            FontStyles.Normal,
            FontWeights.Bold,
            FontStretches.Normal);
        var foregroundBrush = new SolidColorBrush(foreground);
        foregroundBrush.Freeze();
        var formatted = new FormattedText(
            text,
            CultureInfo.CurrentUICulture,
            FlowDirection.LeftToRight,
            typeface,
            fontSizePx,
            foregroundBrush,
            1.0);

        var w = (int)Math.Ceiling(formatted.Width) + Pad * 2;
        var h = (int)Math.Ceiling(formatted.Height) + Pad * 2;
        w = Math.Max(1, w);
        h = Math.Max(1, h);
        var baselineY = Pad + (int)Math.Ceiling(formatted.Baseline);

        var backgroundBrush = new SolidColorBrush(Color.FromRgb(24, 24, 24));
        backgroundBrush.Freeze();
        var rtb = new RenderTargetBitmap(w, h, 96, 96, PixelFormats.Pbgra32);
        var dv = new DrawingVisual();
        using (var dc = dv.RenderOpen())
        {
            dc.DrawRectangle(backgroundBrush, null, new WpfRect(0, 0, w, h));
            dc.DrawText(formatted, new WpfPoint(Pad, Pad));
        }

        rtb.Render(dv);
        rtb.Freeze();
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
