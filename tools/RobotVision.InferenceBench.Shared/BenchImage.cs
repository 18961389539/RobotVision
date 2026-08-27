using OpenCvSharp;
using RobotVision.Core.Models;

namespace RobotVision.InferenceBench;

/// <summary>
/// 与 FileCamera 相同：按字节解码（避开中文路径 ImRead 空图）。
/// ROI 裁剪口径与 <c>RoiHelper.Crop</c> 一致（四舍五入后与图像求交）。
/// </summary>
public sealed class LoadedFrame : IDisposable
{
    public Mat Full { get; }
    public Mat Inference { get; }
    public double OffsetX { get; }
    public double OffsetY { get; }
    public bool IsCropped { get; }

    internal LoadedFrame(Mat full, Mat inference, double ox, double oy, bool cropped)
    {
        Full = full;
        Inference = inference;
        OffsetX = ox;
        OffsetY = oy;
        IsCropped = cropped;
    }

    public void Dispose()
    {
        if (IsCropped)
            Inference.Dispose();
        Full.Dispose();
    }
}

public static class BenchImage
{
    private static readonly string[] Extensions = [".bmp", ".jpg", ".jpeg", ".png", ".tif", ".tiff"];

    /// <summary>文件或目录：目录下按文件名排序，扩展名与 FileCamera 相同。</summary>
    public static IReadOnlyList<string> ListImageFiles(string path)
    {
        if (Directory.Exists(path))
        {
            var files = Directory.EnumerateFiles(path)
                .Where(f => Extensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
                .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (files.Length == 0)
                throw new FileNotFoundException($"目录中没有图片: {path}");
            return files;
        }

        if (File.Exists(path))
            return [path];

        throw new FileNotFoundException($"图片或目录不存在: {path}");
    }

    public static List<LoadedFrame> LoadAll(string path, Roi? roi) =>
        ListImageFiles(path).Select(f => Load(f, roi)).ToList();

    public static LoadedFrame Load(string path, Roi? roi)
    {
        byte[] bytes;
        try
        {
            bytes = File.ReadAllBytes(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new FileNotFoundException($"无法读取图像: {path}", ex);
        }

        var mat = Cv2.ImDecode(bytes, ImreadModes.Color);
        if (mat.Empty())
            throw new InvalidDataException($"无法解码图像: {path}");

        if (roi is null)
            return new LoadedFrame(mat, mat, 0, 0, cropped: false);

        var cropped = Crop(mat, roi, out var ox, out var oy);
        return new LoadedFrame(mat, cropped, ox, oy, cropped: true);
    }

    /// <summary>与 RoiHelper.Crop 同一套 clamp / Round / 求交，避免 bench 与产线裁出不同尺寸。</summary>
    public static Mat Crop(Mat image, Roi roi, out double offsetX, out double offsetY)
    {
        var x = Math.Clamp(roi.X, 0, 1) * image.Width;
        var y = Math.Clamp(roi.Y, 0, 1) * image.Height;
        var w = Math.Min(roi.Width, 1 - roi.X) * image.Width;
        var h = Math.Min(roi.Height, 1 - roi.Y) * image.Height;
        w = Math.Max(1, w);
        h = Math.Max(1, h);

        var rect = new Rect(
            (int)Math.Round(x), (int)Math.Round(y), (int)Math.Round(w), (int)Math.Round(h));
        var clipped = rect & new Rect(0, 0, image.Width, image.Height);
        offsetX = clipped.X;
        offsetY = clipped.Y;
        if (clipped.Width <= 0 || clipped.Height <= 0)
            throw new ArgumentException($"ROI {roi} 与图像 {image.Width}x{image.Height} 无交集，无法裁剪");
        return new Mat(image, clipped);
    }

    public static Roi? ParseRoi(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        var parts = text.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 4)
            throw new FormatException("--roi 需要 4 个数：x,y,w,h（相对 0~1）");

        var vals = parts.Select(p => double.Parse(p, System.Globalization.CultureInfo.InvariantCulture)).ToArray();
        return new Roi(vals[0], vals[1], vals[2], vals[3]);
    }
}
