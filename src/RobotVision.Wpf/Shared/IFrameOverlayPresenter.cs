using RobotVision.Core.Models;
using RobotVision.Core.Recipe;

namespace RobotVision.WpfHost.Shared;

/// <summary>在去畸变图上绘制位姿/ROI 叠加（OpenCV 绘制封装在 WPF 层，ViewModel 不直接引用）。</summary>
public interface IFrameOverlayPresenter
{
    void Compose(VisionImage image, IReadOnlyList<PixelPose> poses, RecipeDisplayHints hints);
}
