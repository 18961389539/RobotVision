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
            log.LogError(ex, "TCP 服务启动失败（端口 {Port} 可能被占用），机器人链路不可用；视觉服务继续运行",
                tcp.Port);
        }
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        log.LogInformation("正在停止 TCP 服务...");
        tcp.Stop();
        return Task.CompletedTask;
    }
}
