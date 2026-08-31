using System.Diagnostics;
using System.Text.Json;

namespace RobotVision.Hosting.Chat;

public sealed record ChatToolAuditEntry(
    DateTimeOffset TimeUtc,
    string Tool,
    string Arguments,
    string Outcome,
    string? Error,
    long DurationMs,
    string? UserSnippet);

/// <summary>对话工具调用审计（JSONL，按天滚动）。</summary>
public sealed class ChatToolAuditStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private readonly ChatConfig _cfg;
    private readonly string _folder;
    private readonly object _gate = new();
    private DateTime _currentDay;
    private string? _currentFile;

    public ChatToolAuditStore(AppConfig cfg)
    {
        _cfg = cfg.Chat;
        _folder = cfg.ResolveDataPath(
            string.IsNullOrWhiteSpace(_cfg.AuditFolder) ? "data/chat-audit" : _cfg.AuditFolder);
        Directory.CreateDirectory(_folder);
        _currentDay = DateTime.MinValue;
    }

    public string Folder => _folder;

    public void Record(ChatToolAuditEntry entry)
    {
        if (!_cfg.AuditEnabled)
            return;
        lock (_gate)
        {
            Append(entry);
            CleanupIfNeeded(entry.TimeUtc.LocalDateTime);
        }
    }

    private void Append(ChatToolAuditEntry entry)
    {
        var day = entry.TimeUtc.LocalDateTime.Date;
        if (day != _currentDay)
        {
            _currentDay = day;
            _currentFile = Path.Combine(_folder, $"audit-{day:yyyy-MM-dd}.jsonl");
        }

        var line = JsonSerializer.Serialize(entry, JsonOptions);
        File.AppendAllText(_currentFile!, line + Environment.NewLine);
    }

    private void CleanupIfNeeded(DateTime now)
    {
        var retained = _cfg.AuditRetainedDays;
        if (retained <= 0)
            return;
        var cutoff = now.Date.AddDays(-retained);
        foreach (var file in Directory.EnumerateFiles(_folder, "audit-*.jsonl"))
        {
            var name = Path.GetFileNameWithoutExtension(file);
            if (name.Length < 16 || !DateTime.TryParseExact(name["audit-".Length..], "yyyy-MM-dd", null,
                    System.Globalization.DateTimeStyles.None, out var fileDay))
                continue;
            if (fileDay < cutoff)
            {
                try { File.Delete(file); } catch { /* 尽力清理 */ }
            }
        }
    }

    internal static string TruncateArguments(string? arguments, int max = 2000) =>
        string.IsNullOrEmpty(arguments) ? "" : arguments.Length <= max ? arguments : arguments[..max] + "…";

    internal static string TruncateUser(string? user, int max = 240) =>
        string.IsNullOrWhiteSpace(user) ? "" : user.Trim().Length <= max ? user.Trim() : user.Trim()[..max] + "…";

    internal static string OutcomeFromResult(string resultText)
    {
        try
        {
            using var doc = JsonDocument.Parse(resultText);
            if (doc.RootElement.TryGetProperty("ok", out var ok)
                && ok.ValueKind is JsonValueKind.False)
                return "failed";
            if (doc.RootElement.TryGetProperty("blocked", out var blocked)
                && blocked.ValueKind is JsonValueKind.True)
                return "blocked";
            return "ok";
        }
        catch (JsonException)
        {
            return "unknown";
        }
    }
}
