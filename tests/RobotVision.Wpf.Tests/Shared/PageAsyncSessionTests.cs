using FluentAssertions;
using RobotVision.WpfHost.Shared;

namespace RobotVision.Wpf.Tests;

public sealed class PageAsyncSessionTests
{
    [Fact]
    public void Deactivate_IncrementsGeneration_AndCancelsToken()
    {
        var session = new PageAsyncSession();
        var generation = session.CaptureGeneration();
        var ct = session.Token;

        session.Deactivate();

        session.IsCurrent(generation).Should().BeFalse();
        ct.IsCancellationRequested.Should().BeTrue();
    }

    [Fact]
    public void Deactivate_IsIdempotent()
    {
        var session = new PageAsyncSession();
        var generation = session.CaptureGeneration();

        session.Deactivate();
        session.Deactivate();

        session.IsCurrent(generation).Should().BeFalse();
    }

    [Fact]
    public async Task Deactivate_DoesNotBlock_OnSlowTrackedTask()
    {
        var session = new PageAsyncSession();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var work = Task.Run(async () =>
        {
            entered.SetResult();
            await release.Task;
        });
        session.Track(work);
        await entered.Task;

        var sw = System.Diagnostics.Stopwatch.StartNew();
        session.Deactivate();
        sw.Stop();

        sw.Elapsed.Should().BeLessThan(TimeSpan.FromMilliseconds(200));
        release.SetResult();
        await work;
    }

    [Fact]
    public async Task IsCurrent_RejectsStaleGenerationAfterDeactivate()
    {
        var session = new PageAsyncSession();
        var generation = session.CaptureGeneration();
        var applied = false;

        var work = Task.Run(async () =>
        {
            await Task.Delay(50);
            if (session.IsCurrent(generation))
                applied = true;
        });
        session.Track(work);
        await Task.Delay(10);
        session.Deactivate();

        await work;
        applied.Should().BeFalse();
    }
}
