using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RobotVision.Infrastructure.Communication;

namespace RobotVision.Hosting;

/// <summary>
/// 将 TCP 服务挂入 Generic Host 生命周期。
/// 启动失败（端口被占用等）不抛出：视觉 UI/手动触发仍可用，仅机器人链路不可用，避免整机崩溃。
/// </summary>
public sealed class TcpHostedService(TcpServerManager tcp, ILogger<TcpHostedService> log) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            tcp.Start();
        }
        catch (Exception ex)
        {
            TcpHostedServiceLog.StartFailed(log, ex, tcp.Port);
        }
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        TcpHostedServiceLog.Stopping(log);
        tcp.Stop();
        return Task.CompletedTask;
    }
}
