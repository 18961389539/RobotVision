using OpenCvSharp;
using RobotVision.Core.Recipe;
using RobotVision.JlVision;

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
                SegmentRefineMethod.ShapeMatch or SegmentRefineMethod.Sift =>
                    TeachShape(mat),
                SegmentRefineMethod.Template => TeachNcc(mat),
                _ => "",
            };
        }
        catch
        {
            return "";
        }
    }

    private static string TeachShape(Mat mat)
    {
        try
        {
            using var gray = JlImageConvert.ToGray(mat);
            using var model = JlShapeRefine.CreateModel(gray);
            return "JLVision 形状模型已建立";
        }
        catch (Exception ex)
        {
            return "JLVision 形状示教失败：" + ex.Message;
        }
    }

    private static string TeachNcc(Mat mat)
    {
        try
        {
            using var gray = JlImageConvert.ToGray(mat);
            using var model = JlNccRefine.CreateModel(gray);
            return "JLVision NCC 模型已建立";
        }
        catch (Exception ex)
        {
            return "JLVision NCC 示教失败：" + ex.Message;
        }
    }
}
