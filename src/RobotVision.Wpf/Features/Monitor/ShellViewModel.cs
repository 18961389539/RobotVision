using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using RobotVision.Hosting;

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

    public void Dispose() => _statusTimer.Stop();
}
