using RobotVision.Core.Models;
using RobotVision.Core.Recipe;
using RobotVision.Hosting;

namespace RobotVision.WpfHost.Shared;

internal sealed class FrameOverlayPresenter : IFrameOverlayPresenter, ICaptureOverlayPainter
{
    public void Compose(VisionImage image, IReadOnlyList<PixelPose> poses, RecipeDisplayHints hints) =>
        FrameOverlayComposer.Compose(image, poses, hints);

    public void Paint(VisionImage image, IReadOnlyList<PixelPose> poses, RecipeDisplayHints hints) =>
        Compose(image, poses, hints);
}
