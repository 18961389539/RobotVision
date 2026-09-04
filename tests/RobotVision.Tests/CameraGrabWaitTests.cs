using RobotVision.Infrastructure.Cameras;
using Xunit;

namespace RobotVision.Tests;

public sealed class CameraGrabWaitTests
{
    [Fact]
    public void NextSliceMs_UsesPollUntilBudgetRunsOut()
    {
        Assert.Equal(50, CameraGrabWait.NextSliceMs(deadlineTick: 1000, pollMs: 50, nowTick: 900));
        Assert.Equal(30, CameraGrabWait.NextSliceMs(deadlineTick: 1000, pollMs: 50, nowTick: 970));
        Assert.Equal(0, CameraGrabWait.NextSliceMs(deadlineTick: 1000, pollMs: 50, nowTick: 1000));
        Assert.Equal(0, CameraGrabWait.NextSliceMs(deadlineTick: 1000, pollMs: 50, nowTick: 1200));
    }

    [Fact]
    public void WaitUnlessCanceled_HonorsToken()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        Assert.Throws<OperationCanceledException>(() => CameraGrabWait.WaitUnlessCanceled(5_000, cts.Token));
    }
}
