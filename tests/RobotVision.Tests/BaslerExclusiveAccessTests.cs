using RobotVision.Infrastructure.Cameras;
using Xunit;

namespace RobotVision.Tests;

public sealed class BaslerExclusiveAccessTests
{
    [Fact]
    public void IsExclusiveAccessError_PylonBusyMessage()
    {
        var ex = new InvalidOperationException(
            "Failed to open 'Basler acA5472-5gm#003053373069#192.168.4.253:3956'. The device is controlled by another application. Err: An attempt was made to access an address location which is currently/momentary not accessible. (0xE1018006)");
        Assert.True(BaslerCamera.IsExclusiveAccessError(ex));
        Assert.Contains("被其他程序占用", BaslerCamera.DescribeConnectFailure(ex), StringComparison.Ordinal);
    }

    [Fact]
    public void IsExclusiveAccessError_OtherErrors_False()
    {
        Assert.False(BaslerCamera.IsExclusiveAccessError(new InvalidOperationException("timeout")));
    }
}
