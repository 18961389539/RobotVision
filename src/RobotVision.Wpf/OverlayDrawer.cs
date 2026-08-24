using OpenCvSharp;
using RobotVision.Core.Models;

namespace RobotVision.WpfHost;

/// <summary>
/// 检测叠加层：在去畸变图像上绘制中心十字、角度方向箭头与序号/分数。
/// 像素坐标系 y 轴向下，与 AngleGeometry 输出的角度约定一致，箭头方向直接取 (cosθ, sinθ)。
/// </summary>
public static class OverlayDrawer
{
    private const double ArrowLength = 60;

    public static void DrawPoses(Mat image, IReadOnlyList<PixelPose> poses)
    {
        for (var i = 0; i < poses.Count; i++)
        {
            var pose = poses[i];
            var center = new Point(pose.Cx, pose.Cy);
            var rad = pose.AngleDeg * Math.PI / 180.0;
            var tip = new Point(center.X + ArrowLength * Math.Cos(rad), center.Y + ArrowLength * Math.Sin(rad));

            Cv2.ArrowedLine(image, center, tip, Scalar.OrangeRed, 2, LineTypes.AntiAlias, 0, 0.35);
            Cv2.DrawMarker(image, center, Scalar.Lime, MarkerTypes.Cross, 18, 2, LineTypes.AntiAlias);

            var label = $"#{i} {pose.Score:F2}";
            var origin = new Point(center.X + 10, center.Y - 10);
            // 黑色描边 + 白色文字，保证任意底色可读
            Cv2.PutText(image, label, new Point(origin.X + 1, origin.Y + 1),
                HersheyFonts.HersheyPlain, 1.3, Scalar.Black, 3, LineTypes.AntiAlias);
            Cv2.PutText(image, label, origin,
                HersheyFonts.HersheyPlain, 1.3, Scalar.White, 1, LineTypes.AntiAlias);
        }
    }
}

