using RobotVision.Core;
using RobotVision.Core.Models;

namespace RobotVision.Infrastructure.Inference;

/// <summary>校验所选推理任务与 ONNX 模型实际类型是否一致（避免 YoloDotNet 内部 InvalidCastException）。</summary>
public static class InferenceTaskValidation
{
    public static void EnsureSupported(IInferenceEngine engine, InferenceTask task)
    {
        var detected = engine.DetectedTask;
        if (detected is null)
            return;

        if (detected == task)
            return;

        throw new VisionException(VisionErrorCode.ModelNotAvailable,
            $"模型类型为「{Label(detected.Value)}」，与所选「{Label(task)}」不匹配。"
            + "请在参数面板切换推理任务，或更换对应类型的 .onnx 模型"
            + "（分割 *-seg.onnx → 分割；*-pose.onnx → 关键点；检测模型 → 检测）。");
    }

    public static string Label(InferenceTask task) => task switch
    {
        InferenceTask.ObjectDetection => "检测",
        InferenceTask.Segmentation => "分割",
        InferenceTask.PoseEstimation => "关键点",
        _ => task.ToString(),
    };
}
