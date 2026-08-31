using RobotVision.Core.Models;

namespace RobotVision.WpfHost.Features.Recipe;

/// <summary>配方页试触发状态栏文案（便于单测超时/取消与 ERR 口径）。</summary>
internal static class RecipeTestTriggerMessages
{
    internal static string FormatPreviewResult(VisionResult result, bool hasUnsavedChanges)
    {
        if (result.Ok)
            return $"测试通过：{result.RecipeName} · {result.Poses.Count} 个目标 · {result.ElapsedMs:0}ms"
                   + (hasUnsavedChanges ? "（编辑器，未保存不上产线）" : "");
        if (result.ErrorCode == VisionErrorCode.RefineFailed)
            return $"测试失败：ERR 1019 精修未过门 · {result.Message}";
        if (result.ErrorCode == VisionErrorCode.Timeout)
            return $"测试超时：ERR 1008 处理超时 · {result.Message}";
        if (result.ErrorCode == VisionErrorCode.QueueTimeout)
            return $"测试超时：ERR 1010 排队超时 · {result.Message}";
        return $"测试失败：ERR {result.ErrorCode} · {result.Message}";
    }

    internal static string FormatException(Exception ex, int recipeTestTimeoutMs)
    {
        if (ex is OperationCanceledException)
        {
            var limitSec = Math.Max(5000, recipeTestTimeoutMs) / 1000.0;
            return $"测试超时：超过 {limitSec:0.#}s 未完成（RecipeTestTimeoutMs），请检查相机取图、模型推理或产线排队";
        }

        return $"测试异常：{ex.Message}";
    }
}
