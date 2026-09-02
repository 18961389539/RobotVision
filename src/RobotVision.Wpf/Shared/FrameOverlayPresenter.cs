using RobotVision.Core.Models;
using RobotVision.Core.Recipe;

namespace RobotVision.WpfHost.Shared;

internal sealed class FrameOverlayPresenter : IFrameOverlayPresenter
{
    public void Compose(VisionImage image, IReadOnlyList<PixelPose> poses, RecipeDisplayHints hints) =>
        FrameOverlayComposer.Compose(image, poses, hints);
}
