using System;
using System.Collections.Generic;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using ImageViewer.Rendering;

namespace ImageViewer.Services
{
    internal sealed class ImageViewerDisplaySourceService
    {
        // 修复：按 palette 预生成 256 级 LUT，避免逐像素 switch + Interpolate 的高频计算。
        private static readonly Dictionary<PseudoColorPalette, uint[]> PaletteLutCache = new();

        private static uint[] GetPaletteLut(PseudoColorPalette palette)
        {
            lock (PaletteLutCache)
            {
                if (!PaletteLutCache.TryGetValue(palette, out uint[]? lut))
                {
                    lut = new uint[256];
                    for (int i = 0; i < 256; i++)
                    {
                        Color color = GetPaletteColor(i / 255d, palette);
                        lut[i] = (uint)color.B | ((uint)color.G << 8) | ((uint)color.R << 16) | (0xFFu << 24);
                    }

                    PaletteLutCache[palette] = lut;
                }

                return lut;
            }
        }
        public static ImageSource? BuildDisplaySource(ImageSource? source, PseudoColorPalette palette)
        {
            if (source is not BitmapSource bitmap || palette == PseudoColorPalette.None)
            {
                return source;
            }

            return ApplyPseudoColor(bitmap, palette);
        }

        public static void ApplyGpuCaching(Canvas imageContainer, bool enableGpuRendering)
        {
            ArgumentNullException.ThrowIfNull(imageContainer);
            imageContainer.CacheMode = enableGpuRendering ? new BitmapCache() : null;
        }

        public static BitmapSource? GetAnalysisBitmap(ImageSource? source)
        {
            if (source is not BitmapSource bitmap)
            {
                return null;
            }

            if (bitmap is RenderTargetBitmap)
            {
                var detachedBitmap = new WriteableBitmap(bitmap);
                if (detachedBitmap.CanFreeze)
                {
                    detachedBitmap.Freeze();
                }

                return detachedBitmap;
            }

            if (bitmap.IsFrozen)
            {
                return bitmap;
            }

            BitmapSource clone = bitmap.Clone();
            if (clone.CanFreeze)
            {
                clone.Freeze();
            }

            return clone;
        }

        public static Effect? CreatePseudoColorEffect(PseudoColorPalette palette)
        {
            if (palette == PseudoColorPalette.None)
            {
                return null;
            }

            try
            {
                return new PseudoColorShaderEffect(palette);
            }
            catch (Exception exception)
            {
                // 修复：原空 catch 吞掉 shader 编译失败，记录真实异常信息便于排查。
                System.Diagnostics.Trace.WriteLine($"Pseudo color shader effect creation failed for palette '{palette}': {exception}");
                return null;
            }
        }

        private static BitmapSource ApplyPseudoColor(BitmapSource source, PseudoColorPalette palette)
        {
            BitmapSource normalizedSource = source.Format == PixelFormats.Gray8
                ? source
                : new FormatConvertedBitmap(source, PixelFormats.Gray8, null, 0);

            int width = normalizedSource.PixelWidth;
            int height = normalizedSource.PixelHeight;
            int stride = width;
            byte[] grayPixels = new byte[stride * height];
            normalizedSource.CopyPixels(grayPixels, stride, 0);

            byte[] colorPixels = new byte[width * height * 4];
            // 修复：256 级 LUT 查表替代逐像素 switch + Interpolate。
            uint[] lut = GetPaletteLut(palette);
            for (int i = 0; i < grayPixels.Length; i++)
            {
                uint color = lut[grayPixels[i]];
                int colorIndex = i * 4;
                colorPixels[colorIndex] = (byte)color;
                colorPixels[colorIndex + 1] = (byte)(color >> 8);
                colorPixels[colorIndex + 2] = (byte)(color >> 16);
                colorPixels[colorIndex + 3] = (byte)(color >> 24);
            }

            var result = BitmapSource.Create(width, height, source.DpiX, source.DpiY, PixelFormats.Bgra32, null, colorPixels, width * 4);
            result.Freeze();
            return result;
        }

        private static Color GetPaletteColor(double value, PseudoColorPalette palette)
        {
            value = Math.Clamp(value, 0d, 1d);
            return palette switch
            {
                PseudoColorPalette.Hot => Interpolate(value,
                    (0.0, Colors.Black),
                    (0.33, Colors.DarkRed),
                    (0.66, Colors.Orange),
                    (1.0, Colors.Yellow)),
                PseudoColorPalette.Jet => Interpolate(value,
                    (0.0, Color.FromRgb(0, 0, 128)),
                    (0.35, Colors.Cyan),
                    (0.66, Colors.Yellow),
                    (1.0, Color.FromRgb(128, 0, 0))),
                PseudoColorPalette.Viridis => Interpolate(value,
                    (0.0, Color.FromRgb(68, 1, 84)),
                    (0.33, Color.FromRgb(59, 82, 139)),
                    (0.66, Color.FromRgb(33, 145, 140)),
                    (1.0, Color.FromRgb(253, 231, 37))),
                _ => Colors.Transparent
            };
        }

        private static Color Interpolate(double value, params (double Stop, Color Color)[] stops)
        {
            if (stops.Length == 0)
            {
                return Colors.Transparent;
            }

            if (value <= stops[0].Stop)
            {
                return stops[0].Color;
            }

            for (int i = 1; i < stops.Length; i++)
            {
                if (value <= stops[i].Stop)
                {
                    double range = stops[i].Stop - stops[i - 1].Stop;
                    double t = range <= 0 ? 0 : (value - stops[i - 1].Stop) / range;
                    return Color.FromRgb(
                        (byte)Math.Round(stops[i - 1].Color.R + (stops[i].Color.R - stops[i - 1].Color.R) * t),
                        (byte)Math.Round(stops[i - 1].Color.G + (stops[i].Color.G - stops[i - 1].Color.G) * t),
                        (byte)Math.Round(stops[i - 1].Color.B + (stops[i].Color.B - stops[i - 1].Color.B) * t));
                }
            }

            return stops[^1].Color;
        }
    }
}