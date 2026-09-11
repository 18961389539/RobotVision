using System.Runtime.InteropServices;
using RobotVision.Core.Models;
using RobotVision.Core.Recipe;
using RobotVision.Hosting;
using Xunit;

namespace RobotVision.Tests;

public sealed class CaptureOverlayPaintTests
{
    private sealed class MarkPainter : ICaptureOverlayPainter
    {
        public void Paint(VisionImage image, IReadOnlyList<PixelPose> poses, RecipeDisplayHints hints) =>
            Marshal.WriteByte(image.Data, 0, 42);
    }

    [Fact]
    public void TryClone_NullPainter_ReturnsNull()
    {
        using var source = VisionImage.AllocateZero(4, 4, 3);
        Assert.Null(CaptureOverlayPaint.TryClone(null, source, [], RecipeDisplayHints.Production));
    }

    [Fact]
    public void TryClone_PaintsCopy_LeavesSourceUnchanged()
    {
        using var source = VisionImage.AllocateZero(4, 4, 3);
        using var painted = CaptureOverlayPaint.TryClone(
            new MarkPainter(), source, [], RecipeDisplayHints.Production);

        Assert.NotNull(painted);
        Assert.Equal(0, Marshal.ReadByte(source.Data));
        Assert.Equal(42, Marshal.ReadByte(painted.Data));
    }
}
