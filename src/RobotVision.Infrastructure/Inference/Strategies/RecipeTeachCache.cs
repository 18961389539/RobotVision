using System.Security.Cryptography;
using System.Text;
using RobotVision.Core.Recipe;

namespace RobotVision.Infrastructure.Inference.Strategies;

internal interface IRecipeTeachCache
{
    void Warm(RecipeConfig recipe);
    void Remove(string recipeName);
}

internal static class RecipeTeachFingerprints
{
    public static string TemplateImage(TemplateOptions template)
    {
        var hash = SHA256.HashData(Encoding.ASCII.GetBytes(template.TemplateImageBase64));
        return Convert.ToHexString(hash);
    }

    public static string RotationPack(TemplateOptions template) =>
        $"{template.RefineRangeDeg:G17}|{(template.UseEdgeMatch ? 1 : 0)}|{TemplateImage(template)}";
}

/// <summary>
/// 按配方名缓存示教产物：指纹失效则重建；<see cref="GetOrCreate"/> 加租约，
/// <see cref="Release"/> 归还，Remove / Dispose 在租约归零后才释放本机资源。
/// </summary>
internal sealed class RecipeTeachCache<TValue> : IRecipeTeachCache, IDisposable
    where TValue : class
{
    private readonly Func<RecipeConfig, bool> _shouldCache;
    private readonly Func<TemplateOptions, string> _fingerprint;
    private readonly Func<RecipeConfig, TValue?> _build;
    private readonly LeasedCache<TValue> _items;
    private bool _disposed;

    public RecipeTeachCache(
        Func<RecipeConfig, bool> shouldCache,
        Func<TemplateOptions, string> fingerprint,
        Func<RecipeConfig, TValue?> build,
        Action<TValue> dispose)
    {
        _shouldCache = shouldCache;
        _fingerprint = fingerprint;
        _build = build;
        _items = new LeasedCache<TValue>(dispose);
    }

    public void Warm(RecipeConfig recipe)
    {
        if (!_shouldCache(recipe))
            return;
        Release(GetOrCreate(recipe));
    }

    public TValue? GetOrCreate(RecipeConfig recipe)
    {
        if (!_shouldCache(recipe))
            return null;

        ObjectDisposedException.ThrowIf(_disposed, this);
        return _items.Acquire(recipe.Name, _fingerprint(recipe.Template), () => _build(recipe));
    }

    public void Release(TValue? value) => _items.Release(value);

    public void Remove(string recipeName) => _items.Remove(recipeName);

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _items.Dispose();
    }
}
