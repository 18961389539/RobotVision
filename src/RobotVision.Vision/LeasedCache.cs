namespace RobotVision.Vision.Inference.Strategies;

/// <summary>
/// 按键缓存带租约的本机资源：Acquire 增加租约，Remove / 指纹重建只退役条目；
/// 最后一次 <see cref="Release"/> 才 Dispose，避免 TRIGGER 匹配中途拆掉 Mat。
/// 缓存自身 Dispose 时退役全部条目；仍有租约的条目延至 Release 后再释放。
/// </summary>
internal sealed class LeasedCache<TValue>(Action<TValue> disposeValue) : IDisposable
    where TValue : class
{
    private readonly object _gate = new();
    private readonly Dictionary<string, Entry> _byKey = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<Entry> _retired = [];
    private bool _disposed;

    private sealed class Entry
    {
        public required string Fingerprint { get; init; }
        public required TValue Value { get; init; }
        public int Leases;
        public bool Retired;
    }

    public TValue? Acquire(string key, string fingerprint, Func<TValue?> build)
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_byKey.TryGetValue(key, out var hit) && hit.Fingerprint == fingerprint)
            {
                hit.Leases++;
                return hit.Value;
            }

            if (hit is not null)
            {
                _byKey.Remove(key);
                Retire(hit);
            }

            var value = build();
            if (value is null)
                return null;

            _byKey[key] = new Entry { Fingerprint = fingerprint, Value = value, Leases = 1 };
            return value;
        }
    }

    public void Release(TValue? value)
    {
        if (value is null)
            return;
        lock (_gate)
        {
            var entry = Find(value);
            if (entry is null)
                return;
            if (entry.Leases > 0)
                entry.Leases--;
            if (entry.Retired && entry.Leases == 0)
            {
                _retired.Remove(entry);
                disposeValue(entry.Value);
            }
        }
    }

    public void Remove(string key)
    {
        lock (_gate)
        {
            if (_byKey.Remove(key, out var entry))
                Retire(entry);
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
                return;
            _disposed = true;
            foreach (var entry in _byKey.Values.ToList())
                Retire(entry);
            _byKey.Clear();
            DisposeRetiredWithNoLeases();
        }
    }

    private void Retire(Entry entry)
    {
        entry.Retired = true;
        if (entry.Leases == 0)
            disposeValue(entry.Value);
        else if (!_retired.Contains(entry))
            _retired.Add(entry);
    }

    private void DisposeRetiredWithNoLeases()
    {
        for (var i = _retired.Count - 1; i >= 0; i--)
        {
            var entry = _retired[i];
            if (entry.Leases != 0)
                continue;
            _retired.RemoveAt(i);
            disposeValue(entry.Value);
        }
    }

    private Entry? Find(TValue value)
    {
        foreach (var entry in _byKey.Values)
        {
            if (ReferenceEquals(entry.Value, value))
                return entry;
        }

        foreach (var entry in _retired)
        {
            if (ReferenceEquals(entry.Value, value))
                return entry;
        }

        return null;
    }
}
