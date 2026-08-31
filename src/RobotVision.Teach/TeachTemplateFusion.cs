using OpenCvSharp;

namespace RobotVision.Teach;

/// <summary>多帧模板中位融合辅助（不进 TRIGGER）。配方页「示教模板」现为单帧，本类仅保留给测试/工具。</summary>
public static class TeachTemplateFusion
{
    public const int DefaultFrameCount = 3;
    public const int GrabGapMs = 80;
    public const double MinSameTargetNcc = 0.75;

    /// <summary>尺寸接近且 NCC 够高视为同一目标；否则回放相机多文件会误融不同零件。</summary>
    public static bool SameTarget(Mat a, Mat b)
    {
        if (a.Empty() || b.Empty() || a.Width < 8 || a.Height < 8 || b.Width < 8 || b.Height < 8)
            return false;
        var areaRatio = (a.Width * (double)a.Height) / (b.Width * (double)b.Height);
        if (areaRatio < 0.7 || areaRatio > 1.0 / 0.7)
            return false;
        using var resized = new Mat();
        Cv2.Resize(b, resized, new Size(a.Width, a.Height), 0, 0, InterpolationFlags.Area);
        if (resized.Width > a.Width || resized.Height > a.Height)
            return false;
        using var result = a.MatchTemplate(resized, TemplateMatchModes.CCoeffNormed);
        Cv2.MinMaxLoc(result, out _, out var maxVal, out _, out _);
        return maxVal >= MinSameTargetNcc;
    }

    /// <summary>对齐到中位宽高后按通道取中位像素。单帧则克隆。</summary>
    public static Mat Blend(IReadOnlyList<Mat> templates)
    {
        if (templates.Count == 0)
            throw new ArgumentException("没有可融合的模板", nameof(templates));
        if (templates.Count == 1)
            return templates[0].Clone();

        var w = MedianInt(templates.Select(t => t.Width).ToArray());
        var h = MedianInt(templates.Select(t => t.Height).ToArray());
        w = Math.Max(8, w);
        h = Math.Max(8, h);
        var aligned = new Mat[templates.Count];
        try
        {
            for (var i = 0; i < templates.Count; i++)
            {
                aligned[i] = new Mat();
                Cv2.Resize(templates[i], aligned[i], new Size(w, h), 0, 0, InterpolationFlags.Area);
                if (aligned[i].Channels() == 1)
                    Cv2.CvtColor(aligned[i], aligned[i], ColorConversionCodes.GRAY2BGR);
            }

            var n = aligned.Length;
            var blend = new Mat(h, w, MatType.CV_8UC3);
            var dest = blend.GetGenericIndexer<Vec3b>();
            var b0 = new byte[n];
            var b1 = new byte[n];
            var b2 = new byte[n];
            for (var y = 0; y < h; y++)
            {
                for (var x = 0; x < w; x++)
                {
                    for (var i = 0; i < n; i++)
                    {
                        var v = aligned[i].At<Vec3b>(y, x);
                        b0[i] = v.Item0;
                        b1[i] = v.Item1;
                        b2[i] = v.Item2;
                    }

                    Array.Sort(b0);
                    Array.Sort(b1);
                    Array.Sort(b2);
                    dest[y, x] = new Vec3b(MedianByte(b0), MedianByte(b1), MedianByte(b2));
                }
            }

            return blend;
        }
        finally
        {
            foreach (var m in aligned)
                m?.Dispose();
        }
    }

    public static double Median(IReadOnlyList<double> values)
    {
        if (values.Count == 0)
            return 0;
        var a = values.OrderBy(v => v).ToArray();
        var n = a.Length;
        return (n & 1) == 1 ? a[n / 2] : 0.5 * (a[n / 2 - 1] + a[n / 2]);
    }

    private static int MedianInt(int[] values)
    {
        Array.Sort(values);
        var n = values.Length;
        return (n & 1) == 1 ? values[n / 2] : (values[n / 2 - 1] + values[n / 2]) / 2;
    }

    private static byte MedianByte(byte[] ordered)
    {
        var n = ordered.Length;
        return (n & 1) == 1
            ? ordered[n / 2]
            : (byte)((ordered[n / 2 - 1] + ordered[n / 2]) / 2);
    }
}
