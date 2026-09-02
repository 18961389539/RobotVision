using System.Globalization;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using RobotVision.Hosting;
using RobotVision.WpfHost.Shared;

namespace RobotVision.WpfHost.Features.Communication;

/// <summary>连接列表行：统计随请求事件实时更新。</summary>
public partial class ClientRow : ObservableObject
{
    public ClientRow(TcpClientView snapshot)
    {
        Id = snapshot.Id;
        Remote = snapshot.Remote;
        ConnectedAt = snapshot.ConnectedAt;
        _requests = snapshot.Requests;
        _bytesIn = snapshot.BytesReceived;
        _bytesOut = snapshot.BytesSent;
        _lastRequest = snapshot.LastRequest;
        _lastRequestAt = snapshot.LastRequestAt;
    }

    public long Id { get; }
    public string Remote { get; }
    public DateTime ConnectedAt { get; }

    public string Ip => Remote.Contains(':', StringComparison.Ordinal) ? Remote[..Remote.LastIndexOf(":", StringComparison.Ordinal)] : Remote;
    public string Port => Remote.Contains(':', StringComparison.Ordinal) ? Remote[(Remote.LastIndexOf(":", StringComparison.Ordinal) + 1)..] : "";

    [ObservableProperty] private long _requests;
    [ObservableProperty] private long _bytesIn;
    [ObservableProperty] private long _bytesOut;
    [ObservableProperty] private string? _lastRequest;
    [ObservableProperty] private DateTime? _lastRequestAt;
}

/// <summary>请求历史行（不可变；列表按最新在前插入，超限裁剪）。</summary>
public sealed record RequestRow(
    DateTime Time, long ClientId, string Client,
    string Request, string Reply, bool Ok, double ElapsedMs)
{
    public string TimeText => Time.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture);
    public string Result => Reply == "处理中" ? "…" : Ok ? "OK" : "ERR";
    public string ElapsedText => $"{ElapsedMs:0} ms";
    public string ReplyTrimmed => Reply.Length <= 96 ? Reply : Reply[..96] + "…";
}

/// <summary>按配方聚合的运行统计行（1 秒周期重建，量级小无绑定开销）。
/// Rate = 成功率 0~1（进度条宽度）；Tier = 颜色档（good≥0.9 / warn≥0.6 / poor）。</summary>
public sealed record RecipeStatsRow(
    string Recipe, long Total, long Ok, long Failed,
    string SuccessRate, string AvgMs, string LastMs, string LastAt,
    double Rate, string Tier, int ConsecutiveFails = 0);

/// <summary>
/// 通信监控：连接列表、请求历史、主动断开与服务启停、按配方运行统计。
/// 事件来自套接字线程，统一封送到 UI 线程后更新集合。
/// </summary>
public partial class CommunicationViewModel : ObservableObject, IDisposable
{
    private const int MaxRequestRows = 200;

    private readonly ITcpRuntime _tcp;
    private readonly VisionService _vision;
    private readonly AppConfig _cfg;
    private readonly IDialogService _dialogs;
    private readonly ILogger<CommunicationViewModel> _log;
    private readonly DispatcherTimer _timer;
    private bool _syncingPlcDebug;

    public ObservableCollection<ClientRow> Clients { get; } = [];

    public ObservableCollection<RequestRow> Requests { get; } = [];

    public ObservableCollection<RecipeStatsRow> StatsRows { get; } = [];

    public bool HasRequests => Requests.Count > 0;

    public bool HasStats => StatsRows.Count > 0;

    [ObservableProperty] private string _serviceStatus = "";
    [ObservableProperty] private string _summaryText = "";
    [ObservableProperty] private string _statsSummary = "";
    [ObservableProperty] private string _toggleButtonLabel = "停止服务";

    // ---- 方案 A：顶部统计迷你卡（1 秒周期刷新） ----

    /// <summary>当前连接数。</summary>
    [ObservableProperty] private int _connectedClients;

    /// <summary>累计请求数（含 STATUS/PING 等非触发请求）。</summary>
    [ObservableProperty] private string _totalRequestsText = "0";

    /// <summary>触发成功率（按配方统计）。</summary>
    [ObservableProperty] private string _successRateText = "—";

    /// <summary>触发平均耗时（按配方统计）。</summary>
    [ObservableProperty] private string _avgMsText = "—";

    /// <summary>最近一条 TCP 行（含处理中），避免只看「运行统计」时误以为没有报文。</summary>
    [ObservableProperty] private string _lastTcpLineText = "尚无 TCP 请求";

    [ObservableProperty] private string _interlockText = "";

    public bool HasInterlock => !string.IsNullOrEmpty(InterlockText);

    public bool HasPlcDebugWarning => PlcAlwaysOkMode;

    public string PlcDebugWarningText =>
        $"PLC 调试模式：TCP 应答一律改写为 OK（默认坐标 {_cfg.PlcDebug.DefaultX:0.###},{_cfg.PlcDebug.DefaultY:0.###},{_cfg.PlcDebug.DefaultRz:0.###}）。视觉仍照常执行，产线务必关闭。";

    public bool PlcAlwaysOkMode
    {
        get => _tcp.PlcAlwaysOkMode;
        set => SetPlcAlwaysOkMode(value);
    }

    public CommunicationViewModel(
        ITcpRuntime tcp,
        VisionService vision,
        AppConfig cfg,
        IDialogService dialogs,
        ILogger<CommunicationViewModel> log)
    {
        _tcp = tcp;
        _vision = vision;
        _cfg = cfg;
        _dialogs = dialogs;
        _log = log;
        foreach (var snapshot in _tcp.GetClients())
            Clients.Add(new ClientRow(snapshot));

        foreach (var record in _tcp.GetRecentRequests())
            Requests.Add(ToRow(record));
        NotifyLists();

        _tcp.ClientConnected += OnClientConnected;
        _tcp.ClientDisconnected += OnClientDisconnected;
        _tcp.RequestStarted += OnRequestStarted;
        _tcp.RequestProcessed += OnRequestProcessed;

        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += (_, _) => RefreshStatus();
        RefreshStatus();
    }

    /// <summary>页面可见时刷新状态（配合 Unloaded 停止，避免后台空转）。</summary>
    public void StartTimer() => _timer.Start();

    public void StopTimer() => _timer.Stop();

    /// <summary>页面 Unload 时退订 TCP 事件并停表。</summary>
    public void Dispose()
    {
        _tcp.ClientConnected -= OnClientConnected;
        _tcp.ClientDisconnected -= OnClientDisconnected;
        _tcp.RequestStarted -= OnRequestStarted;
        _tcp.RequestProcessed -= OnRequestProcessed;
        _timer.Stop();
    }

    private void OnClientConnected(TcpClientView snapshot) =>
        Dispatch(() => Clients.Add(new ClientRow(snapshot)));

    private void OnClientDisconnected(TcpClientView snapshot) => Dispatch(() =>
    {
        for (var i = 0; i < Clients.Count; i++)
        {
            if (Clients[i].Id == snapshot.Id)
            {
                Clients.RemoveAt(i);
                break;
            }
        }
    });

    private void OnRequestStarted(TcpRequestView record) => Dispatch(() =>
    {
        var row = Clients.FirstOrDefault(c => c.Id == record.ClientId);
        if (row is not null)
        {
            row.LastRequest = record.Request;
            row.LastRequestAt = record.Time;
        }

        Requests.Insert(0, new RequestRow(
            record.Time, record.ClientId, record.Client,
            record.Request, "处理中", false, 0));
        while (Requests.Count > MaxRequestRows)
            Requests.RemoveAt(Requests.Count - 1);
        LastTcpLineText = $"{record.Request} → 处理中";
        NotifyLists();
    });

    private void OnRequestProcessed(TcpRequestView record) => Dispatch(() =>
    {
        var row = Clients.FirstOrDefault(c => c.Id == record.ClientId);
        if (row is not null)
        {
            row.Requests++;
            row.BytesIn += record.BytesIn;
            row.BytesOut += record.BytesOut;
            row.LastRequest = record.Request;
            row.LastRequestAt = record.Time;
        }

        var pending = Requests.FirstOrDefault(r =>
            r.Time == record.Time && r.ClientId == record.ClientId && r.Reply == "处理中");
        var completed = ToRow(record);
        if (pending is not null)
        {
            var index = Requests.IndexOf(pending);
            if (index >= 0)
                Requests[index] = completed;
        }
        else
        {
            Requests.Insert(0, completed);
            while (Requests.Count > MaxRequestRows)
                Requests.RemoveAt(Requests.Count - 1);
        }

        LastTcpLineText = FormatLastLine(completed);
        NotifyLists();
    });

    private static RequestRow ToRow(TcpRequestView record) => new(
        record.Time, record.ClientId, record.Client,
        record.Request, record.Reply, record.Ok, record.ElapsedMs);

    private static string FormatLastLine(RequestRow row) =>
        $"{row.Request} → {row.ReplyTrimmed}";

    private void RefreshStatus()
    {
        ServiceStatus = _tcp.IsRunning
            ? PlcAlwaysOkMode
                ? $"运行中 · {_tcp.ConnectedClients} 个客户端 · PLC 调试"
                : $"运行中 · {_tcp.ConnectedClients} 个客户端"
            : PlcAlwaysOkMode
                ? "已停止 · PLC 调试"
                : "已停止";
        ToggleButtonLabel = _tcp.IsRunning ? "停止服务" : "启动服务";
        SummaryText =
            $"监听 {_tcp.ListenEndPoint} · 累计连接 {_tcp.TotalConnections} · 累计请求 {_tcp.TotalRequests}";

        ConnectedClients = _tcp.ConnectedClients;
        TotalRequestsText = $"{_tcp.TotalRequests:N0}";
        RefreshStats();
    }

    /// <summary>从 VisionService 拉取按配方聚合的统计（含手动触发；累计可落盘，重启不丢）。</summary>
    private void RefreshStats()
    {
        var stats = _vision.GetRecipeStats();

        StatsRows.Clear();
        foreach (var s in stats)
        {
            var rate = s.Total == 0 ? 0 : (double)s.Ok / s.Total;
            var tier = rate >= 0.9 ? "good" : rate >= 0.6 ? "warn" : "poor";
            StatsRows.Add(new RecipeStatsRow(
                s.Recipe, s.Total, s.Ok, s.Failed,
                $"{s.SuccessRate:P1}", $"{s.AvgMs:0}", $"{s.LastMs:0}",
                s.LastAt?.ToString("HH:mm:ss", CultureInfo.InvariantCulture) ?? "—",
                rate, tier, s.ConsecutiveFails));
        }

        var total = stats.Sum(s => s.Total);
        var ok = stats.Sum(s => s.Ok);
        var avg = total == 0 ? 0 : stats.Sum(s => s.AvgMs * s.Total) / total;
        StatsSummary = total == 0
            ? "暂无触发记录"
            : $"总触发 {total:N0} · 成功率 {(double)ok / total:P1} · 平均耗时 {avg:0} ms";

        SuccessRateText = total == 0 ? "—" : $"{(double)ok / total:P1}";
        AvgMsText = total == 0 ? "—" : $"{avg:0} ms";

        OnPropertyChanged(nameof(PlcAlwaysOkMode));
        OnPropertyChanged(nameof(HasPlcDebugWarning));
        OnPropertyChanged(nameof(PlcDebugWarningText));

        InterlockText = InterlockBannerText.Format(_vision, includeTcpHint: true);

        OnPropertyChanged(nameof(HasInterlock));
        OnPropertyChanged(nameof(HasPlcDebugWarning));
        OnPropertyChanged(nameof(HasStats));
    }

    private void SetPlcAlwaysOkMode(bool value)
    {
        if (_syncingPlcDebug || value == _tcp.PlcAlwaysOkMode)
            return;

        if (value &&
            !_dialogs.ConfirmYesNo(
                "启用后 TCP 将不再向 PLC 返回 ERR（失败时回默认坐标的 OK）。\n" +
                "视觉仍会照常采图推理，仅协议线伪装成功。\n\n" +
                "仅供 PLC 联调，正式产线务必关闭。继续？",
                "PLC 调试模式"))
        {
            _syncingPlcDebug = true;
            OnPropertyChanged(nameof(PlcAlwaysOkMode));
            OnPropertyChanged(nameof(HasPlcDebugWarning));
            _syncingPlcDebug = false;
            return;
        }

        _cfg.PlcDebug.AlwaysOk = value;
        _tcp.PlcAlwaysOkMode = value;
        _tcp.PlcDebugDefaultX = _cfg.PlcDebug.DefaultX;
        _tcp.PlcDebugDefaultY = _cfg.PlcDebug.DefaultY;
        _tcp.PlcDebugDefaultRz = _cfg.PlcDebug.DefaultRz;
        OnPropertyChanged(nameof(PlcAlwaysOkMode));
        OnPropertyChanged(nameof(HasPlcDebugWarning));
        OnPropertyChanged(nameof(PlcDebugWarningText));
        RefreshStatus();
    }

    [RelayCommand]
    private void ClearInhibit()
    {
        _vision.ClearInhibit();
        RefreshStats();
    }

    [RelayCommand]
    private void DisconnectClient(ClientRow? row)
    {
        if (row is null)
            return;
        _tcp.DisconnectClient(row.Id);
    }

    [RelayCommand]
    private void ClearRequests()
    {
        Requests.Clear();
        LastTcpLineText = "尚无 TCP 请求";
        NotifyLists();
    }

    [RelayCommand]
    private void Refresh() => RefreshStatus();

    [RelayCommand]
    private void ToggleService()
    {
        if (_tcp.IsRunning)
        {
            if (!ConfirmStopService())
                return;
            _tcp.Stop();
        }
        else
        {
            try
            {
                _tcp.Start();
            }
            catch (Exception ex)
            {
                ServiceStatus = $"启动失败: {ex.Message}";
                return;
            }
        }
        RefreshStatus();
    }

    /// <summary>停止 TCP 前确认（测试可注入，默认 MessageBox）。</summary>
    internal Func<bool>? ConfirmStopServiceForTests { get; set; }

    private bool ConfirmStopService()
    {
        if (ConfirmStopServiceForTests is { } confirm)
            return confirm();

        return _dialogs.ConfirmYesNo(
            "停止后 PLC 将无法通过 TCP 触发视觉（STATUS/TRIGGER 均不可用）。\n\n" +
            "确认停止 TCP 服务？",
            "停止 TCP 服务");
    }

    private void NotifyLists()
    {
        if (Requests.Count > 0)
            LastTcpLineText = FormatLastLine(Requests[0]);
        OnPropertyChanged(nameof(HasRequests));
    }

    private static void Dispatch(Action action)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess())
            action();
        else
            dispatcher.BeginInvoke(action);
    }
}
