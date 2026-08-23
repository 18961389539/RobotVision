namespace RobotVision.Hosting;

public sealed class AppConfig
{
    public string IpAddress { get; set; } = "0.0.0.0";

    public int TcpPort { get; set; } = 9999;

    /// <summary>单次 TRIGGER 的总超时（ms）。</summary>
    public int TimeoutMs { get; set; } = 5000;

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

    /// <summary>File = 文件夹回放；Basler = pylon .NET 真实相机；其他品牌 SDK 接入后在此扩展。</summary>
    public string Type { get; set; } = "Basler";

    /// <summary>File 相机：回放图片目录。</summary>
    public string Folder { get; set; } = "";

    /// <summary>File 相机：回放帧间隔（ms），0 = 不限速。与 Virtual 的 IntervalMs 共用字段。</summary>
    public int IntervalMs { get; set; } = 0;

    /// <summary>Basler 相机：序列号；留空时自动绑定枚举到的第一台。</summary>
    public string DeviceId { get; set; } = "";

    /// <summary>Basler 相机：曝光时间（µs）；null = 不下发，使用相机当前值。</summary>
    public double? ExposureTimeUs { get; set; }

    /// <summary>Basler 相机：增益（dB）；null = 不下发。</summary>
    public double? Gain { get; set; }

    /// <summary>Basler 相机：单帧采集超时（ms），需小于配方总超时 TimeoutMs。</summary>
    public int GrabTimeoutMs { get; set; } = 3000;

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
    /// None = 无操作虚拟（未接硬件时的调试兜底）；Serial / Modbus / Tcp
    /// 待实现 ILightController 后在此接入（同相机注册处的扩展模式）。
    /// </summary>
    public string Type { get; set; } = "None";

    /// <summary>串口控制器：串口名（如 COM3）。</summary>
    public string Port { get; set; } = "";

    public int BaudRate { get; set; } = 9600;

    /// <summary>Modbus 控制器：从站地址。</summary>
    public int Address { get; set; } = 1;

    /// <summary>TCP 控制器：端点（host:port）。</summary>
    public string Endpoint { get; set; } = "";

    /// <summary>控制命令超时（ms）。</summary>
    public int TimeoutMs { get; set; } = 200;
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

    public static string ResolveModelsFolder(this AppConfig cfg) => ResolveFolder(cfg.ModelsFolder);

    public static string ResolveCalibrationFolder(this AppConfig cfg) => ResolveFolder(cfg.CalibrationFolder);

    public static string ResolveCameraFolder(this CameraConfig cfg) => ResolveFolder(cfg.Folder);
}
