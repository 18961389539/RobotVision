using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RobotVision.Hosting;
using RobotVision.Infrastructure.Communication;

namespace RobotVision.WpfHost;

/// <summary>连接列表行：统计随请求事件实时更新。</summary>
public partial class ClientRow : ObservableObject
{
    public ClientRow(TcpClientSnapshot snapshot)
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

    public string Ip => Remote.Contains(':') ? Remote[..Remote.LastIndexOf(':')] : Remote;
    public string Port => Remote.Contains(':') ? Remote[(Remote.LastIndexOf(':') + 1)..] : "";

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
    public string TimeText => Time.ToString("HH:mm:ss.fff");
    public string Result => Ok ? "OK" : "ERR";
    public string ElapsedText => $"{ElapsedMs:0} ms";
    public string ReplyTrimmed => Reply.Length <= 96 ? Reply : Reply[..96] + "…";
}

/// <summary>按配方聚合的运行统计行（1 秒周期重建，量级小无绑定开销）。
/// Rate = 成功率 0~1（进度条宽度）；Tier = 颜色档（good≥0.9 / warn≥0.6 / poor）。</summary>
public sealed record RecipeStatsRow(
    string Recipe, long Total, long Ok, long Failed,
    string SuccessRate, string AvgMs, string LastMs, string LastAt,
    double Rate, string Tier);

/// <summary>
/// 通信监控：连接列表、请求历史、主动断开与服务启停、按配方运行统计。
/// 事件来自套接字线程，统一封送到 UI 线程后更新集合。
/// </summary>
public partial class CommunicationViewModel : ObservableObject
{
    private const int MaxRequestRows = 200;

    private readonly TcpServerManager _tcp;
    private readonly VisionService _vision;
    private readonly DispatcherTimer _timer;

    public ObservableCollection<ClientRow> Clients { get; } = [];

    public ObservableCollection<RequestRow> Requests { get; } = [];

    public ObservableCollection<RecipeStatsRow> StatsRows { get; } = [];

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

    public CommunicationViewModel(TcpServerManager tcp, VisionService vision)
    {
        _tcp = tcp;
        _vision = vision;
        foreach (var snapshot in _tcp.GetClients())
            Clients.Add(new ClientRow(snapshot));

        _tcp.ClientConnected += OnClientConnected;
        _tcp.ClientDisconnected += OnClientDisconnected;
        _tcp.RequestProcessed += OnRequestProcessed;

        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += (_, _) => RefreshStatus();
        _timer.Start();
        RefreshStatus();
    }

    /// <summary>页面可见时刷新状态（配合 Unloaded 停止，避免后台空转）。</summary>
    public void StartTimer() => _timer.Start();

    public void StopTimer() => _timer.Stop();

    private void OnClientConnected(TcpClientSnapshot snapshot) =>
        Dispatch(() => Clients.Add(new ClientRow(snapshot)));

    private void OnClientDisconnected(TcpClientSnapshot snapshot) => Dispatch(() =>
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

    private void OnRequestProcessed(TcpRequestRecord record) => Dispatch(() =>
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

        Requests.Insert(0, new RequestRow(
            record.Time, record.ClientId, record.Client,
            record.Request, record.Reply, record.Ok, record.ElapsedMs));
        while (Requests.Count > MaxRequestRows)
            Requests.RemoveAt(Requests.Count - 1);
    });

    private void RefreshStatus()
    {
        ServiceStatus = _tcp.IsRunning
            ? $"运行中 · {_tcp.ConnectedClients} 个客户端"
            : "已停止";
        ToggleButtonLabel = _tcp.IsRunning ? "停止服务" : "启动服务";
        SummaryText =
            $"监听 {_tcp.ListenEndPoint} · 累计连接 {_tcp.TotalConnections} · 累计请求 {_tcp.TotalRequests}";

        ConnectedClients = _tcp.ConnectedClients;
        TotalRequestsText = $"{_tcp.TotalRequests:N0}";
        RefreshStats();
    }

    /// <summary>从 VisionService 拉取按配方聚合的统计（含手动触发；进程内存统计，重启归零）。</summary>
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
                s.LastAt?.ToString("HH:mm:ss") ?? "—",
                rate, tier));
        }

        var total = stats.Sum(s => s.Total);
        var ok = stats.Sum(s => s.Ok);
        var avg = total == 0 ? 0 : stats.Sum(s => s.AvgMs * s.Total) / total;
        StatsSummary = total == 0
            ? "暂无触发记录"
            : $"总触发 {total:N0} · 成功率 {(double)ok / total:P1} · 平均耗时 {avg:0} ms";

        SuccessRateText = total == 0 ? "—" : $"{(double)ok / total:P1}";
        AvgMsText = total == 0 ? "—" : $"{avg:0} ms";
    }

    [RelayCommand]
    private void DisconnectClient(ClientRow? row)
    {
        if (row is null)
            return;
        _tcp.DisconnectClient(row.Id);
    }

    [RelayCommand]
    private void ClearRequests() => Requests.Clear();

    [RelayCommand]
    private void ToggleService()
    {
        if (_tcp.IsRunning)
        {
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

    private static void Dispatch(Action action)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess())
            action();
        else
            dispatcher.BeginInvoke(action);
    }
}
