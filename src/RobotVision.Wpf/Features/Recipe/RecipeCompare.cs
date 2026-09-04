using System.Text.Json;
using System.Text.Json.Serialization;
using RobotVision.Core.Models;
using RobotVision.Core.Recipe;

namespace RobotVision.WpfHost.Features.Recipe;

/// <summary>
/// 配方比较（脏标记）：模板图单独比字符串，其余字段 JSON 序列化比对（不含 <see cref="TemplateOptions.TemplateImageBase64"/>）。
/// </summary>
internal static class RecipeCompare
{
    private static readonly JsonSerializerOptions Options = new()
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public static string BodyFingerprint(RecipeConfig recipe) =>
        JsonSerializer.Serialize(recipe.Clone(includeTemplateImage: false), Options);

    public static bool Same(RecipeConfig a, RecipeConfig b) =>
        string.Equals(a.Template.TemplateImageBase64, b.Template.TemplateImageBase64, StringComparison.Ordinal)
        && string.Equals(a.DualTemplate.TemplateABase64, b.DualTemplate.TemplateABase64, StringComparison.Ordinal)
        && string.Equals(a.DualTemplate.TemplateBBase64, b.DualTemplate.TemplateBBase64, StringComparison.Ordinal)
        && BodyFingerprint(a) == BodyFingerprint(b);

    /// <summary>精修方法 / 转正窗 / 特征框变化会改抓取原点（模板峰 vs 卡尺中线等）。</summary>
    public static bool GrabOriginChanged(RecipeConfig a, RecipeConfig b) =>
        a.AngleMode != b.AngleMode ||
        a.Template.RefineMethod != b.Template.RefineMethod ||
        a.Template.UseUprightCrop != b.Template.UseUprightCrop ||
        !SameRoi(a.Template.Roi, b.Template.Roi);

    public static bool SameRoi(Roi? a, Roi? b) =>
        a is null && b is null ||
        a is not null && b is not null &&
        Math.Abs(a.X - b.X) < 1e-4 &&
        Math.Abs(a.Y - b.Y) < 1e-4 &&
        Math.Abs(a.Width - b.Width) < 1e-4 &&
        Math.Abs(a.Height - b.Height) < 1e-4;
}
