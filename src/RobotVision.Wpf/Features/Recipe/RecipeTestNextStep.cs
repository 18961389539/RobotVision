using RobotVision.Core.Models;

namespace RobotVision.WpfHost.Features.Recipe;

/// <summary>试触发后主画面下一步提示（失败码 → 可操作建议）。</summary>
internal static class RecipeTestNextStep
{
    public static string For(VisionResult? result, string? qualityNote, bool unsaved)
    {
        if (result is null)
            return "";

        if (result.Ok)
            return unsaved ? "测试已用当前编辑器。保存后才上产线。" : "";

        var quality = string.IsNullOrWhiteSpace(qualityNote) ? "" : qualityNote.Trim();
        var action = result.ErrorCode switch
        {
            VisionErrorCode.RefineFailed => "可下调匹配阈值、加大角度范围，或重新示教",
            VisionErrorCode.NoTargetFound => "检查检测区域、置信度与照明",
            VisionErrorCode.CameraGrabFailed or VisionErrorCode.CameraInitFailed => "检查相机连接或回放目录",
            VisionErrorCode.ModelNotAvailable => "在检测 Tab 选择有效分割模型",
            VisionErrorCode.InvalidRecipeConfig => "按提示修正配方后保存",
            VisionErrorCode.RecipeDisabled => "启用该配方并保存",
            VisionErrorCode.NotCalibrated => "先完成该工位标定",
            VisionErrorCode.AssetMismatch => "核对模型/标定后重新钉扎哈希",
            _ => "",
        };

        if (quality.Length > 0 && action.Length > 0)
            return quality + " · " + action;
        return quality.Length > 0 ? quality : action;
    }

    public static string Badge(VisionResult? result)
    {
        if (result is null)
            return "";
        if (result.Ok)
            return result.Poses.Count > 0
                ? $"OK · {result.Poses.Count} 件"
                : "OK";
        var code = (int)result.ErrorCode;
        return code > 0 ? $"ERR {code}" : "失败";
    }
}
