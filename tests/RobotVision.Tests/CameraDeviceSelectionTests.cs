using RobotVision.Infrastructure.Cameras;
using Xunit;

namespace RobotVision.Tests;

public class CameraDeviceSelectionTests
{
    [Fact]
    public void Resolve_SpecifiedMiss_ReturnsNull_DoesNotFallBackToFirst()
    {
        var hit = CameraDeviceSelection.Resolve(["camA", "camB"], "missing", static (d, n) => d == n);
        Assert.Null(hit);
    }

    [Fact]
    public void Resolve_EmptyIdAndTwoDevices_ReturnsNull()
    {
        var hit = CameraDeviceSelection.Resolve(["a", "b"], "", static (d, n) => d == n);
        Assert.Null(hit);
    }

    [Fact]
    public void Resolve_EmptyIdAndOneDevice_BindsThatDevice()
    {
        var hit = CameraDeviceSelection.Resolve(["only"], "  ", static (d, n) => d == n);
        Assert.Equal("only", hit);
    }

    [Fact]
    public void Resolve_SpecifiedHit_ReturnsMatch()
    {
        var hit = CameraDeviceSelection.Resolve(["a", "b"], "b", static (d, n) => d == n);
        Assert.Equal("b", hit);
    }

    [Fact]
    public void UnresolvedMessage_MentionsRefuseOtherCameras()
    {
        var msg = CameraDeviceSelection.UnresolvedMessage("cam_basler", "SN-404", 2, "111;222");
        Assert.Contains("SN-404", msg, StringComparison.Ordinal);
        Assert.Contains("拒绝绑定其他相机", msg, StringComparison.Ordinal);
    }
}
