namespace RobotVision.Hosting;

public sealed class AppConfig
{
    /// <summary>硬件相机单帧采集超时（ms）；UI 不暴露，保存相机时固定写入此值。</summary>
    public const int DefaultGrabTimeoutMs = 60_000;

    /// <summary>单次 TRIGGER 总超时（ms）；须大于采集超时，为推理/坐标变换留裕量。</summary>
    public const int DefaultRequestTimeoutMs = 90_000;

    public string IpAddress { get; set; } = "0.0.0.0";

    public int TcpPort { get; set; } = 9999;

    /// <summary>单次 TRIGGER 的总超时（ms）。与空闲断线无关。</summary>
    public int TimeoutMs { get; set; } = DefaultRequestTimeoutMs;

    /// <summary>
    /// TCP 读侧空闲超时（ms）。0 = 永久保持连接（默认，适配 PLC 节拍间隙）；
    /// 需要回收死连接时可设 30 天 = 2592000000。不再与 TimeoutMs 绑定。
    /// </summary>
    public long IdleTimeoutMs { get; set; }

    /// <summary>并发 TCP 连接上限，0 表示不限。</summary>
    public int MaxConnections { get; set; } = 0;

    /// <summary>监听 backlog（内核排队待 accept 的连接数）。</summary>
    public int TcpBacklog { get; set; } = 16;

    /// <summary>IP 白名单（空 = 允许所有；条目支持 192.168.* 前缀通配）。</summary>
    public List<string> IpWhitelist { get; set; } = [];

    public string RecipesFolder { get; set; } = "recipes";

    public string ModelsFolder { get; set; } = "models";

    public string CalibrationFolder { get; set; } = "data/calibration";

    /// <summary>最大并发排队请求数（排队 + 执行总数），超过立即拒绝（防爆内存/排队雪崩）。</summary>
    public int MaxQueueDepth { get; set; } = 4;

    /// <summary>并发执行上限（同时处理的工位数；取图按相机串行、推理按模型串行）。</summary>
    public int MaxConcurrent { get; set; } = 2;

    /// <summary>文件日志配置（工控机无头运行时的现场留痕）。</summary>
    public FileLoggingConfig FileLogging { get; set; } = new();

    /// <summary>失败现场图像留存配置（远程排障还原失败时刻的画面）。</summary>
    public FailureImageConfig FailureImage { get; set; } = new();

    /// <summary>推理配置（Provider / 会话上限）。</summary>
    public InferenceConfig Inference { get; set; } = new();

    /// <summary>拍照位姿校验（TRIGGER,配方名,X,Y,RZ 带位姿时与 OnArm 外参档案比对）。</summary>
    public PoseCheckConfig PoseCheck { get; set; } = new();

    /// <summary>模型/标定资产完整性（配方钉扎哈希 + 可选全局清单）。</summary>
    public AssetIntegrityConfig AssetIntegrity { get; set; } = new();

    /// <summary>过程能力落盘与连续失败联锁。</summary>
    public ProcessHealthConfig ProcessHealth { get; set; } = new();

    public List<CameraConfig> Cameras { get; set; } = [];

    public List<LightControllerConfig> LightControllers { get; set; } = [];
}

public sealed class FailureImageConfig
{
    public bool Enabled { get; set; } = true;

    /// <summary>留存目录（相对路径按目录解析规则锚定：exe 目录优先，工作目录回退）。</summary>
    public string Folder { get; set; } = "data/failures";

    /// <summary>滚动保留最近 N 张（含元数据）；≤0 表示不自动清理。</summary>
    public int RetainedCount { get; set; } = 200;

    /// <summary>按时间保留最近 N 天；≤0 表示不按时间清理。与数量配额取更严格者。</summary>
    public int RetainedDays { get; set; }
}

public sealed class InferenceConfig
{
    /// <summary>推理 Provider：Cpu（默认）。换 GPU 需引用对应 YoloDotNet.ExecutionProvider
    /// 包并在 YoloDotNetEngineFactory 补充分支（未实现的 Provider 首次加载模型时报错）。</summary>
    public string Provider { get; set; } = "Cpu";

    /// <summary>LRU 会话上限：超过后卸载最久未使用的会话（0 或负数 = 不限制）。</summary>
    public int MaxSessions { get; set; } = 8;
}

/// <summary>拍照位姿校验配置：OnArm（相机装末端）工位的 TRIGGER 位姿一致性检查。</summary>
public sealed class PoseCheckConfig
{
    /// <summary>总开关。false = PLC 上报位姿也不校验（调试/过渡期用，正式产线应开启）。</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>拍照点 TCP 平面偏差容差（mm，XY 欧氏距离，超过返回 1012）。</summary>
    public double XyToleranceMm { get; set; } = 0.5;

    /// <summary>拍照点第 4 轴角度容差（deg，归一化差，超过返回 1012）。</summary>
    public double RzToleranceDeg { get; set; } = 0.5;
}

/// <summary>模型/标定 SHA-256 钉扎。Enabled=false 时 TRIGGER 不校验哈希（仅调试）。</summary>
public sealed class AssetIntegrityConfig
{
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// true = 配方引用的每个模型必须出现在 models/manifest.json 且哈希一致。
    /// 默认 false：仅校验配方里已钉扎的哈希（未钉扎的旧配方行为不变）。
    /// </summary>
    public bool RequireManifest { get; set; }
}

/// <summary>过程能力：按日 TSV 追加 + 累计 JSON；连续过程失败达到阈值后返回 1018。</summary>
public sealed class ProcessHealthConfig
{
    public bool Enabled { get; set; } = true;

    public string Folder { get; set; } = "data/metrics";

    /// <summary>连续过程失败达到该次数后联锁（0 = 只统计不联锁）。</summary>
    public int ConsecutiveFailLimit { get; set; } = 5;

    /// <summary>达到阈值后拒绝 TRIGGER（1018）。false = 只记录/展示，不拦截。</summary>
    public bool InhibitOnLimit { get; set; } = true;

    /// <summary>按日 TSV 保留天数；≤0 不按天清理。</summary>
    public int RetainedDays { get; set; } = 90;
}

public sealed class FileLoggingConfig
{
    public bool Enabled { get; set; } = true;

    /// <summary>日志目录（相对路径按目录解析规则锚定：exe 目录优先，工作目录回退）。</summary>
    public string Folder { get; set; } = "logs";

    /// <summary>按天滚动，保留天数。</summary>
    public int RetainedDays { get; set; } = 30;
}

public sealed class CameraConfig
{
    public string Id { get; set; } = "";

    /// <summary>可选显示名（界面展示用）；留空时各处回退为 <see cref="Id"/>。</summary>
    public string Name { get; set; } = "";

    /// <summary>File = 文件夹回放；Basler = pylon；GigEVision = 开源 GigE Vision；Virtual = 程序生成。</summary>
    public string Type { get; set; } = "Basler";

    /// <summary>File 相机：回放图片目录。</summary>
    public string Folder { get; set; } = "";

    /// <summary>File 相机：回放帧间隔（ms），0 = 不限速。与 Virtual 的 IntervalMs 共用字段。</summary>
    public int IntervalMs { get; set; } = 0;

    /// <summary>Basler / GigEVision：序列号或 IP；留空时自动绑定枚举到的第一台。</summary>
    public string DeviceId { get; set; } = "";

    /// <summary>Basler / GigEVision：曝光时间（µs）；null = 不下发，使用相机当前值。</summary>
    public double? ExposureTimeUs { get; set; }

    /// <summary>Basler / GigEVision：增益（dB 或机型原始单位）；null = 不下发。</summary>
    public double? Gain { get; set; }

    /// <summary>Basler / GigEVision：单帧采集超时（ms），固定为 <see cref="AppConfig.DefaultGrabTimeoutMs"/>。</summary>
    public int GrabTimeoutMs { get; set; } = AppConfig.DefaultGrabTimeoutMs;

    /// <summary>Virtual 相机：生成图像宽（px）。</summary>
    public int Width { get; set; } = 1280;

    /// <summary>Virtual 相机：生成图像高（px）。</summary>
    public int Height { get; set; } = 960;

    /// <summary>Virtual 相机：图案 Chessboard（默认）/ Shapes / Bars。</summary>
    public string Pattern { get; set; } = "Chessboard";

    /// <summary>Virtual 相机：高斯噪声 sigma，0 = 无噪声。</summary>
    public double NoiseSigma { get; set; } = 0;

    /// <summary>Virtual 相机：棋盘格单元边长（px），内角点数 = 宽/格/2-1 × 高/格/2-1。</summary>
    public int ChessCellPx { get; set; } = 40;
}

public sealed class LightControllerConfig
{
    public string Id { get; set; } = "";

    /// <summary>
    /// None = 无操作虚拟（未接硬件时的调试兜底）；
    /// Network = UDP/TCP 网络控制器；Serial = RS232/RS485 串口控制器。
    /// </summary>
    public string Type { get; set; } = "None";

    /// <summary>串口控制器：串口名（如 COM3）。</summary>
    public string Port { get; set; } = "";

    public int BaudRate { get; set; } = 9600;

    /// <summary>Modbus 控制器：从站地址。</summary>
    public int Address { get; set; } = 1;

    /// <summary>网络控制器：端点（host:port），UDP/TCP 共用（参照 VPDLFramework ECLightControl 的 ControllerIPPort）。</summary>
    public string Endpoint { get; set; } = "";

    /// <summary>网络控制器：协议（Tcp / Udp，默认 Tcp；参照 ECLightControl 的 LightEthernetType，0=UDP 1=TCP）。</summary>
    public string Protocol { get; set; } = "Tcp";

    /// <summary>网络控制器：本机绑定端点（host:port，可选；UDP 接收应答时用于固定本地端口）。</summary>
    public string LocalEndpoint { get; set; } = "";

    /// <summary>控制命令超时（ms）。</summary>
    public int TimeoutMs { get; set; } = 200;

    /// <summary>网络控制器：TCP 断线重连尝试次数（参照 ECLightControl.ReconnectTCP）。</summary>
    public int ReconnectAttempts { get; set; } = 3;
}

public static class AppConfigExtensions
{
    /// <summary>
    /// 相对路径以 exe 所在目录为基准解析（部署布局：exe 旁放 recipes/models/data）。
    /// </summary>
    public static string ResolveBase(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || Path.IsPathRooted(path))
            return path;
        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, path));
    }

    /// <summary>
    /// 目录解析（存在性优先）：先 exe 目录（部署布局），再当前工作目录
    /// （开发布局：dotnet run 从仓库根启动，资产在仓库根而非 bin）。
    /// 两者都不存在时回到 exe 目录（由调用方决定创建与否），并允许告警。
    /// Windows 服务/计划任务的 CWD 是 System32，绝不能只按 CWD 解析。
    /// </summary>
    public static string ResolveFolder(string path, Action<string>? onFallbackToCwd = null)
    {
        if (string.IsNullOrWhiteSpace(path) || Path.IsPathRooted(path))
            return path;

        var exeAnchored = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, path));
        if (Directory.Exists(exeAnchored))
            return exeAnchored;

        var cwdAnchored = Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, path));
        if (Directory.Exists(cwdAnchored))
        {
            onFallbackToCwd?.Invoke(cwdAnchored);
            return cwdAnchored;
        }

        return exeAnchored;
    }

    public static string ResolveRecipesFolder(this AppConfig cfg) => ResolveFolder(cfg.RecipesFolder);

    /// <summary>
    /// 运行时配方目录：相对路径固定锚定 exe 目录（不回退仓库根），避免 UI 改 bin 而列表读源码目录。
    /// 首次使用时从 <c>recipes.samples</c> 拷入示例，之后删除的配方不会因编译/重启被拷回。
    /// </summary>
    public static string ResolveAndPrepareRecipesFolder(this AppConfig cfg)
    {
        var folder = string.IsNullOrWhiteSpace(cfg.RecipesFolder)
            ? Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "recipes"))
            : Path.IsPathRooted(cfg.RecipesFolder)
                ? Path.GetFullPath(cfg.RecipesFolder)
                : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, cfg.RecipesFolder));
        Directory.CreateDirectory(folder);
        RecipeSampleSeeder.SeedIfNeeded(folder);
        return folder;
    }

    public static string ResolveModelsFolder(this AppConfig cfg) => ResolveFolder(cfg.ModelsFolder);

    public static string ResolveCalibrationFolder(this AppConfig cfg) => ResolveFolder(cfg.CalibrationFolder);

    public static string ResolveMetricsFolder(this AppConfig cfg) => ResolveFolder(cfg.ProcessHealth.Folder);

    /// <summary>
    /// 启动时对齐固定超时策略：采集 60s、请求总超时 ≥90s（UI 不暴露，避免旧配置 3s/5s 带病运行）。
    /// </summary>
    public static void NormalizeVisionTiming(this AppConfig cfg)
    {
        if (cfg.TimeoutMs < AppConfig.DefaultRequestTimeoutMs)
            cfg.TimeoutMs = AppConfig.DefaultRequestTimeoutMs;

        foreach (var camera in cfg.Cameras)
        {
            if (!IsHardwareCameraType(camera.Type))
                continue;
            camera.GrabTimeoutMs = AppConfig.DefaultGrabTimeoutMs;
        }
    }

    private static bool IsHardwareCameraType(string type) =>
        string.Equals(type, "Basler", StringComparison.OrdinalIgnoreCase)
        || string.Equals(type, "GigEVision", StringComparison.OrdinalIgnoreCase);

    public static string ResolveCameraFolder(this CameraConfig cfg) => ResolveFolder(cfg.Folder);
}
