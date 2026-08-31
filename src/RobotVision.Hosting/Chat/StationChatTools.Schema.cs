using System.Globalization;
using System.Text.Json;
using OpenCvSharp;
using RobotVision.Core.Abstractions;
using RobotVision.Core.Models;
using RobotVision.Core.Recipe;
using RobotVision.Infrastructure;
using RobotVision.Infrastructure.Calibration;
using RobotVision.Infrastructure.Cameras;
using RobotVision.Infrastructure.Communication;
using RobotVision.Infrastructure.Inference;
using RobotVision.Infrastructure.Lighting;

namespace RobotVision.Hosting.Chat;
public sealed partial class StationChatTools
{
    private static List<object> ListPng(string folder, int limit)
    {
        if (!Directory.Exists(folder))
            return [];
        return Directory.EnumerateFiles(folder, "*.png", SearchOption.AllDirectories)
            .OrderByDescending(File.GetLastWriteTime)
            .Take(limit)
            .Select(p => (object)new { path = p, name = Path.GetFileName(p), written = File.GetLastWriteTime(p) })
            .ToList();
    }

    private static bool IsSafeUnder(string root, string path)
    {
        try
        {
            var full = Path.GetFullPath(path);
            var prefix = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                         + Path.DirectorySeparatorChar;
            return full.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static DelegateChatTool Tool(
        string name, string description, JsonElement parameters,
        Func<string, CancellationToken, Task<ChatToolResult>> invoke) =>
        new DelegateChatTool(name, description, parameters, invoke);

    private static JsonElement Empty() => Schema(new { type = "object", properties = new Dictionary<string, object>() });

    private static JsonElement Props(params string[] names) => Props(names, required: false);

    private static JsonElement Props(string name, bool required) => Props([name], required);

    private static JsonElement Props(string[] names, bool required)
    {
        var properties = new Dictionary<string, object>(StringComparer.Ordinal);
        foreach (var n in names)
        {
            properties[n] = n switch
            {
                "ok_only" or "enabled" or "pose_check" or "failure_enabled" or "process_health"
                    or "inhibit_on_limit" or "confirm" => new { type = "boolean" },
                "limit" or "channel" or "brightness" or "client_id" or "serial" or "timeout_ms"
                    or "max_queue" or "tcp_port" or "grab_timeout_ms" or "max_concurrent"
                    or "tcp_backlog" or "max_connections" or "failure_retained"
                    or "consecutive_fail_limit" or "code" or "offset" or "days" or "hours" or "bins" => new { type = "integer" },
                "idle_timeout_ms" or "exposure_us" or "gain" or "xy_tol" or "rz_tol" or "px" or "py"
                    or "angle_deg" or "confidence" or "iou" => new { type = "number" },
                _ => new { type = "string" },
            };
        }
        object schema = required && names.Length > 0
            ? new { type = "object", properties, required = new[] { names[0] } }
            : new { type = "object", properties };
        return Schema(schema);
    }

    private static JsonElement Schema(object o) =>
        JsonSerializer.Deserialize<JsonElement>(JsonSerializer.Serialize(o))!;

    private static JsonDocument Parse(string json)
    {
        var parsed = ChatToolArguments.TryParse(json);
        if (!parsed.IsSuccess)
            throw new InvalidOperationException(parsed.Error);
        return parsed.Document!;
    }

    private static string Str(JsonDocument doc, string name) =>
        doc.RootElement.TryGetProperty(name, out var n) && n.ValueKind == JsonValueKind.String
            ? n.GetString() ?? ""
            : n.ValueKind is JsonValueKind.Number ? n.GetRawText() : "";

    private static int Int(JsonDocument doc, string name, int fallback)
    {
        if (!doc.RootElement.TryGetProperty(name, out var n))
            return fallback;
        if (n.TryGetInt32(out var v))
            return v;
        if (n.ValueKind == JsonValueKind.String && int.TryParse(n.GetString(), out v))
            return v;
        return fallback;
    }

    private static long Long(JsonDocument doc, string name, long fallback)
    {
        if (!doc.RootElement.TryGetProperty(name, out var n))
            return fallback;
        if (n.TryGetInt64(out var v))
            return v;
        if (n.ValueKind == JsonValueKind.String && long.TryParse(n.GetString(), out v))
            return v;
        if (n.TryGetDouble(out var d) && d is >= long.MinValue and <= long.MaxValue)
            return (long)d;
        return fallback;
    }

    private static bool Has(JsonDocument doc, string name) =>
        doc.RootElement.TryGetProperty(name, out var n) && n.ValueKind is not JsonValueKind.Null and not JsonValueKind.Undefined;

    private static double Dbl(JsonDocument doc, string name, double fallback)
    {
        if (!doc.RootElement.TryGetProperty(name, out var n))
            return fallback;
        if (n.TryGetDouble(out var v))
            return v;
        if (n.ValueKind == JsonValueKind.String &&
            double.TryParse(n.GetString(), System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out v))
            return v;
        return fallback;
    }

    private static bool? MaybeBool(JsonDocument doc, string name)
    {
        if (!doc.RootElement.TryGetProperty(name, out var n))
            return null;
        if (n.ValueKind is JsonValueKind.True or JsonValueKind.False)
            return n.GetBoolean();
        if (n.ValueKind == JsonValueKind.String && bool.TryParse(n.GetString(), out var b))
            return b;
        return null;
    }

    private static IReadOnlyList<string> ReadStringList(JsonDocument doc, string name)
    {
        if (!doc.RootElement.TryGetProperty(name, out var n))
            return [];
        if (n.ValueKind == JsonValueKind.Array)
        {
            return n.EnumerateArray()
                .Select(x => x.ValueKind == JsonValueKind.String ? x.GetString() ?? "" : x.GetRawText())
                .Where(s => s.Length > 0)
                .ToList();
        }
        if (n.ValueKind == JsonValueKind.String)
            return (n.GetString() ?? "").Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        return [];
    }

    private static string? EmptyToNull(string s) => string.IsNullOrWhiteSpace(s) ? null : s;

    private static ResultDbQuery BuildResultQuery(JsonDocument doc, int defaultLimit, int maxLimit)
    {
        var now = DateTimeOffset.Now;
        DateTimeOffset? from = ParseWhen(doc, "from");
        DateTimeOffset? to = ParseWhen(doc, "to");
        if (from is null && Has(doc, "days"))
            from = now.AddDays(-Math.Clamp(Int(doc, "days", 1), 1, 3650));
        if (from is null && Has(doc, "hours"))
            from = now.AddHours(-Math.Clamp(Int(doc, "hours", 1), 1, 24 * 90));
        if (from is null)
            ApplyRangePreset(Str(doc, "range"), now, ref from, ref to);
        return new ResultDbQuery
        {
            Recipe = EmptyToNull(Str(doc, "recipe")),
            Station = EmptyToNull(Str(doc, "station")),
            Camera = EmptyToNull(Str(doc, "camera")),
            Code = Has(doc, "code") ? Int(doc, "code", 0) : null,
            OkOnly = MaybeBool(doc, "ok_only"),
            MessageContains = EmptyToNull(Str(doc, "message")),
            From = from,
            To = to,
            Limit = Math.Clamp(Int(doc, "limit", defaultLimit), 1, maxLimit),
            Offset = Math.Max(0, Int(doc, "offset", 0)),
        };
    }

    private static ResultDbQuery WithAnalysisDefaultRange(JsonDocument doc, ResultDbQuery query)
    {
        if (query.From is not null || Has(doc, "from") || Has(doc, "days") || Has(doc, "hours") || Has(doc, "range"))
            return query;
        var now = DateTimeOffset.Now;
        return query with
        {
            From = new DateTimeOffset(now.Date, now.Offset),
            To = now,
        };
    }

    private static bool LooksLikeToday(ResultDbQuery query)
    {
        if (query.From is not { } from)
            return false;
        var now = DateTimeOffset.Now;
        return from.Date == now.Date;
    }

    private static void ApplyRangePreset(string range, DateTimeOffset now, ref DateTimeOffset? from, ref DateTimeOffset? to)
    {
        switch (range.Trim().ToLowerInvariant())
        {
            case "today" or "今天":
                from = new DateTimeOffset(now.Date, now.Offset);
                to ??= now;
                break;
            case "7d" or "7" or "week" or "近7天":
                from = now.AddDays(-7);
                to ??= now;
                break;
            case "30d" or "30" or "month" or "近30天":
                from = now.AddDays(-30);
                to ??= now;
                break;
            case "all" or "全部":
                from = null;
                break;
        }
    }

    private static DateTimeOffset? ParseWhen(JsonDocument doc, string name)
    {
        if (!Has(doc, name))
            return null;
        var s = Str(doc, name).Trim();
        if (s.Length == 0)
            return null;
        var now = DateTimeOffset.Now;
        if (s.Equals("today", StringComparison.OrdinalIgnoreCase) || s == "今天")
            return new DateTimeOffset(now.Date, now.Offset);
        if (s.Equals("now", StringComparison.OrdinalIgnoreCase) || s == "现在")
            return now;
        if (TryParseRelativeAgo(s, now, out var rel))
            return rel;
        if (DateTimeOffset.TryParse(s, System.Globalization.CultureInfo.CurrentCulture,
                System.Globalization.DateTimeStyles.AssumeLocal, out var local))
            return local;
        if (DateTimeOffset.TryParse(s, System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.AssumeLocal, out var inv))
            return inv;
        return null;
    }

    private static bool TryParseRelativeAgo(string s, DateTimeOffset now, out DateTimeOffset value)
    {
        value = default;
        var t = s.Trim().ToLowerInvariant();
        if (t.StartsWith('-'))
            t = t[1..];
        t = t.Replace(" ", "", StringComparison.Ordinal);
        var unitIndex = -1;
        for (var i = 0; i < t.Length; i++)
        {
            if (!char.IsDigit(t[i]))
            {
                unitIndex = i;
                break;
            }
        }
        if (unitIndex <= 0 ||
            !int.TryParse(t[..unitIndex], System.Globalization.NumberStyles.None,
                System.Globalization.CultureInfo.InvariantCulture, out var n) || n <= 0)
            return false;
        var unit = t[unitIndex..];
        value = unit switch
        {
            "d" or "day" or "days" or "天" => now.AddDays(-n),
            "h" or "hr" or "hrs" or "hour" or "hours" or "小时" => now.AddHours(-n),
            _ => default,
        };
        return unit is "d" or "day" or "days" or "天" or "h" or "hr" or "hrs" or "hour" or "hours" or "小时";
    }

    private static ChatToolResult Ok(object o, string? image = null) =>
        new(JsonSerializer.Serialize(o, Json), image);

    private static ChatToolResult Fail(string error) =>
        new(JsonSerializer.Serialize(new { ok = false, error }, Json));
}
