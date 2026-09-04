using System.Security.Cryptography;
using System.Text;
using JLVisionLib;
using OpenCvSharp;
using RobotVision.Core.Recipe;

namespace RobotVision.JlVision;

/// <summary>按配方示教 PNG 缓存一份 JlNCCModel（进程内单槽）。</summary>
public static class JlNccTeachCache
{
    private static readonly object Gate = new();
    private static string _fingerprint = "";
    private static JlNCCModel? _model;
    private static int _templateWidth;
    private static int _templateHeight;

    public static JlNCCModel? GetOrCreate(RecipeConfig recipe)
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
            _templateWidth = gray.Width;
            _templateHeight = gray.Height;
            _model = JlNccRefine.CreateModel(gray);
            _fingerprint = fp;
            return _model;
        }
    }

    public static (int Width, int Height) TemplateSize
    {
        get
        {
            lock (Gate)
                return (_templateWidth, _templateHeight);
        }
    }

    public static void Warm(RecipeConfig recipe)
    {
        if (recipe.Template.RefineMethod != SegmentRefineMethod.Template)
            return;
        GetOrCreate(recipe);
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
            _templateWidth = 0;
            _templateHeight = 0;
        }
    }

    private static string Fingerprint(RecipeConfig recipe)
    {
        var hash = SHA256.HashData(Encoding.ASCII.GetBytes(recipe.Template.TemplateImageBase64));
        return recipe.Name + "|" + Convert.ToHexString(hash);
    }
}
