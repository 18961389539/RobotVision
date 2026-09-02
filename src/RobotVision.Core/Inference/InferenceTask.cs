namespace RobotVision.Core.Inference;

/// <summary>ONNX 推理任务类型（与模型输出头对应）。</summary>
public enum InferenceTask
{
    ObjectDetection,
    Segmentation,
    PoseEstimation,
}
