namespace RobotVision.Hosting;

/// <summary>监控页实时预览：取图 + 按配方工位选择去畸变/原图，输出可展示的 BGRA 缓冲。</summary>
public interface IMonitorPreviewService
{
    BgraImageBuffer GrabDisplayFrame(string cameraId, string? recipeName, CancellationToken ct = default);
}
