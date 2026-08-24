using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace RobotVision.WpfHost;

public readonly record struct LogEntry(DateTime Time, LogLevel Level, string Category, string Message);

/// <summary>
/// UI 日志汇聚器：注册为 ILoggerProvider，把托管日志推给界面。
/// 事件在任意线程触发，订阅者（MainViewModel）负责封送到 UI 线程。
/// </summary>
public sealed class LogSink : ILoggerProvider
{
    private const int Capacity = 2000;

    private readonly ConcurrentQueue<LogEntry> _entries = new();
    private volatile int _count;

    public event Action<LogEntry>? EntryAdded;

    public ILogger CreateLogger(string categoryName) => new SinkLogger(this, categoryName);

    private void Add(LogEntry entry)
    {
        _entries.Enqueue(entry);
        if (Interlocked.Increment(ref _count) > Capacity && _entries.TryDequeue(out _))
            Interlocked.Decrement(ref _count);
        EntryAdded?.Invoke(entry);
    }

    /// <summary>当前缓冲的日志（启动早期订阅者未就绪时也可读取）。</summary>
    public IReadOnlyList<LogEntry> Snapshot() => _entries.ToArray();

    public void Dispose()
    {
    }

    private sealed class SinkLogger(LogSink sink, string category) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information;

        public void Log<TState>(
            LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
                return;

            var message = formatter(state, exception);
            if (exception is not null)
                message += $" | {exception.Message}";

            sink.Add(new LogEntry(DateTime.Now, logLevel, category, message));
        }
    }
}
