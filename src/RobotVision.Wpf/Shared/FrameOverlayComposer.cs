using OpenCvSharp;
using RobotVision.Core.Models;
using RobotVision.Core.Recipe;
using RobotVision.Hosting;

namespace RobotVision.WpfHost.Shared;

/// <summary>统一帧叠加合成：配方试触发与运行监控共用，避免两页绘制分叉。</summary>
public static class FrameOverlayComposer
{
    public static void Compose(VisionImage image, IReadOnlyList<PixelPose> poses, RecipeDisplayHints hints)
    {
        OverlayDrawer.DrawPoses(image, poses, drawDebug: hints.ShowRefineDebug);
        using var drawn = VisionImageMat.AsMat(image);
        var dualSearchRois = hints.SecondarySearchRoi is not null;
        if (hints.DrawDetectionRoi && hints.DetectionRoi is { } roi)
            OverlayDrawer.DrawNormalizedRoi(drawn, roi, dualSearchRois ? "ROI1" : "检测", Scalar.Lime);
        if (hints.SecondarySearchRoi is { } secondary)
            OverlayDrawer.DrawNormalizedRoi(drawn, secondary, "ROI2", Scalar.DeepSkyBlue);
    }
}
