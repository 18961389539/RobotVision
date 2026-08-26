using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using RobotVision.Core.Models;

namespace RobotVision.Core.Recipe;

public sealed class RecipeNotFoundException(string name)
    : Exception($"配方不存在: {name}");

public sealed class InvalidRecipeException(
    string name,
    string reason,
    VisionErrorCode errorCode = VisionErrorCode.InvalidRecipeConfig)
    : Exception($"配方 {name} 无效: {reason}")
{
    public VisionErrorCode ErrorCode { get; } = errorCode;
}

/// <summary>配方引用完整性失败（相机/光源/模型/标定），带协议错误码。</summary>
public readonly record struct RecipeReferenceError(string Message, VisionErrorCode Code);

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

    /// <summary>缓存条目：配方 + 文件最后修改时间与长度。
    /// 长度参与比对：文件系统时间戳精度有限（FAT 2s），同秒内"改两次"或"改回原时间戳"的
    /// 场景单靠时间戳会误判未变化，长度不同必然失效。</summary>
    private sealed record CachedRecipe(RecipeConfig Recipe, DateTime LastWriteUtc, long Length);

    private readonly ConcurrentDictionary<string, CachedRecipe> _cache = new(StringComparer.OrdinalIgnoreCase);

    public int LoadedCount => _cache.Count;

    /// <summary>
    /// 引用完整性校验器（由组装层注入，联动相机/模型/标定管理器）。
    /// 返回 null 表示通过；返回错误时抛带协议码的 InvalidRecipeException。
    /// 纯值域校验见 <see cref="Validate"/>，两者互补。
    /// </summary>
    public Func<RecipeConfig, RecipeReferenceError?>? ReferenceValidator { get; set; }

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
        catch (IOException ex)
        {
            // 文件锁/磁盘满/网络盘瞬断是 IO 故障而非"配方不存在"——伪装成 1001 会把
            // PLC 与现场排障导向"配方名错了"的错误方向；按内部错误（1099）上报
            throw new InvalidRecipeException(name, $"读取配方文件失败（IO）: {ex.Message}",
                VisionErrorCode.InternalError);
        }
    }

    private RecipeConfig GetCore(string name, string path, bool forceReload)
    {
        if (!File.Exists(path))
            throw new RecipeNotFoundException(name);

        // 文件未变化时走缓存；文件被外部工具改动后按 LastWriteTime+Length 自动失效重读
        var info = new FileInfo(path);
        if (!forceReload && _cache.TryGetValue(name, out var cached) &&
            cached.LastWriteUtc == info.LastWriteTimeUtc && cached.Length == info.Length)
        {
            var hit = cached.Recipe.Clone();
            // 标定/相机热删除不改配方文件：每次命中仍做引用校验，避免缓存里的「有效」配方继续上线
            ValidateReferences(hit);
            return hit;
        }

        var recipe = JsonSerializer.Deserialize<RecipeConfig>(File.ReadAllText(path), JsonOptions)
            ?? throw new RecipeNotFoundException(name);
        recipe.Name = name;
        Validate(recipe);
        ValidateReferences(recipe);

        _cache[name] = new CachedRecipe(recipe, info.LastWriteTimeUtc, info.Length);
        return recipe.Clone(); // 首次加载同样返回克隆，与缓存命中路径行为一致
    }

    /// <summary>引用完整性校验（注入校验器时执行）：相机/模型/外参/旋转中心是否存在。</summary>
    public void ValidateReferences(RecipeConfig recipe)
    {
        if (ReferenceValidator is not { } validate)
            return;

        var error = validate(recipe);
        if (error is not null)
            throw new InvalidRecipeException(recipe.Name, error.Value.Message, error.Value.Code);
    }

    /// <summary>启动期或加载时校验，把配置错误拦在进入产线之前。</summary>
    public static void Validate(RecipeConfig recipe)
    {
        var name = recipe.Name;

        if (recipe.SerialNumber < 0)
            throw new InvalidRecipeException(name, "serialNumber 不能为负");

        if (string.IsNullOrWhiteSpace(recipe.CameraId))
            throw new InvalidRecipeException(name, "cameraId 为空");

        // 偏心工具补偿依赖 stationId 查旋转中心档案：缺 stationId 时补偿会被静默跳过
        // （配置声称补偿、实际没补偿），比直接报错危险——加载期拦截
        if (recipe.RotationCompensation == RotationCompensationMode.EccentricTool &&
            string.IsNullOrWhiteSpace(recipe.StationId))
            throw new InvalidRecipeException(name,
                "rotationCompensation=EccentricTool 必须配置 stationId（旋转中心档案按工位查找）");

        // 双BLOB模式纯图像处理、不使用模型：跳过一切模型数量校验（多配的模型静默忽略）
        var isBlobMode = recipe.AngleMode == AngleMode.DualBlobCenterLine;

        if (recipe.Models.Count == 0 && !isBlobMode)
            throw new InvalidRecipeException(name, "models 列表为空");

        // 单模型模式（MaskMinAreaRect/KeyPointLine/MaskTemplate）只使用 Models[0]，多配会静默忽略多余模型，
        // 收紧为恰好 1 个：多余模型既浪费内存又掩盖配方错误
        if ((recipe.AngleMode is AngleMode.MaskMinAreaRect or AngleMode.KeyPointLine or AngleMode.MaskTemplate) &&
            recipe.Models.Count != 1)
            throw new InvalidRecipeException(name,
                $"单模型模式（{recipe.AngleMode}）需要恰好 1 个模型（当前 {recipe.Models.Count}）");

        if (recipe.AngleMode == AngleMode.MaskTemplate &&
            recipe.Template.RefineMethod == SegmentRefineMethod.Template &&
            string.IsNullOrEmpty(recipe.Template.TemplateImageBase64))
            throw new InvalidRecipeException(name, "分割+精修（模板匹配方法）未示教模板（配方页「示教模板」自动生成，或改用直线拟合方法）");

        if (recipe.AngleMode == AngleMode.MaskTemplate)
        {
            if (recipe.Template.MatchThreshold is < 0 or > 1)
                throw new InvalidRecipeException(name, "template.matchThreshold 必须在 [0,1]");
            if (recipe.Template.RefineRangeDeg is <= 0 or > 45)
                throw new InvalidRecipeException(name, "template.refineRangeDeg 必须在 (0,45]");
        }

        if (recipe.AngleMode == AngleMode.DualCenterLine && recipe.Models.Count < 2)
            throw new InvalidRecipeException(name, "双模型模式（DualCenterLine）需要 2 个模型");

        if (recipe.AngleMode == AngleMode.DualCenterLine && recipe.Models.Count > 2)
            throw new InvalidRecipeException(name, "双模型模式（DualCenterLine）最多 2 个模型");

        if (recipe.AngleMode == AngleMode.DualCenterLine &&
            recipe.DualModel.CropWindowPairing &&
            recipe.DualModel.CropExpandRatio is <= 0 or > 5)
            throw new InvalidRecipeException(name, "dualModel.cropExpandRatio 必须在 (0,5]");

        if (recipe.Models.Count > 2 && !isBlobMode)
            throw new InvalidRecipeException(name, "models 最多 2 个");

        if (isBlobMode)
        {
            var blob = recipe.Blob;
            if (blob.Threshold is < 0 or > 255)
                throw new InvalidRecipeException(name, "blob.threshold 必须在 [0,255]");
            if (blob.MinArea < 1)
                throw new InvalidRecipeException(name, "blob.minArea 必须 ≥1");
            if (blob.MaxArea < blob.MinArea)
                throw new InvalidRecipeException(name, "blob.maxArea 不能小于 blob.minArea");
            if (blob.SecondaryMinArea < 1)
                throw new InvalidRecipeException(name, "blob.secondaryMinArea 必须 ≥1");
            if (blob.SecondaryMaxArea < blob.SecondaryMinArea)
                throw new InvalidRecipeException(name, "blob.secondaryMaxArea 不能小于 blob.secondaryMinArea");
            if (blob.CropExpandRatio is <= 0 or > 5)
                throw new InvalidRecipeException(name, "blob.cropExpandRatio 必须在 (0,5]");
            if (blob.MinPairDistancePx < 0)
                throw new InvalidRecipeException(name, "blob.minPairDistancePx 不能为负");
            if (blob.MaxPairDistancePx <= blob.MinPairDistancePx)
                throw new InvalidRecipeException(name, "blob.maxPairDistancePx 必须大于 blob.minPairDistancePx");
            if (blob.OpenKernelSize is < 0 or > 31)
                throw new InvalidRecipeException(name, "blob.openKernelSize 必须在 [0,31]");
        }

        if (recipe.Confidence is < 0 or > 1)
            throw new InvalidRecipeException(name, "confidence 必须在 [0,1]");

        if (recipe.Iou is < 0 or > 1)
            throw new InvalidRecipeException(name, "iou 必须在 [0,1]");

        if (recipe.AngleMode == AngleMode.KeyPointLine && recipe.Keypoint.IndexA == recipe.Keypoint.IndexB)
            throw new InvalidRecipeException(name, "keypoint.indexA 与 keypoint.indexB 不能相同");

        if (recipe.Keypoint.IndexA < 0 || recipe.Keypoint.IndexB < 0)
            throw new InvalidRecipeException(name, "keypoint 索引不能为负");

        if (recipe.Roi is { } roi)
        {
            if (roi.X is < 0 or > 1 || roi.Y is < 0 or > 1 ||
                roi.Width is <= 0 or > 1 || roi.Height is <= 0 or > 1)
                throw new InvalidRecipeException(name, "roi 的 X/Y 必须在 [0,1]，Width/Height 必须在 (0,1]");
            // 1e-9 容差：像素↔比例往返换算可能产生 1.0000000000000002 这类浮点毛刺
            if (roi.X + roi.Width > 1 + 1e-9 || roi.Y + roi.Height > 1 + 1e-9)
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

    /// <summary>启动时预加载全部配方，加载失败抛异常并列出所有问题（按文件名排序，日志顺序确定）。</summary>
    public IReadOnlyList<(string Name, string Error)> LoadAll()
    {
        var errors = new List<(string, string)>();
        if (!Directory.Exists(folder))
            return errors;

        foreach (var file in Directory.EnumerateFiles(folder, "*.json")
                     .OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
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

    /// <summary>
    /// 将 TCP 触发行首段（配方名或序列号）解析为配方名。
    /// 纯数字（或 # 前缀）优先按序列号查找；未命中时再按配方名（支持名称为纯数字的配方）。
    /// </summary>
    public (string? RecipeName, string? Error) ResolveTriggerKey(string key)
    {
        key = NormalizeTriggerKey(key);
        if (key.Length == 0)
            return (null, "MISSING_RECIPE");

        if (IsSerialKey(key))
        {
            if (!int.TryParse(key, NumberStyles.None, CultureInfo.InvariantCulture, out var serial) || serial <= 0)
                return (null, "INVALID_SERIAL");

            var bySerial = FindNameBySerial(serial);
            if (bySerial is not null)
                return (bySerial, null);

            if (IsValidRecipeName(key) && FileExists(key))
                return (key, null);

            return (null, "UNKNOWN_SERIAL");
        }

        if (!IsValidRecipeName(key))
            return (null, "INVALID_RECIPE_NAME");

        if (!FileExists(key))
            return (null, "UNKNOWN_RECIPE");

        return (key, null);
    }

    private static string NormalizeTriggerKey(string key)
    {
        key = key.Trim();
        if (key.StartsWith('#') && key.Length > 1)
            key = key[1..].Trim();
        return key;
    }

    private static bool IsSerialKey(string key) =>
        key.Length > 0 && key.All(char.IsAsciiDigit);

    private string? FindNameBySerial(int serial)
    {
        foreach (var name in ListNames())
        {
            if (Get(name).SerialNumber == serial)
                return name;
        }

        return null;
    }

    public string Folder => folder;

    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>
    /// 校验并写回配方文件，同时更新缓存（缓存存副本，编辑器后续修改不污染缓存）。
    /// 供管理界面保存使用；文件由程序规范化生成（注释丢失属预期）。
    /// 原子落盘（临时文件 + File.Replace）：写一半崩溃不留截断 JSON，
    /// 且 TCP 线程并发 Get 不会读到半写文件（替换对读者近似原子）。
    /// </summary>
    /// <param name="previousName">
    /// 改名前的磁盘文件名。与 <paramref name="recipe"/> 的 Name 不同时，写入成功后删除旧文件
    ///（移动语义，避免列表出现「旧名 + 新名」两条）。新建/复制传 null 或空，不删任何已有配方。
    /// </param>
    public void Save(RecipeConfig recipe, string? previousName = null)
    {
        if (!IsValidRecipeName(recipe.Name))
            throw new InvalidRecipeException(recipe.Name, "名称只允许字母、数字、下划线、中划线");

        Validate(recipe);
        ValidateReferences(recipe);
        EnsureUniqueSerialNumber(recipe, previousName);

        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, recipe.Name + ".json");
        AtomicWriteAllText(path, JsonSerializer.Serialize(recipe, WriteOptions));

        var info = new FileInfo(path);
        _cache[recipe.Name] = new CachedRecipe(recipe.Clone(), info.LastWriteTimeUtc, info.Length);

        // 先写新文件再删旧文件：写失败时旧配方仍在。复制/新建不要传 previousName。
        if (string.IsNullOrEmpty(previousName) ||
            string.Equals(previousName, recipe.Name, StringComparison.OrdinalIgnoreCase))
            return;

        if (!IsValidRecipeName(previousName))
            throw new InvalidRecipeException(recipe.Name, $"已写入 {recipe.Name}，但旧名 {previousName} 非法，未删除");

        if (FileExists(previousName) && !Delete(previousName))
            throw new InvalidRecipeException(recipe.Name,
                $"已写入 {recipe.Name}，但删除旧配方 {previousName} 失败");
    }

    private void EnsureUniqueSerialNumber(RecipeConfig recipe, string? previousName)
    {
        if (recipe.SerialNumber <= 0)
            return;

        foreach (var other in ListNames())
        {
            // 自身文件名、以及即将被替换删除的旧名，都不占序列号
            if (string.Equals(other, recipe.Name, StringComparison.OrdinalIgnoreCase) ||
                (!string.IsNullOrEmpty(previousName) &&
                 string.Equals(other, previousName, StringComparison.OrdinalIgnoreCase)))
                continue;
            RecipeConfig loaded;
            try
            {
                loaded = Get(other);
            }
            catch (Exception)
            {
                // 损坏/无法加载的配方不参与序列号占用判断，否则保存有效配方会被其拖死
                continue;
            }

            if (loaded.SerialNumber == recipe.SerialNumber)
                throw new InvalidRecipeException(recipe.Name,
                    $"serialNumber {recipe.SerialNumber} 已被配方 {other} 占用");
        }
    }

    /// <summary>原子写（临时文件 + 替换）：配方是产线关键资产，与标定档案同策略。
    /// Core 层不依赖 Infrastructure 的实现，此处独立实现同语义。</summary>
    private static void AtomicWriteAllText(string path, string content)
    {
        var full = Path.GetFullPath(path);
        var dir = Path.GetDirectoryName(full)!;
        Directory.CreateDirectory(dir);
        var tmp = Path.Combine(dir, $".{Path.GetFileName(full)}.{Environment.ProcessId}.{Guid.NewGuid():N}.tmp");
        try
        {
            File.WriteAllText(tmp, content);
            if (File.Exists(full))
                File.Replace(tmp, full, null);
            else
                File.Move(tmp, full);
        }
        finally
        {
            try { File.Delete(tmp); }
            catch (IOException) { }
        }
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
