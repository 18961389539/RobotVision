using OpenCvSharp;
using RobotVision.Core.Models;
using SkiaSharp;
using RobotVision.Infrastructure;

namespace RobotVision.Infrastructure.Inference;

/// <summary>
/// ROI 裁剪辅助：按相对比例裁剪检测区域，并把推理结果坐标偏移回全图坐标系。
/// 裁剪使用 Mat 视图（共享像素数据，不复制），调用方负责 Dispose 返回的视图。
/// </summary>
public static class RoiHelper
{
    /// <summary>
    /// 把去畸变图按配方 ROI 转成 SKBitmap。返回偏移量 (offsetX, offsetY)，
    /// 推理结果坐标加上偏移即全图坐标。ROI 为 null 时返回全图转换、偏移为 0。
    /// </summary>
    public static SKBitmap ToBitmap(Mat undistorted, Roi? roi, out double offsetX, out double offsetY)
    {
        if (roi is null)
        {
            offsetX = 0;
            offsetY = 0;
            return MatSkiaConverter.ToSKBitmap(undistorted);
        }

        using var cropped = Crop(undistorted, roi, out offsetX, out offsetY);
        return MatSkiaConverter.ToSKBitmap(cropped);
    }

    /// <summary>
    /// 按 ROI 得到推理用 VisionImage。roi 为 null 时返回 null（调用方直接用原图，避免多余拷贝）。
    /// 非 null 时返回拥有裁剪头的 VisionImage，调用方必须 Dispose；像素仍共享自 source。
    /// </summary>
    public static VisionImage? CropToVisionImage(VisionImage source, Roi? roi, out double offsetX, out double offsetY)
    {
        if (roi is null)
        {
            offsetX = 0;
            offsetY = 0;
            return null;
        }

        using var mat = VisionImageCv.AsMat(source);
        Mat? cropped = Crop(mat, roi, out offsetX, out offsetY);
        try
        {
            var image = VisionImageCv.Adopt(cropped);
            cropped = null;
            return image;
        }
        finally
        {
            cropped?.Dispose();
        }
    }

    /// <summary>按相对比例裁剪（边界 clamp），返回 Mat 视图（共享数据）。</summary>
    public static Mat Crop(Mat image, Roi roi, out double offsetX, out double offsetY)
    {
        var x = Math.Clamp(roi.X, 0, 1) * image.Width;
        var y = Math.Clamp(roi.Y, 0, 1) * image.Height;
        var w = Math.Min(roi.Width, 1 - roi.X) * image.Width;
        var h = Math.Min(roi.Height, 1 - roi.Y) * image.Height;
        w = Math.Max(1, w);
        h = Math.Max(1, h);

        // 先整体四舍五入得到整数 Rect，再与图像边界求交：x/w 各自独立 Round 会让靠边 ROI
        // 的右/下边界超出图像 1px（越界视图访问未定义内存）；求交后把超界部分裁掉
        var rect = new Rect(
            (int)Math.Round(x), (int)Math.Round(y), (int)Math.Round(w), (int)Math.Round(h));
        var clipped = rect & new Rect(0, 0, image.Width, image.Height);
        offsetX = clipped.X;
        offsetY = clipped.Y;
        if (clipped.Width <= 0 || clipped.Height <= 0)
            throw new ArgumentException($"ROI {roi} 与图像 {image.Width}x{image.Height} 无交集，无法裁剪");
        return new Mat(image, clipped);
    }
}
