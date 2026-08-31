using System.Text.Json;
using RobotVision.Core.Models;

namespace RobotVision.Infrastructure.Calibration;

/// <summary>扫描标定目录并按种类载入档案（排序遍历 + Id 去重 + 坏档隔离）。</summary>
internal static class CalibrationDirectoryLoader
{
    public static IReadOnlyList<(string File, string Error)> Load(
        CalibrationStores stores,
        string folder,
        Action<IntrinsicProfile> loadIntrinsic,
        Action<ExtrinsicProfile> loadExtrinsic,
        Action<RotationCenterProfile> loadRotationCenter,
        Action<PolynomialProfile> loadPolynomial,
        Action<ScaleProfile> loadScale)
    {
        stores.Folder = folder;
        var errors = new List<(string, string)>();
        if (!Directory.Exists(folder))
            return errors;

        LoadKind(folder, "intrinsic", errors, stores.AddQualityWarning, p => p.CameraId, loadIntrinsic);
        LoadKind(folder, stores.Extrinsics.Kind, errors, stores.AddQualityWarning, stores.Extrinsics.IdOf, loadExtrinsic);
        LoadKind(folder, stores.RotationCenters.Kind, errors, stores.AddQualityWarning, stores.RotationCenters.IdOf, loadRotationCenter);
        LoadKind(folder, stores.Polynomials.Kind, errors, stores.AddQualityWarning, stores.Polynomials.IdOf, loadPolynomial);
        LoadKind(folder, stores.Scales.Kind, errors, stores.AddQualityWarning, stores.Scales.IdOf, loadScale);

        return errors;
    }

    private static void LoadKind<TProfile>(
        string folder,
        string kind,
        List<(string File, string Error)> errors,
        Action<string> warn,
        Func<TProfile, string> idOf,
        Action<TProfile> load)
        where TProfile : class
    {
        if (!Directory.Exists(folder))
            return;

        var seen = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in Directory.EnumerateFiles(folder, $"*.{kind}.json")
                     .OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                var profile = JsonSerializer.Deserialize<TProfile>(File.ReadAllText(file))
                    ?? throw new InvalidDataException("档案内容为空");
                var id = idOf(profile);
                if (seen.TryGetValue(id, out var firstFile))
                {
                    errors.Add((Path.GetFileName(file),
                        $"档案 Id 重复: {id} 已由 {Path.GetFileName(firstFile)} 加载（按文件名排序先者生效），请删除多余档案"));
                    continue;
                }
                load(profile);
                seen[id] = file;
                WarnIfFileNameMismatch(file, kind, id, warn);
            }
            catch (Exception ex)
            {
                errors.Add((Path.GetFileName(file), ex.Message));
            }
        }
    }

    private static void WarnIfFileNameMismatch(string file, string kind, string id, Action<string> warn)
    {
        var name = Path.GetFileName(file);
        var suffix = $".{kind}.json";
        if (!name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            return;
        var nameId = name[..^suffix.Length];
        if (!string.Equals(nameId, id, StringComparison.OrdinalIgnoreCase))
            warn($"档案文件名 {name} 与内部 Id \"{id}\" 不一致：保存时将按 Id 写出新文件，请重命名或删除旧文件");
    }
}
