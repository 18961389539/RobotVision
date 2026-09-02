using RobotVision.Core;
using RobotVision.Core.Models;
using RobotVision.Core.Recipe;
using RobotVision.Infrastructure.Calibration;

namespace RobotVision.Hosting;

/// <summary>配方编辑/试触发用的推理图像空间（与产线 TRIGGER 一致的去畸变规则）。</summary>
public static class RecipeEditorImagePrep
{
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
