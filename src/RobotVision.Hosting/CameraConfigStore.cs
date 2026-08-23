using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace RobotVision.Hosting;

/// <summary>
/// 相机配置的运行时增删改与持久化：写回 appsettings.json 的 Cameras 节点
/// （保留其他配置节点原样），并同步内存中的 AppConfig（运行中的 DI 单例）。
/// 相机实例的注册/反注册由调用方配合 CameraManager 完成。
/// </summary>
public sealed class CameraConfigStore(AppConfig cfg, string? settingsPath = null)
{
    private readonly string _settingsPath =
        settingsPath ?? Path.Combine(AppContext.BaseDirectory, "appsettings.json");

    private static readonly JsonSerializerOptions Indented = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public string SettingsPath => _settingsPath;

    /// <summary>保存相机列表：落盘 + 同步内存配置。文件不存在时创建仅含 Cameras 的最小配置。</summary>
    public void Save(IReadOnlyList<CameraConfig> cameras)
    {
        // 原子读-改-写：读取与写入同锁（JsonAtomicWrite.Gate），与其他写方（AppSettingsStore/
        // LightingConfigStore）串行化，杜绝并发 Save 的后写覆盖前写
        JsonAtomicWrite.Update(_settingsPath, Indented,
            obj => obj["Cameras"] = JsonSerializer.SerializeToNode(cameras, Indented));

        cfg.Cameras = [.. cameras];
    }
}
