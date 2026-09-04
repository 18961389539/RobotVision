using RobotVision.Infrastructure.Inference.Strategies;
using Xunit;

namespace RobotVision.Tests;

public sealed class JlVisionCoreDeployTests
{
    [Fact]
    public void JlVisionCoreDll_IsCopiedBesideInfrastructure()
    {
        var dir = Path.GetDirectoryName(typeof(MaskTemplateStrategy).Assembly.Location);
        Assert.False(string.IsNullOrEmpty(dir));
        var dll = Path.Combine(dir!, "JLVisionCore.dll");
        Assert.True(File.Exists(dll), "发布包须随带锁定版本的 JLVisionCore.dll: " + dll);
        Assert.True(new FileInfo(dll).Length > 1024);
    }
}
