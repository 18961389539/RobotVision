using OpenCvSharp;
using RobotVision.JlVision;

namespace RobotVision.Hosting;

/// <summary>掩码模板匹配基础设施的统一入口（Hosting 内聚转正裁剪与 PNG 编解码）。</summary>
internal static class MaskTemplateHostingOps
{
    public static Mat DecodeTemplatePng(string base64Png) => JlTemplateIo.DecodePng(base64Png);

    public static string EncodeTemplatePng(Mat template) => JlTemplateIo.EncodePng(template);

    public static JlUprightCrop UprightCrop(Mat image, Point2f[] contour, double marginRatio) =>
        JlTemplateIo.UprightCrop(image, contour, marginRatio);

    public static Mat CropUprightBySourceRect(
        JlUprightCrop crop,
        double sourceX,
        double sourceY,
        double sourceWidth,
        double sourceHeight) =>
        JlTemplateIo.CropUprightBySourceRect(crop, sourceX, sourceY, sourceWidth, sourceHeight);
}
