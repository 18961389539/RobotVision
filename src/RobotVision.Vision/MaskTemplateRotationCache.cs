using System.Diagnostics.CodeAnalysis;
using OpenCvSharp;
using RobotVision.Core.Recipe;

namespace RobotVision.Vision.Inference.Strategies;

/// <summary>
/// 按配方当前 <c>refineRangeDeg</c> 预旋转的模板库（1° 网格，0°±range ∪ 180°±range）。
/// 覆盖粗搜 2° 与精搜 ±1°；亚度插值仍用分数抛物线，不缓存非整数角。
/// 条目所有权在本对象，匹配时只借阅不得 Dispose。
/// </summary>
public sealed class RotatedTemplateBank : IDisposable
{
    private readonly List<(double Deg, Mat Image)> _items;
    private bool _disposed;

    internal RotatedTemplateBank(Mat source, double refineRangeDeg, List<(double Deg, Mat Image)> items)
    {
        Source = source;
        RefineRangeDeg = refineRangeDeg;
        _items = items;
    }

    /// <summary>未旋转的示教模板（BGR）。</summary>
    public Mat Source { get; }

    public double RefineRangeDeg { get; }

    public int Count => _items.Count;

    public bool TryGet(double deg, out Mat image)
    {
        foreach (var (d, m) in _items)
        {
            if (Math.Abs(d - deg) < 1e-6)
            {
                image = m;
                return true;
            }
        }
        image = null!;
        return false;
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        Source.Dispose();
        foreach (var (_, image) in _items)
            image.Dispose();
    }
}

/// <summary>灰度（BGR）旋转库 + 可选边缘图旋转库（UseEdgeMatch）。</summary>
public sealed record MaskTemplateRotationPack(RotatedTemplateBank Gray, RotatedTemplateBank? Edge);

/// <summary>
/// 按配方名缓存旋转模板。加载/保存时 <see cref="Warm"/>，匹配时 <see cref="GetOrCreate"/>。
/// 进程内默认实例 <see cref="Shared"/> 供策略与配方加载器共用。
/// </summary>
public sealed class MaskTemplateRotationCache : IRecipeTeachCache, IDisposable
{
    public static MaskTemplateRotationCache Shared { get; } = new();

    private readonly RecipeTeachCache<MaskTemplateRotationPack> _items = new(
        ShouldCache,
        RecipeTeachFingerprints.RotationPack,
        recipe => BuildPack(recipe.Template),
        DisposePack);

    /// <summary>配方加载/保存时预热。非模板匹配或未示教则跳过。预热不占用匹配租约。</summary>
    public void Warm(RecipeConfig recipe) => _items.Warm(recipe);

    public void Remove(string recipeName) => _items.Remove(recipeName);

    /// <summary>归还 <see cref="GetOrCreate"/> 租约。退役条目在租约归零后才释放 Mat。</summary>
    public void Release(MaskTemplateRotationPack? pack) => _items.Release(pack);

    /// <summary>命中已有指纹则复用；范围或模板变更则重建。调用方必须 <see cref="Release"/>，不得 Dispose 返回的 Mat。</summary>
    public MaskTemplateRotationPack? GetOrCreate(RecipeConfig recipe) => _items.GetOrCreate(recipe);

    public void Dispose() => _items.Dispose();

    private static bool ShouldCache(RecipeConfig recipe) =>
        recipe.AngleMode == AngleMode.MaskTemplate
        && recipe.Template.RefineMethod == SegmentRefineMethod.Template
        && recipe.Template.UseUprightCrop
        && !string.IsNullOrEmpty(recipe.Template.TemplateImageBase64);

    [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope",
        Justification = "Rotation bank ownership transfers to MaskTemplateRotationPack.")]
    private static MaskTemplateRotationPack BuildPack(TemplateOptions template)
    {
        using var decoded = MaskTemplateMatcher.DecodeTemplatePng(template.TemplateImageBase64);
        var gray = MaskTemplateMatcher.CreateRotationBank(decoded, template.RefineRangeDeg);
        RotatedTemplateBank? edge = null;
        if (template.UseEdgeMatch)
        {
            using var edges = MaskTemplateMatcher.ToEdgeMap(decoded);
            edge = MaskTemplateMatcher.CreateRotationBank(edges, template.RefineRangeDeg);
        }
        return new MaskTemplateRotationPack(gray, edge);
    }

    private static void DisposePack(MaskTemplateRotationPack pack)
    {
        pack.Gray.Dispose();
        pack.Edge?.Dispose();
    }
}
