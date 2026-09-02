using OpenCvSharp;
using RobotVision.Infrastructure.Inference.Strategies;

namespace RobotVision.Hosting;

/// <summary>掩码模板匹配基础设施的统一入口（Hosting 内聚 Infrastructure 策略细节）。</summary>
internal static class MaskTemplateHostingOps
{
    public static Mat DecodeTemplatePng(string base64Png) => MaskTemplateMatcher.DecodeTemplatePng(base64Png);

    public static string EncodeTemplatePng(Mat template) => MaskTemplateMatcher.EncodeTemplatePng(template);

    public static UprightCropResult UprightCrop(Mat image, Point2f[] contour, double marginRatio) =>
        MaskTemplateMatcher.UprightCrop(image, contour, marginRatio);

    public static Mat CropUprightBySourceRect(
        UprightCropResult crop,
        double sourceX,
        double sourceY,
        double sourceWidth,
        double sourceHeight) =>
        MaskTemplateMatcher.CropUprightBySourceRect(crop, sourceX, sourceY, sourceWidth, sourceHeight);
}
