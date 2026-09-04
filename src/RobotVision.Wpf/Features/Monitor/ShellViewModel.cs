using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using RobotVision.Hosting;
using RobotVision.WpfHost.Shared;

namespace RobotVision.WpfHost.Features.Monitor;

/// <summary>主窗口壳：全局 TCP 状态栏（与具体页面 VM 解耦）。</summary>
public sealed partial class ShellViewModel : ObservableObject, IDisposable
{
    private readonly ITcpRuntime _tcp;
    private readonly VisionService _vision;
    private readonly DispatcherTimer _statusTimer;

    [ObservableProperty]
    private string _tcpStatus = "TCP 未启动";

    [ObservableProperty]
    private bool _isTcpRunning;

    /// <summary>后台/启动期故障提示（配方半加载、后台服务启动失败等）；空则状态栏不显示。不被 1s 轮询覆盖。</summary>
    [ObservableProperty]
    private string? _notice;

    public ShellViewModel(ITcpRuntime tcp, VisionService vision)
    {
        _tcp = tcp;
        _vision = vision;
        _statusTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _statusTimer.Tick += (_, _) => RefreshTcpStatus();
        _statusTimer.Start();
        RefreshTcpStatus();
    }

    private void RefreshTcpStatus()
    {
        IsTcpRunning = _tcp.IsRunning;
        TcpStatus = $"TCP {_tcp.ConnectedClients} 客户端 · 队列 {_vision.QueueDepth}/{_vision.MaxQueueDepth}";
    }

    /// <summary>把后台/启动期故障汇总到状态栏。可从任意线程调用（内部切回 UI 线程）。</summary>
    public void ReportBackgroundFailure(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return;
        UiDispatch.Begin(() => Notice = message);
    }

    public void Dispose() => _statusTimer.Stop();
}
