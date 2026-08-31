using RobotVision.Core.Models;
using Xunit;

namespace RobotVision.Tests;

/// <summary>运行时匹配窗：示教模板矩形绕匹配峰旋转，与配方特征橙框分离。</summary>
public sealed class OverlayMatchWindowTests
{
    [Fact]
    public void TemplateMatchWindow_ZeroDeg_AxisAlignedAroundPeak()
    {
        var corners = PoseOverlay.TemplateMatchWindow(100, 50, 0, widthPx: 40, heightPx: 20);
        Assert.Equal(4, corners.Length);
        Assert.Equal(80, corners[0].X, 6);
        Assert.Equal(40, corners[0].Y, 6);
        Assert.Equal(120, corners[1].X, 6);
        Assert.Equal(40, corners[1].Y, 6);
        Assert.Equal(120, corners[2].X, 6);
        Assert.Equal(60, corners[2].Y, 6);
        Assert.Equal(80, corners[3].X, 6);
        Assert.Equal(60, corners[3].Y, 6);
    }

    [Fact]
    public void TemplateMatchWindow_90Deg_SwapsWidthAlongY()
    {
        var corners = PoseOverlay.TemplateMatchWindow(0, 0, 90, widthPx: 40, heightPx: 20);
        // 宽沿 +X 转到 +Y（y 向下、逆时针）
        Assert.Equal(10, corners[0].X, 6);
        Assert.Equal(-20, corners[0].Y, 6);
        Assert.Equal(10, corners[1].X, 6);
        Assert.Equal(20, corners[1].Y, 6);
        Assert.Equal(-10, corners[2].X, 6);
        Assert.Equal(20, corners[2].Y, 6);
        Assert.Equal(-10, corners[3].X, 6);
        Assert.Equal(-20, corners[3].Y, 6);
    }
}
