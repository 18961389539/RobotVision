using System.Diagnostics;

namespace RobotVision.InferenceBench;

public readonly record struct OverlapSample(double MakespanMs, double WaitAMs, double WaitBMs);

/// <summary>预热不计统计。并行两枪各自独立 Action（两个会话）。</summary>
public static class TimedRun
{
    public static void PrepareGc()
    {
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true, compacting: true);
        GC.WaitForPendingFinalizers();
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true, compacting: true);
    }

    public static double[] Single(Action infer, int warmup, int iters)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(warmup);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(iters);

        for (var i = 0; i < warmup; i++)
            infer();

        PrepareGc();
        var times = new double[iters];
        for (var i = 0; i < iters; i++)
        {
            var sw = Stopwatch.StartNew();
            infer();
            times[i] = sw.Elapsed.TotalMilliseconds;
        }

        return times;
    }

    public static OverlapSample[] SerialTwo(Action infer, int warmup, int pairs)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(warmup);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(pairs);

        for (var i = 0; i < warmup; i++)
            infer();

        PrepareGc();
        var samples = new OverlapSample[pairs];
        for (var i = 0; i < pairs; i++)
        {
            var pair = Stopwatch.StartNew();
            var a = Stopwatch.StartNew();
            infer();
            var waitA = a.Elapsed.TotalMilliseconds;
            var b = Stopwatch.StartNew();
            infer();
            var waitB = b.Elapsed.TotalMilliseconds;
            samples[i] = new OverlapSample(pair.Elapsed.TotalMilliseconds, waitA, waitB);
        }

        return samples;
    }

    public static OverlapSample[] ParallelTwo(Action inferA, Action inferB, int warmup, int pairs)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(warmup);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(pairs);

        for (var i = 0; i < warmup; i++)
        {
            inferA();
            inferB();
        }

        PrepareGc();
        var samples = new OverlapSample[pairs];
        for (var i = 0; i < pairs; i++)
        {
            double waitA = 0, waitB = 0;
            var pair = Stopwatch.StartNew();
            Parallel.Invoke(
                () =>
                {
                    var sw = Stopwatch.StartNew();
                    inferA();
                    waitA = sw.Elapsed.TotalMilliseconds;
                },
                () =>
                {
                    var sw = Stopwatch.StartNew();
                    inferB();
                    waitB = sw.Elapsed.TotalMilliseconds;
                });
            samples[i] = new OverlapSample(pair.Elapsed.TotalMilliseconds, waitA, waitB);
        }

        return samples;
    }
}
