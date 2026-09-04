using OpenCvSharp;
using RobotVision.Core.Geometry;

namespace RobotVision.JlVision;

/// <summary>
/// 转正裁剪结果。匹配中心在 <see cref="Upright"/> 坐标系，映回原图须加裁剪原点后再逆旋转。
/// </summary>
public sealed record JlUprightCrop(
    Mat Upright,
    double WarpAngleDeg,
    Point2f RotationCenter,
    double CropOriginX,
    double CropOriginY);

/// <summary>示教 PNG 编解码与转正裁剪（Hosting / 示教与精修共用同一套坐标系）。</summary>
public static class JlTemplateIo
{
    public const double CropMarginRatio = 0.15;

    public static string EncodePng(Mat template)
    {
        Cv2.ImEncode(".png", template, out var bytes);
        return Convert.ToBase64String(bytes);
    }

    public static Mat DecodePng(string base64)
    {
        var bytes = Convert.FromBase64String(base64);
        var mat = Cv2.ImDecode(bytes, ImreadModes.Color);
        if (mat.Empty())
            throw new InvalidOperationException("模板图解码失败（数据损坏）");
        return mat;
    }

    public static JlUprightCrop UprightCrop(
        Mat src, IReadOnlyList<Point2f> contour, double marginRatio, double extraWarpDeg = 0)
    {
        var rect = Cv2.MinAreaRect(contour);
        var housing = contour.Count >= 8 ? JlHousing.Fit(contour) : default(HousingFrame?);
        var warpAngleDeg = (housing is { } h
                               ? h.WarpAngleDeg
                               : JlHousing.WarpFromMinAreaRect(rect))
                           + extraWarpDeg;
        var center = housing is { } hh ? hh.Center : rect.Center;

        var longLen = Math.Max(rect.Size.Width, rect.Size.Height);
        var shortLen = Math.Min(rect.Size.Width, rect.Size.Height);
        var marginX = longLen * marginRatio;
        var marginY = shortLen * marginRatio;
        var cropW = (int)Math.Ceiling(longLen + 2 * marginX);
        var cropH = (int)Math.Ceiling(shortLen + 2 * marginY);
        var x = (int)Math.Floor(center.X - cropW / 2.0);
        var y = (int)Math.Floor(center.Y - cropH / 2.0);

        x = Math.Clamp(x, 0, Math.Max(0, src.Width - 1));
        y = Math.Clamp(y, 0, Math.Max(0, src.Height - 1));
        cropW = Math.Min(cropW, src.Width - x);
        cropH = Math.Min(cropH, src.Height - y);
        if (cropW <= 0 || cropH <= 0)
            throw new InvalidOperationException("转正裁剪区域为空（目标超出图像边界）");

        var patch = (int)Math.Ceiling(Math.Sqrt((double)cropW * cropW + (double)cropH * cropH)) + 2;
        var px = Math.Clamp((int)Math.Floor(center.X - patch / 2.0), 0, Math.Max(0, src.Width - 1));
        var py = Math.Clamp((int)Math.Floor(center.Y - patch / 2.0), 0, Math.Max(0, src.Height - 1));
        var pw = Math.Min(patch, src.Width - px);
        var ph = Math.Min(patch, src.Height - py);
        if (pw <= 0 || ph <= 0)
            throw new InvalidOperationException("转正裁剪区域为空（目标超出图像边界）");

        using var region = src[new Rect(px, py, pw, ph)];
        var localCenter = new Point2f((float)(center.X - px), (float)(center.Y - py));
        using var m = Cv2.GetRotationMatrix2D(localCenter, -warpAngleDeg, 1.0);
        using var rotated = new Mat();
        Cv2.WarpAffine(region, rotated, m, new Size(pw, ph), InterpolationFlags.Linear,
            BorderTypes.Reflect101);

        var lx = x - px;
        var ly = y - py;
        if (lx < 0)
        {
            cropW += lx;
            x -= lx;
            lx = 0;
        }
        if (ly < 0)
        {
            cropH += ly;
            y -= ly;
            ly = 0;
        }
        cropW = Math.Min(cropW, rotated.Width - lx);
        cropH = Math.Min(cropH, rotated.Height - ly);
        if (cropW <= 0 || cropH <= 0 || lx >= rotated.Width || ly >= rotated.Height)
            throw new InvalidOperationException("转正裁剪区域为空（目标超出图像边界）");

        return new JlUprightCrop(
            rotated[new Rect(lx, ly, cropW, cropH)].Clone(), warpAngleDeg, center, x, y);
    }

    public static Point2d MapUprightToSource(JlUprightCrop crop, Point2d centerInUpright)
    {
        var rotatedX = centerInUpright.X + crop.CropOriginX;
        var rotatedY = centerInUpright.Y + crop.CropOriginY;
        using var m = Cv2.GetRotationMatrix2D(crop.RotationCenter, -crop.WarpAngleDeg, 1.0);
        using var mInv = new Mat();
        Cv2.InvertAffineTransform(m, mInv);
        var px = mInv.At<double>(0, 0) * rotatedX + mInv.At<double>(0, 1) * rotatedY + mInv.At<double>(0, 2);
        var py = mInv.At<double>(1, 0) * rotatedX + mInv.At<double>(1, 1) * rotatedY + mInv.At<double>(1, 2);
        return new Point2d(px, py);
    }

    public static Point2d MapSourceToUpright(JlUprightCrop crop, Point2d source)
    {
        using var m = Cv2.GetRotationMatrix2D(crop.RotationCenter, -crop.WarpAngleDeg, 1.0);
        var rx = m.At<double>(0, 0) * source.X + m.At<double>(0, 1) * source.Y + m.At<double>(0, 2);
        var ry = m.At<double>(1, 0) * source.X + m.At<double>(1, 1) * source.Y + m.At<double>(1, 2);
        return new Point2d(rx - crop.CropOriginX, ry - crop.CropOriginY);
    }

    public static Mat CropUprightBySourceRect(
        JlUprightCrop crop, double x, double y, double width, double height)
    {
        var corners = new[]
        {
            new Point2d(x, y),
            new Point2d(x + width, y),
            new Point2d(x + width, y + height),
            new Point2d(x, y + height),
        };
        var minX = double.PositiveInfinity;
        var minY = double.PositiveInfinity;
        var maxX = double.NegativeInfinity;
        var maxY = double.NegativeInfinity;
        foreach (var corner in corners)
        {
            var u = MapSourceToUpright(crop, corner);
            minX = Math.Min(minX, u.X);
            minY = Math.Min(minY, u.Y);
            maxX = Math.Max(maxX, u.X);
            maxY = Math.Max(maxY, u.Y);
        }

        var ix = (int)Math.Floor(minX);
        var iy = (int)Math.Floor(minY);
        var iw = (int)Math.Ceiling(maxX) - ix;
        var ih = (int)Math.Ceiling(maxY) - iy;
        var clipped = new Rect(ix, iy, Math.Max(0, iw), Math.Max(0, ih))
            & new Rect(0, 0, crop.Upright.Width, crop.Upright.Height);
        if (clipped.Width < 8 || clipped.Height < 8)
            throw new InvalidOperationException(
                "特征 ROI 转正后过小或落在目标外（请把特征框画在分割目标上）");
        return crop.Upright[clipped].Clone();
    }
}
