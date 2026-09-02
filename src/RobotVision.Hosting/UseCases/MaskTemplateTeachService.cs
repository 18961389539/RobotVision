using OpenCvSharp;
using RobotVision.Core.Recipe;
using RobotVision.Infrastructure.Inference.Strategies;
using RobotVision.Vision.Inference.Strategies;

namespace RobotVision.Hosting;

internal sealed class MaskTemplateTeachService : IMaskTemplateTeachService
{
    public BgraImageBuffer? TryDecodePreview(string templatePngBase64)
    {
        if (string.IsNullOrEmpty(templatePngBase64))
            return null;
        try
        {
            using var mat = MaskTemplateHostingOps.DecodeTemplatePng(templatePngBase64);
            return BgraImageBuffer.FromBgrMat(mat);
        }
        catch
        {
            return null;
        }
    }

    public string GetTeachDiagnostics(string templatePngBase64, SegmentRefineMethod method)
    {
        if (string.IsNullOrEmpty(templatePngBase64))
            return "";
        try
        {
            using var mat = MaskTemplateHostingOps.DecodeTemplatePng(templatePngBase64);
            return method switch
            {
                SegmentRefineMethod.ShapeMatch => MaskShapeMatch.BuildTeach(mat) is { } shape
                    ? $"形状示教边缘点 {shape.PointCount} 个"
                    : "形状示教边缘点不足（需 ≥24 个 Canny 采样点）",
                SegmentRefineMethod.Sift => BuildSiftDiagnostics(mat),
                _ => "",
            };
        }
        catch
        {
            return "";
        }
    }

    private static string BuildSiftDiagnostics(Mat mat)
    {
        var model = MaskSiftRefine.BuildTeach(mat);
        if (model is null)
            return "SIFT 示教特征不足（需 ≥16 个关键点）";
        using (model)
            return $"SIFT 示教关键点 {model.KeypointCount} 个";
    }
}
