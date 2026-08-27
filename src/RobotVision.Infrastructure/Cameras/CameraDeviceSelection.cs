namespace RobotVision.Infrastructure.Cameras;

/// <summary>
/// 硬件相机 DeviceId 解析：指定必须命中；未指定时仅当现场恰好一台才绑定。
/// 禁止「对不上就打开第一台」。
/// </summary>
public static class CameraDeviceSelection
{
    public static T? Resolve<T>(
        IReadOnlyList<T> devices,
        string? deviceId,
        Func<T, string, bool> matches)
    {
        if (devices.Count == 0)
            return default;

        if (string.IsNullOrWhiteSpace(deviceId))
            return devices.Count == 1 ? devices[0] : default;

        var needle = deviceId.Trim();
        foreach (var device in devices)
        {
            if (matches(device, needle))
                return device;
        }

        return default;
    }

    public static string UnresolvedMessage(string cameraId, string? deviceId, int availableCount, string availableList)
    {
        if (string.IsNullOrWhiteSpace(deviceId))
        {
            return availableCount > 1
                ? $"相机 {cameraId} 未指定 DeviceId，现场有 {availableCount} 台（{availableList}），拒绝自动绑定。请填写序列号或 IP。"
                : $"相机 {cameraId} 未发现可绑定设备";
        }

        var listed = string.IsNullOrWhiteSpace(availableList) ? "无" : availableList;
        return $"相机 {cameraId} 未找到 DeviceId={deviceId.Trim()}（可用: {listed}），拒绝绑定其他相机";
    }
}
