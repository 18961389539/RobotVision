namespace RobotVision.InferenceBench;

/// <summary>轻量 <c>--key value</c> / <c>--flag</c> 解析；同一 key 可重复（如多个 --model）。</summary>
public sealed class CliArgs
{
    private readonly Dictionary<string, List<string>> _values = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _flags = new(StringComparer.OrdinalIgnoreCase);

    public static CliArgs Parse(string[] args)
    {
        var parsed = new CliArgs();
        for (var i = 0; i < args.Length; i++)
        {
            var token = args[i];
            if (!token.StartsWith("--", StringComparison.Ordinal))
                continue;

            var key = token;
            if (i + 1 < args.Length && !args[i + 1].StartsWith("--", StringComparison.Ordinal))
            {
                parsed.Add(key, args[++i]);
            }
            else
            {
                parsed._flags.Add(key);
            }
        }

        return parsed;
    }

    public bool Help => Has("--help") || Has("-h") || Has("/?");

    public bool Has(string key) => _flags.Contains(key) || _values.ContainsKey(key);

    public string Get(string key, string fallback)
    {
        if (_values.TryGetValue(key, out var list) && list.Count > 0)
            return list[^1];
        return fallback;
    }

    public string? Get(string key)
    {
        if (_values.TryGetValue(key, out var list) && list.Count > 0)
            return list[^1];
        return null;
    }

    public IReadOnlyList<string> GetAll(string key) =>
        _values.TryGetValue(key, out var list) ? list : [];

    public int GetInt(string key, int fallback) =>
        int.TryParse(Get(key), out var n) ? n : fallback;

    public double GetDouble(string key, double fallback) =>
        double.TryParse(Get(key), System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out var n) ? n : fallback;

    private void Add(string key, string value)
    {
        if (!_values.TryGetValue(key, out var list))
        {
            list = [];
            _values[key] = list;
        }

        list.Add(value);
    }
}
