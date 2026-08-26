using System.Text.Json;
using RobotVision.Core.Assets;

namespace RobotVision.Infrastructure.Inference;

/// <summary>models/manifest.json 条目。</summary>
public sealed class ModelManifestEntry
{
    public string File { get; set; } = "";

    public string Sha256 { get; set; } = "";
}

/// <summary>models/manifest.json 根对象。</summary>
public sealed class ModelManifest
{
    public List<ModelManifestEntry> Models { get; set; } = [];
}

/// <summary>读取（可选写入）models/manifest.json：全局模型哈希清单。</summary>
public sealed class ModelManifestStore(string modelsFolder)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        WriteIndented = true,
    };

    public string ManifestPath => Path.Combine(modelsFolder, "manifest.json");

    /// <summary>文件名（大小写不敏感）→ 规范化 SHA-256。清单不存在返回空表。</summary>
    public IReadOnlyDictionary<string, string> Load()
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (!File.Exists(ManifestPath))
            return map;

        var manifest = JsonSerializer.Deserialize<ModelManifest>(File.ReadAllText(ManifestPath), JsonOptions);
        if (manifest is null)
            return map;

        foreach (var entry in manifest.Models)
        {
            if (string.IsNullOrWhiteSpace(entry.File) || !FileSha256.IsHex(entry.Sha256))
                continue;
            map[Path.GetFileName(entry.File)] = FileSha256.Normalize(entry.Sha256);
        }

        return map;
    }
}
