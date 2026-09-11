using Microsoft.Extensions.Logging;
using RobotVision.Core.Models;
using RobotVision.Core.Recipe;

namespace RobotVision.Hosting;

/// <summary>
/// 在去畸变图上绘制检测叠加，供失败/成功留存「绘制图」。
/// WPF 宿主用与监控/配方试触发同一套绘制；无 UI 的宿主可不注册（绘制图则跳过）。
/// </summary>
public interface ICaptureOverlayPainter
{
    void Paint(VisionImage image, IReadOnlyList<PixelPose> poses, RecipeDisplayHints hints);
}

internal static class CaptureOverlayPaint
{
    public static VisionImage? TryClone(
        ICaptureOverlayPainter? painter,
        VisionImage source,
        IReadOnlyList<PixelPose> poses,
        RecipeDisplayHints hints,
        ILogger? log = null)
    {
        if (painter is null || source.IsEmpty)
            return null;

        var clone = source.Clone();
        try
        {
            painter.Paint(clone, poses, hints);
            return clone;
        }
        catch (Exception ex)
        {
            clone.Dispose();
            if (log is not null)
                VisionServiceLog.OverlayPaintFailed(log, ex);
            return null;
        }
    }
}
