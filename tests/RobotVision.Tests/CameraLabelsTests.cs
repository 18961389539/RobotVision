using RobotVision.Hosting;
using Xunit;

namespace RobotVision.Tests;

public class CameraLabelsTests
{
    [Fact]
    public void ListTitle_UsesNameWhenPresent()
    {
        var camera = new CameraConfig { Id = "cam_a", Name = "  左工位  " };
        Assert.Equal("左工位", CameraLabels.ListTitle(camera));
    }

    [Fact]
    public void ListTitle_FallsBackToId_WhenNameEmpty()
    {
        var camera = new CameraConfig { Id = "cam_a", Name = "   " };
        Assert.Equal("cam_a", CameraLabels.ListTitle(camera));
    }

    [Fact]
    public void ComboLabel_ShowsNameAndId()
    {
        var camera = new CameraConfig { Id = "cam_a", Name = "左工位" };
        Assert.Equal("左工位 (cam_a)", CameraLabels.ComboLabel(camera));
    }

    [Fact]
    public void CameraOption_FromRegistered_UsesConfigName()
    {
        var configs = new[] { new CameraConfig { Id = "cam_a", Name = "左工位" } };
        var options = CameraOption.FromRegistered(configs, ["cam_a", "missing"]);

        Assert.Equal(2, options.Count);
        Assert.Equal("左工位 (cam_a)", options[0].Label);
        Assert.Equal("missing", options[1].Label);
    }
}
