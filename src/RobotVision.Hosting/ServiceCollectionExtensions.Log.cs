using Microsoft.Extensions.Logging;

namespace RobotVision.Hosting;

internal static partial class ServiceCollectionExtensionsLog
{
    [LoggerMessage(EventId = 5001, Level = LogLevel.Warning,
        Message = "配方 {Name} 模板旋转缓存预热失败，首次匹配将现场旋转")]
    public static partial void RecipeRotationWarmFailed(ILogger logger, Exception ex, string name);

    [LoggerMessage(EventId = 5002, Level = LogLevel.Warning,
        Message = "配方 {Name} SIFT 示教缓存预热失败，首次精修将现场提取")]
    public static partial void RecipeSiftWarmFailed(ILogger logger, Exception ex, string name);

    [LoggerMessage(EventId = 5003, Level = LogLevel.Warning,
        Message = "配方 {Name} 形状匹配示教缓存预热失败，首次精修将现场提取")]
    public static partial void RecipeShapeMatchWarmFailed(ILogger logger, Exception ex, string name);

    [LoggerMessage(EventId = 5004, Level = LogLevel.Warning,
        Message = "配置中存在无 Id 的相机条目，已跳过")]
    public static partial void CameraEntryMissingId(ILogger logger);

    [LoggerMessage(EventId = 5005, Level = LogLevel.Warning,
        Message = "相机 Id 重复: {Id}（后条目已跳过，请检查 appsettings.json）")]
    public static partial void CameraDuplicateId(ILogger logger, string id);

    [LoggerMessage(EventId = 5006, Level = LogLevel.Warning,
        Message = "相机 {Id} 类型 {Type} 无工厂（实现 ICameraFactory 并调用 CameraTypeRegistry.Register 一行接入）")]
    public static partial void CameraNoFactory(ILogger logger, string id, string? type);

    [LoggerMessage(EventId = 5007, Level = LogLevel.Warning,
        Message = "相机 {Id} 未填写 DeviceId：仅当现场只有一台该类型相机时才能打开，多台将拒绝绑定")]
    public static partial void CameraMissingDeviceId(ILogger logger, string id);

    [LoggerMessage(EventId = 5008, Level = LogLevel.Warning,
        Message = "相机 {Id} GrabTimeoutMs={Grab} 不小于总超时 TimeoutMs={Total}，取图超时将表现为 1008 而非 1003，建议调小")]
    public static partial void CameraGrabTimeoutExceedsTotal(ILogger logger, string id, int grab, int total);

    [LoggerMessage(EventId = 5009, Level = LogLevel.Information,
        Message = "相机 {Id} 已注册（{Type}）")]
    public static partial void CameraRegistered(ILogger logger, string id, string? type);

    [LoggerMessage(EventId = 5010, Level = LogLevel.Error,
        Message = "相机 {Id} 初始化失败，使用该相机的配方将返回错误码 {Code}")]
    public static partial void CameraInitFailed(ILogger logger, Exception ex, string id, int code);

    [LoggerMessage(EventId = 5011, Level = LogLevel.Warning,
        Message = "配置中存在无 Id 的光源控制器条目，已跳过")]
    public static partial void LightEntryMissingId(ILogger logger);

    [LoggerMessage(EventId = 5012, Level = LogLevel.Warning,
        Message = "光源控制器 Id 重复: {Id}（后条目已跳过，请检查 appsettings.json）")]
    public static partial void LightDuplicateId(ILogger logger, string id);

    [LoggerMessage(EventId = 5013, Level = LogLevel.Warning,
        Message = "光源控制器 {Id} 类型 {Type} 无工厂（实现 ILightControllerFactory 并调用 LightControllerTypeRegistry.Register 一行接入）")]
    public static partial void LightNoFactory(ILogger logger, string id, string? type);

    [LoggerMessage(EventId = 5014, Level = LogLevel.Information,
        Message = "光源控制器 {Id} 已注册（{Type}）")]
    public static partial void LightRegistered(ILogger logger, string id, string? type);

    [LoggerMessage(EventId = 5015, Level = LogLevel.Error,
        Message = "光源控制器 {Id} 初始化失败，使用该控制器的配方将返回错误码 {Code}")]
    public static partial void LightInitFailed(ILogger logger, Exception ex, string id, int code);

    [LoggerMessage(EventId = 5016, Level = LogLevel.Warning,
        Message = "标定档案 {File} 加载失败: {Error}")]
    public static partial void CalibrationFileLoadFailed(ILogger logger, string file, string error);

    [LoggerMessage(EventId = 5017, Level = LogLevel.Warning,
        Message = "标定质量警告: {Warning}")]
    public static partial void CalibrationQualityWarning(ILogger logger, string warning);

    [LoggerMessage(EventId = 5018, Level = LogLevel.Information,
        Message = "MaxConcurrent/TcpBacklog 改动已保存到配置文件，需重启程序后生效")]
    public static partial void RuntimeSyncRestartRequired(ILogger logger);

    [LoggerMessage(EventId = 5019, Level = LogLevel.Warning, Message = "{Warning}")]
    public static partial void BootWarning(ILogger logger, string warning);
}
