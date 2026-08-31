using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using RobotVision.Core.Assets;
using RobotVision.Core.Models;
using RobotVision.Core.Recipe;
using RobotVision.Infrastructure.Calibration;
using RobotVision.Infrastructure.Inference;

namespace RobotVision.Hosting;

/// <summary>
/// TRIGGER 前核对配方钉扎的模型/工位哈希，以及可选的 models/manifest.json。
/// 不在配方加载期拦截，以便界面仍能打开配方重新钉扎。
/// </summary>
public sealed class AssetIntegrityChecker(
    AppConfig cfg,
    ModelManager models,
    CalibrationManager calibration,
    ILogger<AssetIntegrityChecker> log)
{
    private readonly ConcurrentDictionary<string, byte> _unpinnedWarned = new(StringComparer.OrdinalIgnoreCase);

    public bool IsEnabled => cfg.AssetIntegrity.Enabled;

    /// <summary>通过返回 null；失败返回 1017 消息（ASCII，可上协议线）。</summary>
    public string? Check(RecipeConfig recipe)
    {
        if (!cfg.AssetIntegrity.Enabled)
            return null;

        IReadOnlyDictionary<string, string>? manifest = null;
        if (cfg.AssetIntegrity.RequireManifest)
        {
            var store = new ModelManifestStore(models.ModelsFolder);
            if (!File.Exists(store.ManifestPath))
                return "MANIFEST_MISSING";
            try
            {
                manifest = store.Load();
            }
            catch (Exception ex)
            {
                AssetIntegrityCheckerLog.ManifestReadFailed(log, ex);
                return "MANIFEST_INVALID";
            }
        }

        var isBlob = recipe.AngleMode == AngleMode.DualBlobCenterLine;
        if (!isBlob)
        {
            for (var i = 0; i < recipe.Models.Count; i++)
            {
                var file = recipe.Models[i];
                if (string.IsNullOrWhiteSpace(file))
                    continue;

                string actual;
                try
                {
                    actual = models.ComputeSha256(file);
                }
                catch (FileNotFoundException)
                {
                    // 文件缺失由引用校验/1005 处理
                    continue;
                }

                var pinned = i < recipe.ModelSha256.Count ? recipe.ModelSha256[i] : "";
                if (string.IsNullOrWhiteSpace(pinned))
                {
                    var warnKey = $"{recipe.Name}\0{file}";
                    if (_unpinnedWarned.TryAdd(warnKey, 0))
                    {
                        AssetIntegrityCheckerLog.ModelSha256NotPinned(log, recipe.Name, file);
                    }
                }
                else if (!FileSha256.EqualsHex(pinned, actual))
                    return "MODEL_HASH_MISMATCH";

                if (cfg.AssetIntegrity.RequireManifest && manifest is not null)
                {
                    var key = Path.GetFileName(file);
                    if (!manifest.TryGetValue(key, out var expected) ||
                        !FileSha256.EqualsHex(expected, actual))
                        return "MANIFEST_HASH_MISMATCH";
                }
            }
        }

        if (!string.IsNullOrWhiteSpace(recipe.StationSha256) &&
            !string.IsNullOrWhiteSpace(recipe.StationId))
        {
            var includeRotation = recipe.RotationCompensation == RotationCompensationMode.EccentricTool;
            var actual = calibration.ComputeStationSha256(
                recipe.StationId, includeRotation, recipe.CameraId);
            if (actual is null || !FileSha256.EqualsHex(recipe.StationSha256, actual))
                return "STATION_HASH_MISMATCH";
        }

        return null;
    }

    /// <summary>按当前磁盘/内存档案生成配方应写入的钉扎值（界面「钉死当前哈希」）。</summary>
    public (List<string> ModelHashes, string? StationHash) Snapshot(RecipeConfig recipe)
    {
        var hashes = new List<string>();
        if (recipe.AngleMode != AngleMode.DualBlobCenterLine)
        {
            foreach (var file in recipe.Models)
            {
                if (string.IsNullOrWhiteSpace(file) || !models.ModelFileExists(file))
                {
                    hashes.Add("");
                    continue;
                }

                hashes.Add(models.ComputeSha256(file));
            }
        }

        string? station = null;
        if (!string.IsNullOrWhiteSpace(recipe.StationId))
        {
            var includeRotation = recipe.RotationCompensation == RotationCompensationMode.EccentricTool;
            station = calibration.ComputeStationSha256(
                recipe.StationId, includeRotation, recipe.CameraId);
        }

        return (hashes, station);
    }
}
