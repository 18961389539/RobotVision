using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace RobotVision.Core.Recipe;

public sealed class RecipeNotFoundException(string name)
    : Exception($"配方不存在: {name}");

public sealed class InvalidRecipeException(string name, string reason)
    : Exception($"配方 {name} 无效: {reason}");

/// <summary>从配方目录加载 JSON 配方并缓存。配方文件名即配方名（{name}.json）。</summary>
public sealed class RecipeLoader(string folder)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>缓存条目：配方 + 文件最后修改时间。文件被外部改动后按时间戳自动失效重读。</summary>
    private sealed record CachedRecipe(RecipeConfig Recipe, DateTime LastWriteUtc);

    private readonly ConcurrentDictionary<string, CachedRecipe> _cache = new(StringComparer.OrdinalIgnoreCase);

    public int LoadedCount => _cache.Count;

    /// <summary>
    /// 引用完整性校验器（由组装层注入，联动相机/模型/标定管理器）。
    /// 返回 null 表示通过；返回错误消息时抛 InvalidRecipeException。
    /// 纯值域校验见 <see cref="Validate"/>，两者互补。
    /// </summary>
    public Func<RecipeConfig, string?>? ReferenceValidator { get; set; }

    /// <summary>配方文件是否已存在（供 UI 覆盖确认）。</summary>
    public bool FileExists(string name) =>
        IsValidRecipeName(name) && File.Exists(Path.Combine(folder, name + ".json"));

    /// <summary>
    /// 配方名只允许字母、数字、下划线、中划线。
    /// TCP 服务监听 0.0.0.0，任意网段客户端可用 TRIGGER,..\..\x 探测文件系统，必须拦截。
    /// </summary>
    public static bool IsValidRecipeName(string name) =>
        name.Length > 0 && name.Length <= 64 &&
        name.All(c => char.IsAsciiLetterOrDigit(c) || c is '_' or '-');

    public RecipeConfig Get(string name, bool forceReload = false)
    {
        if (!IsValidRecipeName(name))
            throw new RecipeNotFoundException(name);

        var path = Path.Combine(folder, name + ".json");
        try
        {
            return GetCore(name, path, forceReload);
        }
        catch (FileNotFoundException)
        {
            // TOCTOU：File.Exists → GetLastWriteTimeUtc → ReadAllText 之间文件被删，
            // 转换为业务异常而不是把 IO 异常冒成 1099 InternalError
            throw new RecipeNotFoundException(name);
        }
        catch (DirectoryNotFoundException)
        {
            throw new RecipeNotFoundException(name);
        }
        catch (IOException)
        {
            throw new RecipeNotFoundException(name);
        }
    }

    private RecipeConfig GetCore(string name, string path, bool forceReload)
    {
        if (!File.Exists(path))
            throw new RecipeNotFoundException(name);

        // 文件未变化时走缓存；文件被外部工具改动后按 LastWriteTime 自动失效重读
        var lastWriteUtc = File.GetLastWriteTimeUtc(path);
        if (!forceReload && _cache.TryGetValue(name, out var cached) && cached.LastWriteUtc == lastWriteUtc)
            return cached.Recipe.Clone(); // 缓存存本体、返回克隆：调用方改动不污染共享缓存

        var recipe = JsonSerializer.Deserialize<RecipeConfig>(File.ReadAllText(path), JsonOptions)
            ?? throw new RecipeNotFoundException(name);
        recipe.Name = name;
        Validate(recipe);
        ValidateReferences(recipe);

        _cache[name] = new CachedRecipe(recipe, lastWriteUtc);
        return recipe.Clone(); // 首次加载同样返回克隆，与缓存命中路径行为一致
    }

    /// <summary>引用完整性校验（注入校验器时执行）：相机/模型/外参/旋转中心是否存在。</summary>
    public void ValidateReferences(RecipeConfig recipe)
    {
        if (ReferenceValidator is not { } validate)
            return;

        var error = validate(recipe);
        if (error is not null)
            throw new InvalidRecipeException(recipe.Name, error);
    }

    /// <summary>启动期或加载时校验，把配置错误拦在进入产线之前。</summary>
    public static void Validate(RecipeConfig recipe)
    {
        var name = recipe.Name;

        if (string.IsNullOrWhiteSpace(recipe.CameraId))
            throw new InvalidRecipeException(name, "cameraId 为空");

        if (recipe.Models.Count == 0)
            throw new InvalidRecipeException(name, "models 列表为空");

        // 单模型模式（MaskMinAreaRect/KeyPointLine）只使用 Models[0]，多配会静默忽略多余模型，
        // 收紧为恰好 1 个：多余模型既浪费内存又掩盖配方错误
        if ((recipe.AngleMode is AngleMode.MaskMinAreaRect or AngleMode.KeyPointLine) &&
            recipe.Models.Count != 1)
            throw new InvalidRecipeException(name,
                $"单模型模式（{recipe.AngleMode}）需要恰好 1 个模型（当前 {recipe.Models.Count}）");

        if (recipe.AngleMode == AngleMode.DualCenterLine && recipe.Models.Count < 2)
            throw new InvalidRecipeException(name, "双模型模式（DualCenterLine）需要 2 个模型");

        if (recipe.AngleMode == AngleMode.DualCenterLine && recipe.Models.Count > 2)
            throw new InvalidRecipeException(name, "双模型模式（DualCenterLine）最多 2 个模型");

        if (recipe.Models.Count > 2)
            throw new InvalidRecipeException(name, "models 最多 2 个");

        if (recipe.Confidence is < 0 or > 1)
            throw new InvalidRecipeException(name, "confidence 必须在 [0,1]");

        if (recipe.Iou is < 0 or > 1)
            throw new InvalidRecipeException(name, "iou 必须在 [0,1]");

        if (recipe.AngleMode == AngleMode.KeyPointLine && recipe.KeypointIndexA == recipe.KeypointIndexB)
            throw new InvalidRecipeException(name, "keypointIndexA 与 keypointIndexB 不能相同");

        if (recipe.KeypointIndexA < 0 || recipe.KeypointIndexB < 0)
            throw new InvalidRecipeException(name, "keypoint 索引不能为负");

        if (recipe.Roi is { } roi)
        {
            if (roi.X is < 0 or > 1 || roi.Y is < 0 or > 1 ||
                roi.Width is <= 0 or > 1 || roi.Height is <= 0 or > 1)
                throw new InvalidRecipeException(name, "roi 的 X/Y 必须在 [0,1]，Width/Height 必须在 (0,1]");
            if (roi.X + roi.Width > 1 || roi.Y + roi.Height > 1)
                throw new InvalidRecipeException(name, "roi 超出图像范围（X+Width/Y+Height 不能超过 1）");
        }

        ValidateLighting(recipe);
    }

    /// <summary>
    /// 照明配置值域校验：lightControllerId 与 lighting 必须成对出现，
    /// 通道号 ≥1、亮度 [0,255]、稳定延时非负。
    /// </summary>
    private static void ValidateLighting(RecipeConfig recipe)
    {
        var name = recipe.Name;

        if (!string.IsNullOrEmpty(recipe.LightControllerId) && recipe.Lighting is null)
            throw new InvalidRecipeException(name, "指定了 lightControllerId 但缺少 lighting 配置段");

        if (recipe.Lighting is not { } lighting)
            return;

        if (string.IsNullOrWhiteSpace(recipe.LightControllerId))
            throw new InvalidRecipeException(name, "配置了 lighting 但缺少 lightControllerId");

        if (lighting.StabilizeDelayMs < 0)
            throw new InvalidRecipeException(name, "lighting.stabilizeDelayMs 不能为负");

        foreach (var channel in lighting.Channels)
        {
            if (channel.Channel < 1)
                throw new InvalidRecipeException(name, $"lighting 通道号必须 ≥1（当前 {channel.Channel}）");
            if (channel.Brightness is < 0 or > 255)
                throw new InvalidRecipeException(name, $"lighting 亮度必须在 [0,255]（通道 {channel.Channel}）");
        }
    }

    /// <summary>启动时预加载全部配方，加载失败抛异常并列出所有问题。</summary>
    public IReadOnlyList<(string Name, string Error)> LoadAll()
    {
        var errors = new List<(string, string)>();
        if (!Directory.Exists(folder))
            return errors;

        foreach (var file in Directory.EnumerateFiles(folder, "*.json"))
        {
            var name = Path.GetFileNameWithoutExtension(file);
            try
            {
                Get(name);
            }
            catch (Exception ex)
            {
                errors.Add((name, ex.Message));
            }
        }
        return errors;
    }

    /// <summary>列出配方目录中的全部配方名（仅合法名，供 UI 选择）。</summary>
    public IReadOnlyList<string> ListNames() =>
        Directory.Exists(folder)
            ? Directory.EnumerateFiles(folder, "*.json")
                .Select(f => Path.GetFileNameWithoutExtension(f) ?? "")
                .Where(IsValidRecipeName) // 过滤含空格等非法字符的文件名，避免 UI 列出无法操作的配方
                .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                .ToList()
            : [];

    public string Folder => folder;

    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>
    /// 校验并写回配方文件，同时更新缓存（缓存存副本，编辑器后续修改不污染缓存）。
    /// 供管理界面保存使用；文件由程序规范化生成（注释丢失属预期）。
    /// </summary>
    public void Save(RecipeConfig recipe)
    {
        if (!IsValidRecipeName(recipe.Name))
            throw new InvalidRecipeException(recipe.Name, "名称只允许字母、数字、下划线、中划线");

        Validate(recipe);
        ValidateReferences(recipe);

        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, recipe.Name + ".json");
        File.WriteAllText(path, JsonSerializer.Serialize(recipe, WriteOptions));

        _cache[recipe.Name] = new CachedRecipe(recipe.Clone(), File.GetLastWriteTimeUtc(path));
    }

    /// <summary>删除配方文件并移除缓存。文件不存在时仍清理缓存并返回 false。</summary>
    public bool Delete(string name)
    {
        if (!IsValidRecipeName(name))
            return false;

        _cache.TryRemove(name, out _);
        var path = Path.Combine(folder, name + ".json");
        if (!File.Exists(path))
            return false;

        File.Delete(path);
        return true;
    }
}
