namespace RobotVision.Hosting;

public sealed class AppConfig
{
    /// <summary>硬件相机单帧采集超时（ms）；相机管理页可编辑，须小于总超时 TimeoutMs。</summary>
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

    /// <summary>
    /// 工位数据根（配方/标定/回放/失败图/结果等）。空 = 相对路径仍按 exe/CWD 解析（便携部署）。
    /// 模型目录不走此根。启动时 <see cref="DataRootBinder.Apply"/> 把未设绝对路径的项绑到此目录下。
    /// </summary>
    public string DataRoot { get; set; } = "";

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

    /// <summary>结果日志（JSON Lines + 本机 SQLite）：成功与失败结果的原始留档，供追溯/统计/分析页。</summary>
    public ResultLogConfig ResultLog { get; set; } = new();

    /// <summary>成功产品现场图留存（默认关）：开启后成功检测也存图，供复检/工艺分析。</summary>
    public CaptureSuccessConfig CaptureSuccess { get; set; } = new();

    /// <summary>本机 CPU 对话助手（连接 llama-server，不在视觉进程内加载权重）。</summary>
    public ChatConfig Chat { get; set; } = new();

    /// <summary>运行监控页叠加模式：默认与配方试触发一致。</summary>
    public MonitorOverlayMode MonitorOverlayMode { get; set; } = MonitorOverlayMode.MatchRecipeTest;

    public List<CameraConfig> Cameras { get; set; } = [];

    public List<LightControllerConfig> LightControllers { get; set; } = [];

    /// <summary>界面主题：<see cref="UiThemes.Dark"/> 或 <see cref="UiThemes.Light"/>。</summary>
    public string UiTheme { get; set; } = UiThemes.Dark;
}

/// <summary>appsettings / 设置页共用的界面主题键。</summary>
public static class UiThemes
{
    public const string Dark = "Dark";
    public const string Light = "Light";

    public static readonly IReadOnlyList<string> All = [Dark, Light];

    public static string Normalize(string? value) =>
        string.Equals(value, Light, StringComparison.OrdinalIgnoreCase) ? Light : Dark;

    public static string Label(string theme) =>
        string.Equals(theme, Light, StringComparison.OrdinalIgnoreCase) ? "浅色" : "深色";
}

/// <summary>运行监控页检测叠加策略。</summary>
public enum MonitorOverlayMode
{
    /// <summary>只画位姿本体（掩码/框/分数/十字等），不画检测 ROI 与卡尺探针。</summary>
    Production = 0,

    /// <summary>与配方页试触发结果图一致（含检测 ROI、卡尺调试层）。</summary>
    MatchRecipeTest = 1,
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
    /// <summary>推理 Provider：OpenVinoGpu（默认，Intel 核显）。GPU 会话创建失败时自动回退 OpenVINO CPU 并打警告。
    /// 也可显式设为 OpenVinoCpu。YoloDotNet 每进程只能一种 EP，产线不混编 CPU+OpenVINO 池。</summary>
    public string Provider { get; set; } = "OpenVinoGpu";

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

/// <summary>
/// 结果日志配置：每次触发的成功/失败结果原始留档。
/// JSON Lines 便于 Excel/文本直接打开；本机 SQLite 供分析页按时间/配方查询。
/// 两者默认同时写（后台异步，不影响检测节拍）；可用 Jsonl/Sqlite 分别关掉。
/// </summary>
public sealed class ResultLogConfig
{
    public bool Enabled { get; set; } = true;

    /// <summary>日志目录（相对路径按目录解析规则锚定：exe 目录优先，工作目录回退）。</summary>
    public string Folder { get; set; } = "data/results";

    /// <summary>按天滚动保留天数（JSONL 文件与 SQLite 行共用）；≤0 不清理。</summary>
    public int RetainedDays { get; set; } = 30;

    /// <summary>继续按天追加 JSON Lines（results-yyyy-MM-dd.jsonl）。</summary>
    public bool Jsonl { get; set; } = true;

    /// <summary>写入本机 SQLite（默认 results.db），供按配方/时间/错误码查询。</summary>
    public bool Sqlite { get; set; } = true;

    /// <summary>库文件名（仅文件名，位于 Folder 下；非法名回退 results.db）。</summary>
    public string SqliteFile { get; set; } = "results.db";
}

/// <summary>
/// 成功产品现场图留存（默认关）：开启后成功检测也保存去畸变图 + 元数据，
/// 供复检/工艺分析；默认关闭避免产线高速节拍下磁盘暴涨。
/// </summary>
public sealed class CaptureSuccessConfig
{
    public bool Enabled { get; set; }

    public string Folder { get; set; } = "data/captures";

    /// <summary>缩图最大宽度（0 = 原图；产线量大利建议开缩图，如 1280）。</summary>
    public int MaxWidth { get; set; }

    /// <summary>按天目录保留天数；≤0 不清理。</summary>
    public int RetainedDays { get; set; } = 30;
}

/// <summary>
/// 本机对话：WPF 只做界面，推理走 OpenAI 兼容 HTTP（默认 llama-server :8080）。
/// 不加载 HuggingFace BF16；请用 Q4 GGUF + CPU llama-server。
/// </summary>
public sealed class ChatConfig
{
    public string Endpoint { get; set; } = "http://127.0.0.1:8080";

    /// <summary>请求中的 model 字段；llama-server 可填任意占位名。</summary>
    public string Model { get; set; } = "qwen";

    public int MaxTokens { get; set; } = 512;

    /// <summary>人设与工具纪律；空则回退 <see cref="Chat.ChatSystemPrompt.Default"/>。</summary>
    public string SystemPrompt { get; set; } = Chat.ChatSystemPrompt.Default;

    /// <summary>true = 对话页自动拉起 llama-server（CPU）。</summary>
    public bool AutoStart { get; set; } = true;

    /// <summary>llama-server.exe；空则按常见目录查找。</summary>
    public string LlamaServerPath { get; set; } = @"E:\光模块\llm\llama-cpp\llama-server.exe";

    /// <summary>Q4 GGUF 路径。</summary>
    public string GgufPath { get; set; } = @"E:\光模块\llm\Qwen3.5-4B-Q4_K_M.gguf";

    public int Port { get; set; } = 8080;

    /// <summary>CPU 线程数；14700 建议 8（性能核）。</summary>
    public int Threads { get; set; } = 8;

    /// <summary>llama-server 上下文长度（token）。CPU 4B Q4 默认 8192。</summary>
    public int ContextSize { get; set; } = 8192;

    /// <summary>发给模型的历史 token 预算；0 = 按 ContextSize 自动估算。</summary>
    public int HistoryTokenBudget { get; set; }

    /// <summary>单轮对话内工具调用最大轮次。</summary>
    public int MaxToolRounds { get; set; } = 8;

    /// <summary>拍照等工具结果是否以 base64 附图发给模型（需视觉模型或 llama-server 多模态支持）。</summary>
    public bool SendImagesToModel { get; set; } = true;

    /// <summary>附图 JPEG 缩放最长边（px），控制 token 与带宽。</summary>
    public int ImageMaxEdgePx { get; set; } = 768;

    /// <summary>危险写操作须在参数中 confirm:true，且用户最近一条消息明确同意。</summary>
    public bool RequireDangerousActionConfirm { get; set; } = true;

    /// <summary>是否记录工具调用审计 JSONL。</summary>
    public bool AuditEnabled { get; set; } = true;

    /// <summary>审计日志目录。</summary>
    public string AuditFolder { get; set; } = "data/chat-audit";

    /// <summary>对话拍照 PNG 目录。</summary>
    public string CaptureFolder { get; set; } = "data/chat-captures";

    /// <summary>审计日志保留天数；≤0 不清理。</summary>
    public int AuditRetainedDays { get; set; } = 90;

    /// <summary>等待模型加载的秒数。</summary>
    public int LoadTimeoutSeconds { get; set; } = 180;
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

    /// <summary>Basler / GigEVision：序列号或 IP。留空仅当现场恰好一台时绑定；多台必须填写，对不上不会回落第一台。</summary>
    public string DeviceId { get; set; } = "";

    /// <summary>Basler / GigEVision：曝光时间（µs）；null = 不下发，使用相机当前值。</summary>
    public double? ExposureTimeUs { get; set; }

    /// <summary>Basler / GigEVision：增益（dB 或机型原始单位）；null = 不下发。</summary>
    public double? Gain { get; set; }

    /// <summary>Basler / GigEVision：单帧采集超时（ms）。合法且小于总超时的值启动时保留；0 或越界才改回默认 60s。</summary>
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

    public static string ResolveRecipesFolder(this AppConfig cfg) => cfg.ResolveDataPath(cfg.RecipesFolder);

    /// <summary>
    /// 工位数据根的绝对路径。未配置时为空。相对 DataRoot 按 <see cref="ResolveFolder"/> 解析。
    /// </summary>
    public static string ResolveDataRoot(this AppConfig cfg)
    {
        if (string.IsNullOrWhiteSpace(cfg.DataRoot))
            return "";
        var root = cfg.DataRoot.Trim();
        return Path.IsPathRooted(root) ? Path.GetFullPath(root) : ResolveFolder(root);
    }

    /// <summary>
    /// 工位数据路径：已是绝对路径则原样；配置了 DataRoot 时相对路径落在其下；否则与 <see cref="ResolveFolder"/> 相同。
    /// 模型目录请用 <see cref="ResolveModelsFolder"/>，不走 DataRoot。
    /// </summary>
    public static string ResolveDataPath(this AppConfig cfg, string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return path;
        if (Path.IsPathRooted(path))
            return Path.GetFullPath(path);
        var root = cfg.ResolveDataRoot();
        if (!string.IsNullOrEmpty(root))
            return Path.GetFullPath(Path.Combine(root, path));
        return ResolveFolder(path);
    }

    /// <summary>
    /// 运行时配方目录：未配 DataRoot 时相对路径固定锚定 exe 目录（不回退仓库根），
    /// 避免 UI 改 bin 而列表读源码目录。配了 DataRoot 则落在数据根下。
    /// 首次使用时从 <c>recipes.samples</c> 拷入示例，之后删除的配方不会因编译/重启被拷回。
    /// </summary>
    public static string ResolveAndPrepareRecipesFolder(this AppConfig cfg)
    {
        string folder;
        if (!string.IsNullOrWhiteSpace(cfg.RecipesFolder) && Path.IsPathRooted(cfg.RecipesFolder))
            folder = Path.GetFullPath(cfg.RecipesFolder);
        else if (!string.IsNullOrWhiteSpace(cfg.DataRoot))
            folder = cfg.ResolveDataPath(string.IsNullOrWhiteSpace(cfg.RecipesFolder) ? "recipes" : cfg.RecipesFolder);
        else
            folder = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,
                string.IsNullOrWhiteSpace(cfg.RecipesFolder) ? "recipes" : cfg.RecipesFolder));
        Directory.CreateDirectory(folder);
        RecipeSampleSeeder.SeedIfNeeded(folder);
        return folder;
    }

    public static string ResolveModelsFolder(this AppConfig cfg) => ResolveFolder(cfg.ModelsFolder);

    public static string ResolveCalibrationFolder(this AppConfig cfg) => cfg.ResolveDataPath(cfg.CalibrationFolder);

    public static string ResolveMetricsFolder(this AppConfig cfg) => cfg.ResolveDataPath(cfg.ProcessHealth.Folder);

    public static string ResolveResultsFolder(this AppConfig cfg) => cfg.ResolveDataPath(cfg.ResultLog.Folder);

    public static string ResolveChatCapturesFolder(this AppConfig cfg) =>
        cfg.ResolveDataPath(string.IsNullOrWhiteSpace(cfg.Chat.CaptureFolder)
            ? "data/chat-captures"
            : cfg.Chat.CaptureFolder);

    /// <summary>
    /// 启动时对齐超时下限：请求总超时 ≥90s（避免旧配置 3s/5s 带病运行）。
    /// 硬件相机 GrabTimeoutMs 仅在未设或已大于等于总超时时才改成默认 60s，不覆盖合法自定义值。
    /// </summary>
    public static void NormalizeVisionTiming(this AppConfig cfg)
    {
        if (cfg.TimeoutMs < AppConfig.DefaultRequestTimeoutMs)
            cfg.TimeoutMs = AppConfig.DefaultRequestTimeoutMs;

        foreach (var camera in cfg.Cameras)
        {
            if (!IsHardwareCameraType(camera.Type))
                continue;
            if (camera.GrabTimeoutMs <= 0 || camera.GrabTimeoutMs >= cfg.TimeoutMs)
                camera.GrabTimeoutMs = Math.Min(AppConfig.DefaultGrabTimeoutMs, Math.Max(1, cfg.TimeoutMs - 1));
        }
    }

    public static bool IsHardwareCameraType(string type) =>
        string.Equals(type, "Basler", StringComparison.OrdinalIgnoreCase)
        || string.Equals(type, "GigEVision", StringComparison.OrdinalIgnoreCase);

    public static bool UsesGrabTimeout(this CameraConfig camera) =>
        IsHardwareCameraType(camera.Type);

    public static string ResolveCameraFolder(this CameraConfig camera, AppConfig? app = null) =>
        app is null ? ResolveFolder(camera.Folder) : app.ResolveDataPath(camera.Folder);
}
