using System.Text.Json;

namespace RobotVision.InferenceBench;

public static class ReportJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    public static void Write(string path, WorkerReport report)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);
        File.WriteAllText(path, JsonSerializer.Serialize(report, Options));
    }

    public static WorkerReport Read(string path) =>
        JsonSerializer.Deserialize<WorkerReport>(File.ReadAllText(path), Options)
        ?? throw new InvalidDataException($"无法解析报告: {path}");
}
