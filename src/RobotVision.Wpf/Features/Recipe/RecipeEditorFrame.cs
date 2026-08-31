using System.Windows.Media;
using RobotVision.Core;
using RobotVision.Core.Models;
using RobotVision.Core.Recipe;
using RobotVision.Hosting;
using RobotVision.Infrastructure.Calibration;

namespace RobotVision.WpfHost.Features.Recipe;

/// <summary>配方页取图：与产线 TRIGGER 一致（光源稳定 + 推理用图像空间）。</summary>
internal static class RecipeEditorFrame
{
    public static async Task<(ImageSource Frame, int Width, int Height)> GrabPreviewAsync(
        ICameraRuntime cameras,
        ICalibrationRuntime calibration,
        ILightingRuntime lighting,
        RecipeConfig recipe,
        CancellationToken ct = default)
    {
        using var lightingScope = lighting.Apply(recipe.LightControllerId, recipe.Lighting);
        if (lightingScope.StabilizeDelayMs > 0)
            await Task.Delay(lightingScope.StabilizeDelayMs, ct).ConfigureAwait(false);

        return await Task.Run(() =>
        {
            using var grabbed = cameras.Grab(recipe.CameraId);
            using var inference = PrepareInferenceImage(calibration, recipe, grabbed.Image);
            return (ImageConverter.ToBitmapSource(inference), inference.Width, inference.Height);
        }, ct).ConfigureAwait(false);
    }

    public static VisionImage PrepareInferenceImage(
        ICalibrationRuntime calibration, RecipeConfig recipe, VisionImage source)
    {
        var mode = calibration.GetMappingMode(recipe.StationId);
        if (mode is StationMappingMode.Polynomial or StationMappingMode.Scale)
            return source.Clone();

        try
        {
            return calibration.Undistort(recipe.CameraId, source);
        }
        catch (VisionException)
        {
            return source.Clone();
        }
    }
}
