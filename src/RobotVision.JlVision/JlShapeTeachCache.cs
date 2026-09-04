using System.Security.Cryptography;
using System.Text;
using JLVisionLib;
using OpenCvSharp;
using RobotVision.Core.Recipe;

namespace RobotVision.JlVision;

/// <summary>按配方示教 PNG 缓存一份 JlShapeModel（进程内单槽）。</summary>
public static class JlShapeTeachCache
{
    private static readonly object Gate = new();
    private static string _fingerprint = "";
    private static JlShapeModel? _model;
    private static double _housingOffsetX;
    private static double _housingOffsetY;

    public static JlShapeModel? GetOrCreate(RecipeConfig recipe)
    {
        if (string.IsNullOrEmpty(recipe.Template.TemplateImageBase64))
            return null;

        var fp = Fingerprint(recipe);
        lock (Gate)
        {
            if (_model is not null && _fingerprint == fp)
                return _model;

            _model?.Dispose();
            _model = null;
            using var decoded = JlTemplateIo.DecodePng(recipe.Template.TemplateImageBase64);
            using var gray = JlImageConvert.ToGray(decoded);
            _model = JlShapeRefine.CreateModel(gray, recipe.Template);
            var off = JlPoseAlign.TeachHousingOffset(gray);
            _housingOffsetX = off.Ox;
            _housingOffsetY = off.Oy;
            _fingerprint = fp;
            return _model;
        }
    }

    public static (double Ox, double Oy) HousingOffset
    {
        get
        {
            lock (Gate)
                return (_housingOffsetX, _housingOffsetY);
        }
    }

    public static void Warm(RecipeConfig recipe)
    {
        if (recipe.Template.RefineMethod is not (SegmentRefineMethod.ShapeMatch or SegmentRefineMethod.Sift))
            return;
        GetOrCreate(recipe);
    }

    public static string TeachDiagnostics(RecipeConfig recipe)
    {
        try
        {
            return GetOrCreate(recipe) is not null
                ? "JLVision 形状模型已建立"
                : "JLVision 形状示教失败（无模板图）";
        }
        catch (Exception ex)
        {
            return "JLVision 形状示教失败：" + ex.Message;
        }
    }

    public static void Remove(string recipeName)
    {
        lock (Gate)
        {
            if (!_fingerprint.StartsWith(recipeName + "|", StringComparison.Ordinal))
                return;
            _model?.Dispose();
            _model = null;
            _fingerprint = "";
            _housingOffsetX = 0;
            _housingOffsetY = 0;
        }
    }

    private static string Fingerprint(RecipeConfig recipe)
    {
        var hash = SHA256.HashData(Encoding.ASCII.GetBytes(recipe.Template.TemplateImageBase64));
        return recipe.Name + "|" + Convert.ToHexString(hash)
            + $"|{(int)recipe.Template.ShapeMatchMetric}|{recipe.Template.ShapeMatchMinContrast:G9}";
    }
}
