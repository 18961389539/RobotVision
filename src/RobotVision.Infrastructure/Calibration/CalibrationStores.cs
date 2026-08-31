using System.Collections.Concurrent;
using System.Text.Json;
using RobotVision.Core.IO;
using RobotVision.Core.Models;

namespace RobotVision.Infrastructure.Calibration;

/// <summary>标定档案共享状态与落盘基础设施。</summary>
internal sealed class CalibrationStores : IDisposable
{
    public static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public JsonProfileStore<ExtrinsicProfile> Extrinsics { get; } = new(ExtrinsicKind.Instance);
    public JsonProfileStore<RotationCenterProfile> RotationCenters { get; } = new(RotationCenterKind.Instance);
    public JsonProfileStore<PolynomialProfile> Polynomials { get; } = new(PolynomialKind.Instance);
    public JsonProfileStore<ScaleProfile> Scales { get; } = new(ScaleKind.Instance);

    public IntrinsicCalibrationService Intrinsics { get; }

    public ConcurrentQueue<string> QualityWarnings { get; } = new();

    public string? Folder { get; set; }

    public CalibrationStores() => Intrinsics = new IntrinsicCalibrationService(AddQualityWarning);

    public void AddQualityWarning(string message)
    {
        QualityWarnings.Enqueue(message);
        while (QualityWarnings.Count > CalibrationConstants.MaxQualityWarnings)
            QualityWarnings.TryDequeue(out _);
    }

    public void RequireFolder()
    {
        if (string.IsNullOrEmpty(Folder))
            throw new InvalidOperationException("标定目录未初始化（先调用 LoadDirectory）");
    }

    public string ProfileFile(string kind, string id)
    {
        ValidateProfileId(id);
        return Path.Combine(Folder!, $"{id}.{kind}.json");
    }

    public void WriteJson(string path, object profile)
    {
        Directory.CreateDirectory(Folder!);
        AtomicFile.WriteAllText(path, JsonSerializer.Serialize(profile, JsonOptions));
    }

    public bool DeleteProfileFile(string id, string kind)
    {
        if (string.IsNullOrEmpty(Folder))
            return false;
        if (string.IsNullOrWhiteSpace(id) || id is "." or ".." ||
            id.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || Path.GetFileName(id) != id)
            return false;
        var file = Path.Combine(Folder, $"{id}.{kind}.json");
        if (!File.Exists(file))
            return false;
        File.Delete(file);
        return true;
    }

    public static void ValidateProfileId(string id)
    {
        if (string.IsNullOrWhiteSpace(id) || id is "." or ".." ||
            id.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || Path.GetFileName(id) != id)
            throw new InvalidDataException($"档案 Id 非法: {id}");
    }

    public void Dispose() => Intrinsics.Dispose();
}
