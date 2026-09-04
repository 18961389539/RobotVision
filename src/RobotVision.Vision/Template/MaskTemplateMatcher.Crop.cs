using OpenCvSharp;
using RobotVision.Core.Geometry;

namespace RobotVision.Vision;

public static partial class MaskTemplateMatcher
{
    /// <summary>
    /// 按最小外接矩形把目标转正并裁剪（warpAffine 旋转 -θ 后取矩形区域）。
    /// θ 取长边方向（与 AngleGeometry.LongAxisFromMinAreaRect 同口径），转正后长边水平。
    /// marginRatio 为四周边距（相对矩形边长）；示教与运行时匹配均用 0.15（见 <see cref="MaskShapeMatch.CropMarginRatio"/>）。
    /// 只旋转能盖住裁剪窗的局部补丁（对角线边长），避免对整幅相机图 WarpAffine。
    /// 映射仍按「绕原图矩形中心旋转、裁剪原点在原图像素系」计算，与整图旋转等价。
    /// </summary>
    public static UprightCropResult UprightCrop(
        Mat src, IReadOnlyList<Point2f> contour, double marginRatio, double extraWarpDeg = 0)
    {
        var rect = Cv2.MinAreaRect(contour);
        var housing = contour.Count >= 8 ? MaskHousing.Fit(contour) : default(HousingFrame?);
        // 与 WarpAffine 使用同一未归一化角：NormalizeDeg 会把 180° 折成 0°，逆变换会差 180°
        // 壳体中心/角用于转正（凸起不拖偏）；裁剪窗尺寸仍按整段轮廓，以免裁掉凸起纹理
        var warpAngleDeg = (housing is { } h
                               ? h.WarpAngleDeg
                               : MaskHousing.WarpFromMinAreaRect(rect))
                           + extraWarpDeg;
        var center = housing is { } hh ? hh.Center : rect.Center;

        // 裁剪窗尺寸显式用长/短边（max/min），不随 MinAreaRect 的 Width/Height 表示顺序变化：
        // OpenCV 对同一矩形可返回 (Width=长边,A=α) 或 (Width=短边,A=α±90)，直接读 Size 会让
        // 转正后的窗方向与目标错位（如 -33° 目标被裁）。warp 角保持原口径（不在此改动）。
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

        // 轴对齐 cropW×cropH 旋转后的外接圆直径 = 对角线；+2 吸收 floor 对齐
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

        return new UprightCropResult(
            rotated[new Rect(lx, ly, cropW, cropH)].Clone(), warpAngleDeg, center, x, y);
    }

    /// <summary>
    /// 不转正：按轮廓轴对齐包围盒外扩裁剪。WarpAngleDeg=0，匹配须把示教模板转到现场粗角。
    /// 匹配峰用 <see cref="MapUprightToSource"/> 映回原图（此时为平移）。
    /// </summary>
    public static UprightCropResult AxisAlignedCrop(
        Mat src, IReadOnlyList<Point2f> contour, double marginRatio)
    {
        if (contour.Count < 1)
            throw new InvalidOperationException("转正裁剪区域为空（目标超出图像边界）");
        var pts = contour as Point2f[] ?? [.. contour];
        var box = Cv2.BoundingRect(pts);
        var mx = (int)Math.Ceiling(box.Width * marginRatio);
        var my = (int)Math.Ceiling(box.Height * marginRatio);
        var x = box.X - mx;
        var y = box.Y - my;
        var cropW = box.Width + 2 * mx;
        var cropH = box.Height + 2 * my;

        x = Math.Clamp(x, 0, Math.Max(0, src.Width - 1));
        y = Math.Clamp(y, 0, Math.Max(0, src.Height - 1));
        cropW = Math.Min(cropW, src.Width - x);
        cropH = Math.Min(cropH, src.Height - y);
        if (cropW <= 0 || cropH <= 0)
            throw new InvalidOperationException("转正裁剪区域为空（目标超出图像边界）");

        var center = new Point2f((float)(x + cropW / 2.0), (float)(y + cropH / 2.0));
        return new UprightCropResult(
            src[new Rect(x, y, cropW, cropH)].Clone(), 0, center, x, y);
    }

    /// <summary>
    /// 把转正裁剪图上的点映回源图坐标：先加裁剪原点（得到旋转整图坐标），再 Invert
    /// 与 UprightCrop 相同的绕矩形中心旋转 -θ。
    /// </summary>
    public static Point2d MapUprightToSource(UprightCropResult crop, Point2d centerInUpright)
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

    /// <summary>
    /// 源图坐标 → 转正裁剪图坐标：与 <see cref="MapUprightToSource"/> 互逆
    ///（同一旋转矩阵，不加 Invert）。
    /// </summary>
    public static Point2d MapSourceToUpright(UprightCropResult crop, Point2d source)
    {
        using var m = Cv2.GetRotationMatrix2D(crop.RotationCenter, -crop.WarpAngleDeg, 1.0);
        var rx = m.At<double>(0, 0) * source.X + m.At<double>(0, 1) * source.Y + m.At<double>(0, 2);
        var ry = m.At<double>(1, 0) * source.X + m.At<double>(1, 1) * source.Y + m.At<double>(1, 2);
        return new Point2d(rx - crop.CropOriginX, ry - crop.CropOriginY);
    }

    /// <summary>匹配角落在 180° 支（头尾与示教相反），转正窗相对示教翻了面。</summary>
    public static bool IsOrientationFlip(double rotationDeg) =>
        Math.Abs(NormalizeSigned(rotationDeg)) >= 90;

    /// <summary>
    /// 转正窗需要再转 180° 才能与示教同向：匹配已走 180° 支，或 180° NCC 明显高于 0°
    ///（极性偶发同号时仍会用错 0° 峰，Y 跳一档）。
    /// </summary>
    public static bool NeedsUprightAlign(MaskTemplateMatchResult match) =>
        IsOrientationFlip(match.RotationDeg)
        || LastDebug.Score180 > LastDebug.Score0 + 0.08;

    /// <summary>
    /// 把源图上的轴对齐特征框映到转正图，取四角 AABB 后裁出模板（独立拷贝）。
    /// 示教用：特征框在相机图上画出，模板必须与运行时同一套转正坐标系。
    /// </summary>
    public static Mat CropUprightBySourceRect(
        UprightCropResult crop, double x, double y, double width, double height)
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
