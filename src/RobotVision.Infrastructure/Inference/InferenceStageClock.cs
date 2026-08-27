using System.Diagnostics;

namespace RobotVision.Infrastructure.Inference;

/// <summary>
/// 当前线程一次 <c>Compute</c> 的分割 / 精修耗时（毫秒）。
/// 策略在线程池上写入，<c>VisionService</c> 在同一 <c>Task.Run</c> 内读取。
/// </summary>
public static class InferenceStageClock
{
    [ThreadStatic] private static double _segmentMs;
    [ThreadStatic] private static double _refineMs;

    public static void Reset()
    {
        _segmentMs = 0;
        _refineMs = 0;
    }

    public static void AddSegment(double ms) => _segmentMs += ms;

    public static void AddRefine(double ms) => _refineMs += ms;

    public static (double SegmentMs, double RefineMs) Snapshot() => (_segmentMs, _refineMs);

    public static T MeasureSegment<T>(Func<T> work)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            return work();
        }
        finally
        {
            AddSegment(sw.Elapsed.TotalMilliseconds);
        }
    }

    public static T MeasureRefine<T>(Func<T> work)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            return work();
        }
        finally
        {
            AddRefine(sw.Elapsed.TotalMilliseconds);
        }
    }
}
